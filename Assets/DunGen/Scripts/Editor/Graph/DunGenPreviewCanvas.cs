using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Preview canvas: Generate and view expanded cycles.
    /// Standalone window for read-only dungeon preview.
    /// </summary>
    public sealed class DunGenPreviewCanvas : EditorWindow
    {
        // =========================================================
        // DATA
        // =========================================================
        [System.NonSerialized] private DungeonCycle generatedCycle;
        [SerializeField] private DungeonGenerationSettings generationSettings;
        [SerializeField] private float nodeRadius = 25.0f;
        [SerializeField] private int currentSeed;

        // Cached data
        private RewrittenGraph _flatGraph;
        private Dictionary<CycleNode, Vector2> _nodePositions = new Dictionary<CycleNode, Vector2>();

        // =========================================================
        // SHARED COMPONENTS
        // =========================================================
        private CameraController _camera = new CameraController();
        private GraphRenderer _renderer = new GraphRenderer();
        private NodeStyleProvider _styleProvider = new NodeStyleProvider();
        private PreviewInspector _inspector;

        // =========================================================
        // PREVIEW MODE CONTROLLER
        // =========================================================
        private PreviewModeController _previewController;

        // =========================================================
        // WINDOW SETUP
        // =========================================================
        [MenuItem("Tools/DunGen/Preview Canvas")]
        public static void ShowWindow()
        {
            var window = GetWindow<DunGenPreviewCanvas>("DunGen Preview");
            window.minSize = new Vector2(1200, 700);
        }

        private void OnEnable()
        {
            _inspector = new PreviewInspector(_styleProvider);
            _previewController = new PreviewModeController(nodeRadius);

            // Generate initial dungeon if none exists
            if (generatedCycle == null)
                GenerateWithNewSeed();
            else
                _previewController.SetCycle(generatedCycle);
        }

        private void OnDisable()
        {
            _nodePositions.Clear();
            _flatGraph = null;
            _styleProvider.Clear();
        }

        // =========================================================
        // MAIN GUI
        // =========================================================
        private void OnGUI()
        {
            DrawToolbar();

            Rect canvasRect = new Rect(
                0,
                EditorStyles.toolbar.fixedHeight,
                position.width - 260f,
                position.height - EditorStyles.toolbar.fixedHeight
            );

            Rect inspectorRect = new Rect(
                position.width - 260f,
                EditorStyles.toolbar.fixedHeight,
                260f,
                position.height - EditorStyles.toolbar.fixedHeight
            );

            DrawCanvas(canvasRect);
            DrawInspector(inspectorRect);

            Repaint();
        }

        // =========================================================
        // TOOLBAR
        // =========================================================
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("??? PREVIEW MODE", EditorStyles.boldLabel, GUILayout.Width(150));

                GUILayout.Space(10);

                // Generation settings
                generationSettings = (DungeonGenerationSettings)EditorGUILayout.ObjectField(
                    generationSettings, typeof(DungeonGenerationSettings), false, GUILayout.Width(200));

                if (GUILayout.Button("Generate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GenerateProcedural();
                }

                if (generatedCycle != null)
                {
                    if (GUILayout.Button("??", EditorStyles.toolbarButton, GUILayout.Width(30)))
                    {
                        GenerateWithNewSeed();
                    }

                    GUILayout.Label($"Seed: {currentSeed}", GUILayout.Width(80));
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Load Template", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    LoadTemplateForPreview();
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Center", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    CenterView();

                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    FitView();

                if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ResetView();
            }
        }

        // =========================================================
        // CANVAS
        // =========================================================
        private void DrawCanvas(Rect canvasRect)
        {
            // Draw background
            _renderer.DrawBackground(canvasRect);

            // Handle input
            HandleCanvasInput(canvasRect);

            // Update graph data
            if (generatedCycle != null)
            {
                // Preview mode: EXPAND rewrite sites to show the generated dungeon structure
                // (Author mode shows placeholders, Preview shows expanded result)
                _flatGraph = GraphRewriter.RewriteToFlatGraph(generatedCycle);

                // Build node depth map
                _styleProvider.BuildDepthMap(generatedCycle);

                // CRITICAL: Compute layout for the EXPANDED graph, not the original cycle!
                // The flat graph has new nodes from rewrite expansion
                if (_flatGraph != null && _flatGraph.nodes != null && _flatGraph.nodes.Count > 0)
                {
                    // Create a temporary cycle from the flat graph for layout computation
                    var flatCycle = new DungeonCycle();
                    flatCycle.nodes.Clear();
                    flatCycle.edges.Clear();

                    foreach (var node in _flatGraph.nodes)
                        flatCycle.nodes.Add(node);
                    foreach (var edge in _flatGraph.edges)
                        flatCycle.edges.Add(edge);

                    // Find start/goal in flat graph
                    flatCycle.startNode = _flatGraph.nodes.Find(n => n != null && n.HasRole(NodeRoleType.Start));
                    flatCycle.goalNode = _flatGraph.nodes.Find(n => n != null && n.HasRole(NodeRoleType.Goal));

                    // Compute layout for expanded graph
                    _nodePositions = GraphLayoutEngine.ComputeLayout(flatCycle, nodeRadius);
                }
            }

            // Draw grid
            _renderer.DrawGrid(canvasRect, _camera);

            // Draw graph
            if (_flatGraph != null && _nodePositions.Count > 0)
            {
                _renderer.DrawEdges(_flatGraph, _nodePositions, canvasRect, _camera, nodeRadius);
                _renderer.DrawNodes(_flatGraph, _nodePositions, canvasRect, _camera, _styleProvider,
                    generatedCycle, nodeRadius, _previewController.SelectedNode);
            }

            // Preview mode has no overlays
            _previewController.DrawOverlays(canvasRect, _camera);
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!canvasRect.Contains(mousePos))
                return;

            // SHARED: Camera controls
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _camera.Pan(e.delta);
                e.Use();
                return;
            }

            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 0.05f;
                _camera.ZoomToward(mousePos, canvasRect, zoomDelta);
                e.Use();
                return;
            }

            // Preview-specific input handling (read-only selection)
            _previewController.HandleInput(e, mousePos, canvasRect, _camera);
        }

        // =========================================================
        // INSPECTOR
        // =========================================================
        private void DrawInspector(Rect inspectorRect)
        {
            _inspector.DrawInspector(
                inspectorRect,
                _flatGraph,
                _previewController.SelectedNode,
                generatedCycle
            );
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        private void GenerateProcedural()
        {
            if (generationSettings == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Please assign a DungeonGenerationSettings asset", "OK");
                return;
            }

            // Generate new seed
            currentSeed = UnityEngine.Random.Range(0, 10000);

            // Use the procedural generator
            var generator = new ProceduralDungeonGenerator(generationSettings);
            generatedCycle = generator.Generate(currentSeed);

            if (generatedCycle == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Check console for errors", "OK");
                return;
            }

            // Set in preview controller
            _previewController.SetSeed(currentSeed);
            _previewController.SetCycle(generatedCycle);

            ResetView();
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    FitView();
                    Repaint();
                }
            };
        }

        private void GenerateWithNewSeed()
        {
            if (generatedCycle == null)
                return;

            currentSeed = Random.Range(0, 10000);
            _previewController.SetSeed(currentSeed);
            _previewController.RegenerateLayout();

            FitView();
        }

        private void LoadTemplateForPreview()
        {
            string path = EditorUtility.OpenFilePanel(
                "Load Cycle Template",
                "Assets",
                "asset"
            );

            if (string.IsNullOrEmpty(path))
                return;

            // Convert absolute path to relative asset path
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            // Load the template asset
            var template = AssetDatabase.LoadAssetAtPath<CycleTemplate>(path);

            if (template == null)
            {
                EditorUtility.DisplayDialog("Load Failed", "Could not load template asset", "OK");
                return;
            }

            // Validate template
            if (!template.IsValid(out string errorMessage))
            {
                EditorUtility.DisplayDialog("Load Failed", $"Invalid template:\n{errorMessage}", "OK");
                return;
            }

            // Load cycle for preview
            generatedCycle = template.cycle;

            currentSeed = Random.Range(0, 10000);
            _previewController.SetSeed(currentSeed);
            _previewController.SetCycle(generatedCycle);

            ResetView();

            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    FitView();
                    Repaint();
                }
            };
        }

        // =========================================================
        // CAMERA CONTROLS
        // =========================================================

        private void ResetView()
        {
            _camera.Reset();
        }

        private void CenterView()
        {
            if (_nodePositions.Count == 0)
                return;

            Vector2 centroid = CameraController.CalculateCentroid(_nodePositions);
            _camera.CenterOn(centroid);
        }

        private void FitView()
        {
            if (_nodePositions.Count == 0)
                return;

            Rect bounds = CameraController.CalculateWorldBounds(_nodePositions, nodeRadius);
            Vector2 canvasSize = new Vector2(
                position.width - 260f,
                position.height - EditorStyles.toolbar.fixedHeight
            );

            float padding = nodeRadius * 2f + 30f;
            _camera.FitToBounds(bounds, canvasSize, padding);
        }

        // =========================================================
        // HELPERS
        // =========================================================

    }
}