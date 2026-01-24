using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Editor preview window for generated dungeon graphs.
    ///
    /// Mental model:
    /// - <see cref="DungeonGraphRewriter"/> generates two representations:
    ///   1) <see cref="DungeonCycle"/>: the *nested derivation tree* (grammar expansion result).
    ///      - Useful for inspecting rewrite structure, debugging generation rules, and later “mappable” workflows.
    ///   2) <see cref="FlatGraph"/>: the *final connectivity graph* (nodes/edges after rewrites are applied).
    ///      - This is the graph we want to draw *readably* and (eventually) map into space.
    ///
    /// Preview goals:
    /// - "Preview always readable": avoid crossings when the connectivity graph is planar.
    /// - "Mappable later": keep the nested root cycle around for future mapping workflows, but do not render it here.
    ///
    /// Rendering pipeline in this window:
    /// - Generate nested + flat graphs.
    /// - Compute a 2D planar (or fallback) layout for the flat graph via <see cref="PreviewLayoutEngine"/>.
    /// - Render the positioned flat graph via <see cref="PreviewGraphRenderer"/>.
    /// </summary>
    public sealed class DunGenPreviewCanvas : EditorWindow
    {
        // =========================================================
        // GENERATED DATA (most recent run)
        // =========================================================

        /// <summary>
        /// Nested derivation tree produced by grammar instantiation.
        /// Kept for inspection and future mapping work, but not used for rendering in this window.
        /// </summary>
        [System.NonSerialized] private DungeonCycle _generatedRootCycle;

        /// <summary>
        /// Final connectivity graph after rewrite splicing.
        /// This is the “draw this” representation for readable preview.
        /// </summary>
        [System.NonSerialized] private FlatGraph _generatedFlatGraph;

        /// <summary>Generator settings used to pick templates and apply rewrite rules.</summary>
        [SerializeField] private DungeonGenerationSettings generationSettings;

        /// <summary>Seed used for the most recent generation (shown in inspector).</summary>
        [SerializeField] private int currentSeed;

        // =========================================================
        // LAYOUT (flat planar / fallback)
        // =========================================================

        /// <summary>
        /// Cached layout output for the most recent flat graph.
        /// Includes planarity result and computed vertex positions.
        /// </summary>
        private PreviewLayoutEngine.Result _layoutResult;

        // =========================================================
        // VIEW + UI
        // =========================================================

        private AuthoringGraphRenderer _renderer;

        /// <summary>Camera-like state for panning/zooming in the canvas.</summary>
        private CameraController _camera;

        /// <summary>
        /// Handles selection/hit testing and stores per-mode transient UI state.
        /// For preview mode, this is primarily “which node is selected”.
        /// </summary>
        private PreviewModeController _previewController;

        /// <summary>Inspector panel renderer (stats + selected node details).</summary>
        private PreviewInspector _inspector;

        /// <summary>Shared styling/config for node visuals (kept for future polish).</summary>
        private NodeStyleProvider _styleProvider;

        /// <summary>
        /// Nested derivation tree generated most recently.
        /// </summary>
        /// <remarks>
        /// This is only valid after a successful Generate.
        /// It is not currently used to draw the preview; it exists for inspection and future mapping work.
        /// </remarks>
        public DungeonCycle GeneratedRootCycle => _generatedRootCycle;

        /// <summary>
        /// Flat connectivity graph generated most recently.
        /// </summary>
        /// <remarks>
        /// This is the canonical “preview graph” representation: nodes/edges after rewrites are applied.
        /// </remarks>
        public FlatGraph GeneratedFlatGraph => _generatedFlatGraph;

        // =========================================================
        // WINDOW
        // =========================================================

        [MenuItem("Tools/DunGen/Preview Graph")]
        public static void Open()
        {
            var w = GetWindow<DunGenPreviewCanvas>("DunGen Preview");
            w.minSize = new Vector2(1000, 650);
        }

        private void OnEnable()
        {
            _camera = new CameraController();
            _renderer = new AuthoringGraphRenderer();
            _styleProvider = new NodeStyleProvider();
            _previewController = new PreviewModeController(25f); // Hit-test radius in *world* space (before zoom).
            _inspector = new PreviewInspector(_styleProvider);

            // If the domain reloads and we still have cached data, rebuild layout so the window redraws correctly.
            if (_generatedRootCycle != null)
                RebuildLayout();
        }

        private void OnGUI()
        {
            DrawToolbar();

            const float toolbarH = 36f;
            const float inspectorW = 320f;

            var canvasRect = new Rect(0, toolbarH, position.width - inspectorW, position.height - toolbarH);
            var inspectorRect = new Rect(position.width - inspectorW, toolbarH, inspectorW, position.height - toolbarH);

            DrawCanvas(canvasRect);
            DrawInspector(inspectorRect);
        }

        // =========================================================
        // TOOLBAR
        // =========================================================

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                generationSettings = (DungeonGenerationSettings)EditorGUILayout.ObjectField(
                    new GUIContent("Settings"),
                    generationSettings,
                    typeof(DungeonGenerationSettings),
                    false,
                    GUILayout.Width(360));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Generate", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    GenerateProcedural();

                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    FitView();

                if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    ResetView();
            }
        }

        // =========================================================
        // CANVAS
        // =========================================================

        private void DrawCanvas(Rect canvasRect)
        {
            _renderer.DrawBackground(canvasRect);

            // Input is handled before drawing so selection and camera changes apply immediately.
            HandleCanvasInput(canvasRect);

            _renderer.DrawGrid(canvasRect, _camera);

            // Planarity status is derived from the *layout result we are actually rendering*.
            // This avoids mismatches where a simplified planarity check disagrees with draw-time data.
            bool hasGraph = _generatedFlatGraph != null && !_generatedFlatGraph.IsEmpty;
            bool planar = _layoutResult != null && _layoutResult.isPlanar;

            string msg = !hasGraph
                ? "Planarity: (no graph)"
                : (planar
                    ? "Planarity: Planar (embedding available)"
                    : "Planarity: Non-planar (crossings unavoidable)");

            var r = new Rect(canvasRect.x + 10, canvasRect.y + 10, 520, 44);
            EditorGUI.HelpBox(r, msg, planar ? MessageType.Info : MessageType.Warning);

            // Draw the positioned flat graph.
            // Note: We intentionally draw only the flat connectivity representation here.
            // The nested root cycle remains available for analysis/mapping work, but is not rendered in this view.
            if (_generatedFlatGraph != null &&
                _layoutResult != null &&
                _layoutResult.positions != null &&
                _layoutResult.positions.Count > 0)
            {
                var start = _generatedRootCycle != null ? _generatedRootCycle.startNode : null;
                var goal = _generatedRootCycle != null ? _generatedRootCycle.goalNode : null;

                _renderer.DrawEdges(
                    _generatedFlatGraph,
                    _layoutResult.positions,
                    canvasRect,
                    _camera,
                    _styleProvider
                );

                _renderer.DrawNodes(
                    _generatedFlatGraph,
                    _layoutResult.positions,
                    canvasRect,
                    _camera,
                    _styleProvider,
                    _generatedRootCycle,
                    _previewController.SelectedNode
                );

            }
            else if (_generatedFlatGraph != null)
            {
                // We have a graph, but no layout positions (layout failed or returned empty).
                // This should be rare; check console for details.
                var helpStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };

                var helpRect = new Rect(
                    canvasRect.center.x - 170,
                    canvasRect.center.y - 45,
                    340,
                    90
                );

                GUI.Box(helpRect, "Layout not available.\nCheck console for errors.", helpStyle);
            }

            _previewController.DrawOverlays(canvasRect, _camera);
        }

        /// <summary>
        /// Handles camera controls (pan/zoom) and forwards selection input to the preview controller.
        /// </summary>
        private void HandleCanvasInput(Rect canvasRect)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!canvasRect.Contains(mousePos))
                return;

            // Pan camera (MMB drag)
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _camera.Pan(e.delta);
                e.Use();
                return;
            }

            // Zoom camera (scroll wheel)
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 0.05f;
                _camera.ZoomToward(mousePos, canvasRect, zoomDelta);
                e.Use();
                return;
            }

            // Selection (LMB) and other mode-specific behavior.
            _previewController.HandleInput(e, mousePos, canvasRect, _camera);
        }

        // =========================================================
        // INSPECTOR
        // =========================================================

        private void DrawInspector(Rect inspectorRect)
        {
            // The inspector reads:
            // - the computed layout result (planarity + positions)
            // - the flat graph (connectivity / degrees / etc.)
            // - the selected node (UI state)
            // - the root cycle (generation context and start/goal references)
            _inspector.DrawInspector(
                inspectorRect,
                _layoutResult,
                _generatedFlatGraph,
                _previewController.SelectedNode,
                _generatedRootCycle,
                currentSeed
            );
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        /// <summary>
        /// Runs the full preview pipeline:
        /// 1) Validate settings.
        /// 2) Generate a seed.
        /// 3) Compile the dungeon grammar into:
        ///    - a nested derivation tree (<see cref="DungeonCycle"/>)
        ///    - and a flat connectivity graph (<see cref="FlatGraph"/>).
        /// 4) Compute a planar (or fallback) 2D layout for the flat graph.
        /// </summary>
        private void GenerateProcedural()
        {
            if (generationSettings == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Please assign a DungeonGenerationSettings asset.", "OK");
                return;
            }

            if (!generationSettings.IsValid(out var err))
            {
                EditorUtility.DisplayDialog("Generation Failed", err, "OK");
                return;
            }

            // New seed for each generation, displayed in inspector.
            currentSeed = Random.Range(0, 10000);
            _previewController.SetSeed(currentSeed);

            Debug.Log($"[Preview] ========== GENERATING DUNGEON (Seed: {currentSeed}) ==========");

            // One-shot pipeline:
            // - Instantiate nested rewrite tree (DungeonCycle)
            // - Rewrite/splice to final flat connectivity (FlatGraph)
            _generatedFlatGraph = DungeonGraphRewriter.CompileToFlatGraph(
                generationSettings,
                currentSeed,
                out _generatedRootCycle
            );

            if (_generatedRootCycle == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Generator returned null. Check console for errors.", "OK");
                return;
            }

            if (_generatedFlatGraph == null || _generatedFlatGraph.IsEmpty)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Rewriter returned an empty graph. Check console for errors.", "OK");
                return;
            }

            Debug.Log(
                $"[Preview] Nested root cycle nodes: {_generatedRootCycle.nodes?.Count ?? 0}, " +
                $"Flat nodes: {_generatedFlatGraph.NodeCount}, Flat edges: {_generatedFlatGraph.EdgeCount}"
            );

            RebuildLayout();
            ResetView();

            // Delay fit to ensure the window has a valid size and layout result has populated positions.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                FitView();
                Repaint();
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private void RebuildLayout()
        {
            if (_generatedFlatGraph == null || _generatedFlatGraph.IsEmpty)
            {
                _layoutResult = null;
                return;
            }

            _layoutResult = PreviewLayoutEngine.Compute(_generatedFlatGraph);

            // REQUIRED for correct coloring & slot markers
            _styleProvider.Clear();
            _styleProvider.BuildDepthMap(_generatedRootCycle);

            _previewController.SetGraph(_generatedFlatGraph);

            if (_layoutResult == null)
                Debug.LogError("[Preview] Layout computation failed!");
        }


        /// <summary>
        /// Fits the camera to the currently computed layout bounds.
        /// </summary>
        private void FitView()
        {
            if (_layoutResult?.positions == null || _layoutResult.positions.Count == 0)
                return;

            var bounds = CameraController.CalculateWorldBounds(_layoutResult.positions, 25f);
            var canvasSize = new Vector2(position.width - 320f, position.height - 36f);
            _camera.FitToBounds(bounds, canvasSize, padding: 50f);
        }

        /// <summary>
        /// Resets camera pan/zoom to defaults.
        /// </summary>
        private void ResetView()
        {
            _camera.Reset();
        }
    }
}
