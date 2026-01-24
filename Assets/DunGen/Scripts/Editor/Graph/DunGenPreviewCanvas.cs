using UnityEditor;
using UnityEngine;

using GraphPlanarityTesting.PlanarityTesting.BoyerMyrvold;

namespace DunGen.Editor
{
    /// <summary>
    /// Preview canvas: compile a grammar and render the HIERARCHICAL graph.
    /// Uses PreviewLayoutEngine and PreviewGraphRenderer for generated dungeons.
    /// </summary>
    public sealed class DunGenPreviewCanvas : EditorWindow
    {
        // =========================================================
        // DATA
        // =========================================================
        [System.NonSerialized] private DungeonCycle _generatedRootCycle;
        [System.NonSerialized] private FlatGraph _generatedFlatGraph;
        [SerializeField] private DungeonGenerationSettings generationSettings;
        [SerializeField] private int currentSeed;

        // Layout result (hierarchical)
        private PreviewLayoutEngine.Result _layoutResult;

        // Render + camera
        private PreviewGraphRenderer _renderer;
        private CameraController _camera;

        // Mode controller + inspector
        private PreviewModeController _previewController;
        private PreviewInspector _inspector;
        private NodeStyleProvider _styleProvider;

        // Planarity test cache
        private bool _isPlanar;
        private PlanarEmbedding<GraphNode> _planarEmbedding;
        private string _planarityWarning;

        /// <summary>
        /// Gets the root cycle that was generated for the dungeon during the most recent generation process.
        /// </summary>
        /// <remarks>The root cycle represents the primary loop or cycle structure within the dungeon
        /// layout. This property can be used to analyze or visualize the overall connectivity and flow of the generated
        /// dungeon. The value is only valid after dungeon generation has completed successfully.</remarks>
        public DungeonCycle GeneratedRootCycle => _generatedRootCycle;

        /// <summary>
        /// Gets the generated flat graph representation of the data.
        /// </summary>
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
            _renderer = new PreviewGraphRenderer();
            _styleProvider = new NodeStyleProvider();
            _previewController = new PreviewModeController(25f); // Node radius
            _inspector = new PreviewInspector(_styleProvider);

            if (_generatedRootCycle != null)
                RebuildLayout();
        }

        private void OnGUI()
        {
            DrawToolbar();

            var toolbarH = 36f;
            var inspectorW = 320f;

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

            HandleCanvasInput(canvasRect);

            _renderer.DrawGrid(canvasRect, _camera);

            {
                var msg = _generatedFlatGraph == null
                    ? "Planarity: (no graph)"
                    : (_isPlanar
                        ? "Planarity: Planar (embedding available)"
                        : "Planarity: Non-planar (crossings unavoidable)");

                var r = new Rect(canvasRect.x + 10, canvasRect.y + 10, 520, 44);
                EditorGUI.HelpBox(r, msg, _isPlanar ? MessageType.Info : MessageType.Warning);
            }

            if (_generatedFlatGraph != null && _layoutResult != null && _layoutResult.positions != null && _layoutResult.positions.Count > 0)
            {
                var start = _generatedRootCycle != null ? _generatedRootCycle.startNode : null;
                var goal = _generatedRootCycle != null ? _generatedRootCycle.goalNode : null;

                _renderer.DrawFlatGraph(
                    _generatedFlatGraph,
                    _layoutResult.positions,
                    canvasRect,
                    _camera,
                    _previewController.SelectedNode,
                    start,
                    goal
                );
            }
            else if (_generatedFlatGraph != null)
            {
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

        private void HandleCanvasInput(Rect canvasRect)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!canvasRect.Contains(mousePos))
                return;

            // Camera pan (MMB)
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _camera.Pan(e.delta);
                e.Use();
                return;
            }

            // Zoom (wheel)
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 0.05f;
                _camera.ZoomToward(mousePos, canvasRect, zoomDelta);
                e.Use();
                return;
            }

            // Forward to controller for node selection
            _previewController.HandleInput(e, mousePos, canvasRect, _camera);
        }

        // =========================================================
        // INSPECTOR
        // =========================================================
        private void DrawInspector(Rect inspectorRect)
        {
            // Delegate to PreviewInspector (like AuthorCanvas does with AuthorInspector)
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

        private void GenerateProcedural()
        {
            // Validate settings
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

            // Generate new seed and set in controller
            currentSeed = Random.Range(0, 10000);
            _previewController.SetSeed(currentSeed);

            Debug.Log($"[Preview] ========== GENERATING DUNGEON (Seed: {currentSeed}) ==========");

            // One-shot dungeon graph pipeline:
            // - Instantiate grammar (nested DungeonCycle rewrite tree)
            // - Apply rewrites to produce final flat connectivity (FlatGraph)
            // Returns:
            // - _generatedRootCycle: nested derivation tree (for hierarchical layout / inspection)
            // - _generatedFlatGraph: flat connectivity graph (for planarity + mapping)
            _generatedFlatGraph = DungeonGraphRewriter.CompileToFlatGraph(
                generationSettings,
                currentSeed,
                out _generatedRootCycle
            );

            _isPlanar = FlatGraphPlanarity.TryGetEmbedding(_generatedFlatGraph, out _planarEmbedding);

            _planarityWarning = _isPlanar
                ? null
                : "Non-planar: edge crossings are unavoidable. Falling back to best-effort layout.";


            // Validate results
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

            // Results are valid, log summary
            Debug.Log(
                $"[Preview] Nested root cycle nodes: {_generatedRootCycle.nodes?.Count ?? 0}, " +
                $"Flat nodes: {_generatedFlatGraph.NodeCount}, Flat edges: {_generatedFlatGraph.EdgeCount}"
            );

            // Rebuild layout + reset view
            RebuildLayout();
            ResetView();

            // Delay fit + repaint to end of frame to ensure layout is ready
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                FitView();
                Repaint();
            };
        }

        private void RebuildLayout()
        {
            if (_generatedFlatGraph == null || _generatedFlatGraph.IsEmpty)
            {
                _layoutResult = null;
                return;
            }

            _layoutResult = PreviewLayoutEngine.Compute(_generatedFlatGraph);

            // keep controller positions in sync for hit-testing
            _previewController.SetGraph(_generatedFlatGraph);

            if (_layoutResult == null)
                Debug.LogError("[Preview] Layout computation failed!");
        }

        private void FitView()
        {
            if (_layoutResult?.positions == null || _layoutResult.positions.Count == 0)
                return;

            var bounds = CameraController.CalculateWorldBounds(_layoutResult.positions, 25f);
            var canvasSize = new Vector2(position.width - 320f, position.height - 36f);
            _camera.FitToBounds(bounds, canvasSize, padding: 50f);
        }

        private void ResetView()
        {
            _camera.Reset();
        }
    }
}