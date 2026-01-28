using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Editor preview window for generated dungeon graphs.
    /// UPDATED: Now supports edge selection and highlighting in preview mode.
    /// </summary>
    public sealed class DunGenPreviewCanvas : EditorWindow
    {
        // =========================================================
        // GENERATED DATA (most recent run)
        // =========================================================

        [System.NonSerialized] private DungeonCycle _generatedRootCycle;
        [System.NonSerialized] private FlatGraph _generatedFlatGraph;
        [System.NonSerialized] private KeyRegistry _keyRegistry;

        [SerializeField] private DungeonGenerationSettings generationSettings;
        [SerializeField] private int currentSeed;

        // =========================================================
        // LAYOUT (flat planar / fallback)
        // =========================================================

        private PreviewLayoutEngine.Result _layoutResult;

        // =========================================================
        // VIEW + UI
        // =========================================================

        private GraphRenderer _renderer;
        private CameraController _camera;
        private PreviewModeController _previewController;
        private PreviewInspector _inspector;
        private NodeStyleProvider _styleProvider;

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
            _renderer = new GraphRenderer();
            _styleProvider = new NodeStyleProvider();
            _previewController = new PreviewModeController(25f);
            _inspector = new PreviewInspector(_styleProvider);

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
            HandleCanvasInput(canvasRect);
            _renderer.DrawGrid(canvasRect, _camera);

            bool hasGraph = _generatedFlatGraph != null && !_generatedFlatGraph.IsEmpty;
            bool planar = _layoutResult != null && _layoutResult.isPlanar;

            string msg = !hasGraph
                ? "Planarity: (no graph)"
                : (planar
                    ? "Planarity: Planar (embedding available)"
                    : "Planarity: Non-planar (crossings unavoidable)");

            var r = new Rect(canvasRect.x + 10, canvasRect.y + 10, 520, 44);
            EditorGUI.HelpBox(r, msg, planar ? MessageType.Info : MessageType.Warning);

            if (_generatedFlatGraph != null &&
                _layoutResult != null &&
                _layoutResult.positions != null &&
                _layoutResult.positions.Count > 0)
            {
                // FIXED: Pass selected edge for highlighting
                _renderer.DrawEdges(
                    _generatedFlatGraph,
                    _layoutResult.positions,
                    canvasRect,
                    _camera,
                    _styleProvider,
                    _previewController.SelectedEdge);

                _renderer.DrawNodes(
                    _generatedFlatGraph,
                    _layoutResult.positions,
                    canvasRect,
                    _camera,
                    _styleProvider,
                    _generatedRootCycle,
                    _previewController.SelectedNode);
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

            // Selection (LMB) - handled by preview controller
            _previewController.HandleInput(e, mousePos, canvasRect, _camera);
        }

        // =========================================================
        // INSPECTOR
        // =========================================================

        private void DrawInspector(Rect inspectorRect)
        {
            _inspector.DrawInspector(
                inspectorRect,
                _layoutResult,
                _generatedFlatGraph,
                _previewController.SelectedNode,
                _previewController.SelectedEdge,
                _generatedRootCycle,
                currentSeed,
                _keyRegistry
            );
        }

        // =========================================================
        // ACTIONS
        // =========================================================

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

            currentSeed = Random.Range(0, 10000);
            _previewController.SetSeed(currentSeed);

            Debug.Log($"[Preview] ========== GENERATING DUNGEON (Seed: {currentSeed}) ==========");

            _generatedFlatGraph = DungeonGraphRewriter.CompileToFlatGraph(
                generationSettings,
                currentSeed,
                out _generatedRootCycle,
                out _keyRegistry
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

            _styleProvider.Clear();
            _styleProvider.BuildDepthMap(_generatedRootCycle);

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