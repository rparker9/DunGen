using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Preview canvas: Generate and view expanded cycles.
    /// Uses CycleFlattener to collect nested nodes/edges for rendering,
    /// but uses GraphLayoutEngine.ComputeLayout(rootCycle) for positions.
    /// </summary>
    public sealed class DunGenPreviewCanvas : EditorWindow
    {
        // =========================================================
        // DATA
        // =========================================================
        [System.NonSerialized] private DungeonCycle generatedCycle;
        [SerializeField] private DungeonGenerationSettings generationSettings;
        [SerializeField] private int currentSeed;

        // Cached data for rendering
        private FlatGraph _flatGraph;

        // Layout results
        private readonly Dictionary<CycleNode, Vector2> _nodePositions = new();
        private List<GraphLayoutEngine.CycleVisualBounds> _cycleBounds = new();

        // Render + styling
        private GraphRenderer _renderer;
        private NodeStyleProvider _styleProvider;

        // Camera
        private CameraController _camera;

        // Mode controller + inspector (preview-only)
        private PreviewModeController _previewController;
        private PreviewInspector _inspector;

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
            _renderer = new GraphRenderer();
            _styleProvider = new NodeStyleProvider();

            _previewController = new PreviewModeController(NodeStyleProvider.NodeSize);
            _inspector = new PreviewInspector(_styleProvider);

            if (generatedCycle != null)
                RefreshCycleDisplay();
        }

        private void OnGUI()
        {
            DrawToolbar();

            // Layout: left canvas, right inspector
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

            if (_flatGraph != null && _nodePositions.Count > 0)
            {
                _renderer.DrawEdges(
                    _flatGraph, 
                    _nodePositions, 
                    canvasRect, 
                    _camera, 
                    _styleProvider);
                _renderer.DrawNodes(
                    _flatGraph,
                    _nodePositions,
                    canvasRect,
                    _camera,
                    _styleProvider,
                    generatedCycle,
                    _previewController.SelectedNode);
            }
            else if (generatedCycle != null)
            {
                var helpStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };

                var helpRect = new Rect(canvasRect.center.x - 170, canvasRect.center.y - 45, 340, 90);
                string debugInfo =
                    $"Original Cycle: {generatedCycle.nodes?.Count ?? 0} nodes\n" +
                    $"Flat Graph: {_flatGraph?.nodes?.Count ?? 0} nodes, {_flatGraph?.edges?.Count ?? 0} edges\n" +
                    $"Positions: {_nodePositions.Count}\n\n" +
                    "Check console for detailed logs";

                GUI.Box(helpRect, debugInfo, helpStyle);
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

            var generator = new ProceduralDungeonGenerator(generationSettings);
            generatedCycle = generator.Generate(currentSeed);

            if (generatedCycle == null)
            {
                EditorUtility.DisplayDialog("Generation Failed", "Generator returned null. Check console for errors.", "OK");
                return;
            }

            RefreshCycleDisplay();
            ResetView();

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                FitView();
                Repaint();
            };
        }

        private void RefreshCycleDisplay()
        {
            if (generatedCycle == null)
            {
                _flatGraph = null;
                _nodePositions.Clear();
                _cycleBounds.Clear();
                return;
            }

            Debug.Log($"[Preview] ===== REFRESH CYCLE DISPLAY =====");
            Debug.Log($"[Preview] Original cycle: {generatedCycle.nodes?.Count ?? 0} nodes, {generatedCycle.rewriteSites?.Count ?? 0} rewrite sites");

            // Flatten for rendering edges (GraphRenderer consumes FlatGraph)
            _flatGraph = CycleFlattener.FlattenNestedCycle(generatedCycle);

            if (_flatGraph == null || _flatGraph.nodes == null || _flatGraph.nodes.Count == 0)
            {
                Debug.LogWarning("[Preview] Flatten returned empty graph");
                _nodePositions.Clear();
                _cycleBounds.Clear();
                return;
            }

            Debug.Log($"[Preview] Flat graph: {_flatGraph.nodes.Count} nodes, {_flatGraph.edges.Count} edges");

            // Compute positions from hierarchical cycle layout
            _nodePositions.Clear();
            _cycleBounds = new List<GraphLayoutEngine.CycleVisualBounds>();

            var positions = GraphLayoutEngine.ComputeLayout(generatedCycle, NodeStyleProvider.NodeSize);

            // If layout returns nothing (e.g., missing replacementPattern links), fallback to circle
            if (positions == null || positions.Count == 0)
            {
                Debug.LogWarning("[Preview] Hierarchical ComputeLayout returned 0 positions; using circle fallback.");
                positions = ComputeCircleFallback(_flatGraph, NodeStyleProvider.NodeSize);
            }

            // Make sure every flattened node has a position; if not, place missing ones on an outer ring.
            foreach (var kv in positions)
                _nodePositions[kv.Key] = kv.Value;

            EnsureAllFlatNodesHavePositions(_flatGraph, _nodePositions, NodeStyleProvider.NodeSize);

            Debug.Log($"[Preview] Computed {_nodePositions.Count} positions");
        }

        private static Dictionary<CycleNode, Vector2> ComputeCircleFallback(FlatGraph graph, float radius)
        {
            var result = new Dictionary<CycleNode, Vector2>();
            if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
                return result;

            int count = graph.nodes.Count;
            float r = Mathf.Max(250f, count * radius * 0.9f);

            for (int i = 0; i < count; i++)
            {
                var n = graph.nodes[i];
                if (n == null) continue;

                float t = (count <= 1) ? 0f : i / (float)count;
                float ang = t * Mathf.PI * 2f;
                result[n] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }

            return result;
        }

        private static void EnsureAllFlatNodesHavePositions(FlatGraph graph, Dictionary<CycleNode, Vector2> positions, float nodeRadius)
        {
            if (graph == null || graph.nodes == null || positions == null)
                return;

            // Find a sensible center
            Vector2 center = Vector2.zero;
            int c = 0;
            foreach (var kv in positions)
            {
                center += kv.Value;
                c++;
            }
            if (c > 0) center /= c;

            // Find max radius already used
            float maxR = 0f;
            foreach (var kv in positions)
            {
                float r = (kv.Value - center).magnitude;
                if (r > maxR) maxR = r;
            }

            // Place missing nodes on an outer ring
            var missing = new List<CycleNode>();
            foreach (var n in graph.nodes)
            {
                if (n == null) continue;
                if (!positions.ContainsKey(n))
                    missing.Add(n);
            }

            if (missing.Count == 0)
                return;

            float ringR = Mathf.Max(maxR + nodeRadius * 6f, 300f);
            for (int i = 0; i < missing.Count; i++)
            {
                float t = (missing.Count <= 1) ? 0f : i / (float)missing.Count;
                float ang = t * Mathf.PI * 2f;
                positions[missing[i]] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringR;
            }
        }

        private void FitView()
        {
            if (_nodePositions.Count == 0)
                return;

            var bounds = CameraController.CalculateWorldBounds(_nodePositions, NodeStyleProvider.NodeSize);
            var canvasSize = new Vector2(position.width - 320f, position.height - 36f);
            _camera.FitToBounds(bounds, canvasSize, padding: NodeStyleProvider.NodeSize * 2f);
        }

        private void ResetView()
        {
            _camera.Reset();
        }
    }
}
