using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Cached data
        private FlatGraph _templateGraph;

        // These are the positions used for rendering the graph
        // The positions are decided by the author controller
        private Dictionary<GraphNode, Vector2> _nodePositions = new Dictionary<GraphNode, Vector2>();   // This is kept in sync with the author controller

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
            _inspector = new AuthorInspector();
            _authorController = new AuthorModeController(NodeStyleProvider.NodeSize);

            // Create empty template if none exists
            if (currentTemplate == null)
                CreateNewTemplate();
            else
                _authorController.SetCycle(currentTemplate);
        }

        private void OnDisable()
        {
            _nodePositions.Clear();
            _templateGraph = null;
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
                EditorGUILayout.LabelField("AUTHOR MODE", EditorStyles.boldLabel, GUILayout.Width(150));

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
                // Authoring view: draw the template as-authored (no rewrite resolution here).
                _templateGraph = FlatGraph.FromTemplateCycle(currentTemplate);

                // Build node depth map
                _styleProvider.BuildDepthMap(currentTemplate);

                // Get manual positions from author controller
                _nodePositions = _authorController.GetNodePositions();
            }

            // Draw grid
            _renderer.DrawGrid(canvasRect, _camera);

            // Draw graph (edges + nodes)
            if (_templateGraph != null && _nodePositions.Count > 0)
            {
                // FIXED: Pass selected edge for highlighting
                _renderer.DrawEdges(_templateGraph, _nodePositions, canvasRect, _camera, _styleProvider,
                    _authorController.SelectedEdge);
                _renderer.DrawNodes(_templateGraph, _nodePositions, canvasRect, _camera, _styleProvider,
                    currentTemplate, _authorController.SelectedNode);
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
            // Clear existing template if it exists
            if (currentTemplate != null)
            {
                currentTemplate.nodes.Clear();
                currentTemplate.edges.Clear();
                currentTemplate.rewriteSites.Clear();
                currentTemplate.startNode = null;
                currentTemplate.goalNode = null;
            }

            // Clear visual data
            _nodePositions.Clear();
            _templateGraph = null;

            // Create fresh empty cycle
            currentTemplate = new DungeonCycle();

            // Initialize controller with empty cycle
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

            // CRITICAL: Strip replacement patterns BEFORE saving
            StripReplacementPatternsFromCycle(currentTemplate);

            // Get template name from user
            string templateName = EditorUtility.SaveFilePanel(
                "Save Cycle Template",
                TemplateRegistry.TEMPLATES_FOLDER,
                "NewTemplate",
                "json"
            );

            if (string.IsNullOrEmpty(templateName))
                return;

            // Ensure it ends with .dungen.json
            if (!templateName.EndsWith(TemplateRegistry.FILE_EXTENSION))
            {
                templateName = Path.ChangeExtension(templateName, null) + TemplateRegistry.FILE_EXTENSION;
            }

            // Get positions
            var positions = _authorController.GetNodePositions();

            // Save using JSON system (NO circular dependency!)
            bool success = CycleTemplate.Save(
                templateName,
                currentTemplate,
                positions,
                Path.GetFileNameWithoutExtension(templateName),
                "" // description
            );

            if (success)
            {
                // Refresh registry
                TemplateRegistry.Refresh();

                EditorUtility.DisplayDialog(
                    "Save Successful",
                    $"Template saved to:\n{templateName}\n\n" +
                    $"Nodes: {currentTemplate.nodes.Count}\n" +
                    $"Edges: {currentTemplate.edges.Count}\n" +
                    $"Rewrite sites: {currentTemplate.rewriteSites.Count}",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog("Save Failed", "Check console for errors", "OK");
            }
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
                    // Recursively strip nested first
                    if (site.replacementPattern != null)
                    {
                        StripReplacementPatternsFromCycle(site.replacementPattern);
                        site.replacementPattern = null; // Then null it out
                    }
                }
            }
        }

        private void LoadTemplate()
        {
            // Prompt user to select file FIRST (before clearing)
            string filePath = EditorUtility.OpenFilePanel(
                "Load Cycle Template",
                TemplateRegistry.TEMPLATES_FOLDER,
                "json"
            );

            if (string.IsNullOrEmpty(filePath))
                return;

            // Load using JSON system
            var (loadedCycle, loadedPositions, metadata) = CycleTemplate.Load(filePath);

            if (loadedCycle == null)
            {
                EditorUtility.DisplayDialog("Load Failed", "Failed to load template. Check console for errors.", "OK");
                return;
            }

            // NOW clear everything (after successful load)
            if (currentTemplate != null)
            {
                currentTemplate.nodes.Clear();
                currentTemplate.edges.Clear();
                currentTemplate.rewriteSites.Clear();
                currentTemplate.startNode = null;
                currentTemplate.goalNode = null;
            }

            _nodePositions.Clear();
            _templateGraph = null;

            // Set as current template
            currentTemplate = loadedCycle;

            // Initialize controller with the loaded cycle
            _authorController.SetCycle(currentTemplate);

            // Apply loaded positions
            if (loadedPositions != null && loadedPositions.Count > 0)
            {
                var controllerPositions = _authorController.GetNodePositions();

                // Direct copy - keys are the same objects!
                foreach (var kvp in loadedPositions)
                {
                    if (kvp.Key != null && controllerPositions.ContainsKey(kvp.Key))
                    {
                        controllerPositions[kvp.Key] = kvp.Value;
                    }
                }
            }

            ResetView();

            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    FitView();
                    Repaint();
                }
            };

            EditorUtility.DisplayDialog(
                "Load Successful",
                $"Loaded template '{metadata.name}'\n\n" +
                $"Nodes: {currentTemplate.nodes.Count}\n" +
                $"Edges: {currentTemplate.edges.Count}\n" +
                $"Rewrite sites: {currentTemplate.rewriteSites.Count}\n" +
                $"Positions: {loadedPositions?.Count ?? 0}",
                "OK"
            );
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

            Rect bounds = CameraController.CalculateWorldBounds(_nodePositions, NodeStyleProvider.NodeSize);
            Vector2 canvasSize = new Vector2(
                position.width - 260f,
                position.height - EditorStyles.toolbar.fixedHeight
            );

            float padding = NodeStyleProvider.NodeSize * 2f + 30f;
            _camera.FitToBounds(bounds, canvasSize, padding);
        }
    }
}