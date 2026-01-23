using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
        [System.NonSerialized] private DungeonCycle generatedCycle;
        [SerializeField] private DungeonGenerationSettings generationSettings;
        [SerializeField] private int currentSeed;

        // Layout result (hierarchical)
        private PreviewLayoutEngine.LayoutResult _layoutResult;

        // Render + camera
        private PreviewGraphRenderer _renderer;
        private CameraController _camera;

        // Mode controller + inspector
        private PreviewModeController _previewController;
        private PreviewInspector _inspector;
        private NodeStyleProvider _styleProvider;

        public DungeonCycle GeneratedCycle => generatedCycle;

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

            if (generatedCycle != null)
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

            if (_layoutResult != null && _layoutResult.root != null)
            {
                _renderer.DrawHierarchicalGraph(
                    _layoutResult,
                    canvasRect,
                    _camera,
                    _previewController.SelectedNode
                );
            }
            else if (generatedCycle != null)
            {
                // Show message if layout failed
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

                GUI.Box(helpRect, "Layout generation failed.\nCheck console for errors.", helpStyle);
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
                _previewController.SelectedNode,
                generatedCycle,
                currentSeed
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

            // CRITICAL: Compile the template into a generated dungeon
            var compiler = new DungeonGraphCompiler(generationSettings);
            generatedCycle = compiler.CompileCycle(currentSeed);

            if (generatedCycle == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Generator returned null. Check console for errors.", "OK");
                return;
            }

            Debug.Log($"[Preview] Generated cycle has {generatedCycle.nodes?.Count ?? 0} nodes");

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
            if (generatedCycle == null)
            {
                _layoutResult = null;
                return;
            }

            Debug.Log($"[Preview] ===== COMPUTING HIERARCHICAL LAYOUT =====");

            // Compute hierarchical layout using PreviewLayoutEngine
            _layoutResult = PreviewLayoutEngine.ComputeLayout(generatedCycle);

            if (_layoutResult != null)
            {
                Debug.Log($"[Preview] Layout complete:");
                Debug.Log($"  - Total cycles: {_layoutResult.allCycles.Count}");
                Debug.Log($"  - Total nodes: {_layoutResult.allPositions.Count}");
                Debug.Log($"  - Max depth: {_inspector.GetMaxDepth(_layoutResult)}");

                // Update controller
                _previewController.SetCycle(generatedCycle);
            }
            else
            {
                Debug.LogError("[Preview] Layout computation failed!");
            }
        }

        private void FitView()
        {
            if (_layoutResult?.allPositions == null || _layoutResult.allPositions.Count == 0)
                return;

            var bounds = CameraController.CalculateWorldBounds(_layoutResult.allPositions, 25f);
            var canvasSize = new Vector2(position.width - 320f, position.height - 36f);
            _camera.FitToBounds(bounds, canvasSize, padding: 50f);
        }

        private void ResetView()
        {
            _camera.Reset();
        }
    }
}