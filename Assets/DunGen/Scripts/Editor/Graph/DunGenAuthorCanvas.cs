using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Author canvas: Create and edit cycle templates manually.
    /// Standalone window for interactive template authoring.
    /// </summary>
    public sealed class DunGenAuthorCanvas : EditorWindow
    {
        // =========================================================
        // DATA
        // =========================================================
        [System.NonSerialized] private DungeonCycle currentTemplate;
        [SerializeField] private float nodeRadius = 25.0f;

        // Cached data
        private FlatGraph _flatGraph;
        private Dictionary<CycleNode, Vector2> _nodePositions = new Dictionary<CycleNode, Vector2>();

        // =========================================================
        // SHARED COMPONENTS
        // =========================================================
        private CameraController _camera = new CameraController();
        private GraphRenderer _renderer = new GraphRenderer();
        private NodeStyleProvider _styleProvider = new NodeStyleProvider();
        private AuthorInspector _inspector;

        // =========================================================
        // AUTHOR MODE CONTROLLER
        // =========================================================
        private AuthorModeController _authorController;

        // =========================================================
        // WINDOW SETUP
        // =========================================================
        [MenuItem("Tools/DunGen/Author Canvas")]
        public static void ShowWindow()
        {
            var window = GetWindow<DunGenAuthorCanvas>("DunGen Author");
            window.minSize = new Vector2(1200, 700);
        }

        private void OnEnable()
        {
            _inspector = new AuthorInspector(_styleProvider);
            _authorController = new AuthorModeController(nodeRadius);

            // Create empty template if none exists
            if (currentTemplate == null)
                CreateNewTemplate();
            else
                _authorController.SetCycle(currentTemplate);
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
                EditorGUILayout.LabelField("?? AUTHOR MODE", EditorStyles.boldLabel, GUILayout.Width(150));

                GUILayout.Space(10);

                if (GUILayout.Button("+ Node", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    _authorController.StartPlacingNode();
                }

                GUILayout.Space(10);

                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    CreateNewTemplate();
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    SaveTemplate();
                }

                if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    LoadTemplate();
                }

                GUILayout.FlexibleSpace();

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
            if (currentTemplate != null)
            {
                // In Author mode, we want to see the ORIGINAL structure (including rewrite sites)
                // NOT the expanded flat graph (which removes placeholders)
                _flatGraph = new FlatGraph(currentTemplate.nodes, currentTemplate.edges);

                // Build node depth map
                _styleProvider.BuildDepthMap(currentTemplate);

                // Get manual positions from author controller
                _nodePositions = _authorController.GetNodePositions();
            }

            // Draw grid
            _renderer.DrawGrid(canvasRect, _camera);

            // Draw graph
            if (_flatGraph != null && _nodePositions.Count > 0)
            {
                _renderer.DrawEdges(_flatGraph, _nodePositions, canvasRect, _camera);
                _renderer.DrawNodes(_flatGraph, _nodePositions, canvasRect, _camera, _styleProvider,
                    currentTemplate, nodeRadius, _authorController.SelectedNode);
            }

            // Draw author overlays (placement preview, connection line, etc.)
            _authorController.DrawOverlays(canvasRect, _camera);
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

            // Author-specific input handling
            _authorController.HandleInput(e, mousePos, canvasRect, _camera);

            // Sync edge selection from controller to inspector
            if (_authorController.SelectedEdge != null)
            {
                _inspector.SetSelectedEdge(_authorController.SelectedEdge);
            }
            else if (_authorController.SelectedNode != null)
            {
                // Node selected, clear edge selection
                _inspector.ClearEdgeSelection();
            }
        }

        // =========================================================
        // INSPECTOR
        // =========================================================
        private void DrawInspector(Rect inspectorRect)
        {
            _inspector.DrawInspector(
                inspectorRect,
                currentTemplate,
                _authorController.SelectedNode,
                () => Repaint() // Callback when template changes
            );
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        private void CreateNewTemplate()
        {
            // Create empty cycle with just start and goal
            currentTemplate = new DungeonCycle();

            _authorController.SetCycle(currentTemplate);
            ResetView();
        }

        private void SaveTemplate()
        {
            if (currentTemplate == null)
            {
                EditorUtility.DisplayDialog("Save Failed", "No template to save", "OK");
                return;
            }

            // Validate template
            if (currentTemplate.nodes == null || currentTemplate.nodes.Count < 2)
            {
                EditorUtility.DisplayDialog("Save Failed", "Template must have at least Start and Goal nodes", "OK");
                return;
            }

            // CRITICAL: Strip replacement patterns from source BEFORE saving
            // (in case this template was used in Preview mode)
            StripReplacementPatternsFromCycle(currentTemplate);

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Cycle Template",
                "CycleTemplate",
                "asset",
                "Save cycle template as asset"
            );

            if (string.IsNullOrEmpty(path))
                return;

            // Create template asset
            var positions = _authorController.GetNodePositions();
            var template = CycleTemplate.CreateFromCycle(currentTemplate, positions);

            // Save as asset
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Save Successful", $"Template saved to:\n{path}", "OK");
        }

        /// <summary>
        /// Recursively strip all replacement patterns from a cycle (in-place)
        /// </summary>
        private void StripReplacementPatternsFromCycle(DungeonCycle cycle)
        {
            if (cycle == null || cycle.rewriteSites == null)
                return;

            foreach (var site in cycle.rewriteSites)
            {
                if (site != null)
                {
                    if (site.replacementPattern != null)
                    {
                        // Recursively strip nested first
                        StripReplacementPatternsFromCycle(site.replacementPattern);
                        // Then null it out
                        site.replacementPattern = null;
                    }
                }
            }
        }

        private void LoadTemplate()
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

            // Load cycle and positions
            currentTemplate = template.cycle;

            UnityEngine.Debug.Log($"[LoadTemplate] Loaded cycle with {currentTemplate.nodes.Count} nodes");

            // Set cycle first (initializes all nodes to zero)
            _authorController.SetCycle(currentTemplate);

            // Restore positions by matching node labels
            var savedPositions = template.GetPositionsDictionary();
            var controllerPositions = _authorController.GetNodePositions();

            UnityEngine.Debug.Log($"[LoadTemplate] Saved positions: {savedPositions.Count}, Controller positions: {controllerPositions.Count}");

            // Match saved positions to current node objects by label
            int matchedCount = 0;
            foreach (var savedEntry in savedPositions)
            {
                foreach (var controllerNode in controllerPositions.Keys)
                {
                    if (controllerNode != null &&
                        savedEntry.Key != null &&
                        controllerNode.label == savedEntry.Key.label)
                    {
                        UnityEngine.Debug.Log($"[LoadTemplate] Matched '{controllerNode.label}' -> {savedEntry.Value}");
                        controllerPositions[controllerNode] = savedEntry.Value;
                        matchedCount++;
                        break;
                    }
                }
            }

            UnityEngine.Debug.Log($"[LoadTemplate] Matched {matchedCount} of {savedPositions.Count} positions");

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
    }
}