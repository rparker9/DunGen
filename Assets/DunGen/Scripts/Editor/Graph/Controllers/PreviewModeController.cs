using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    
    public sealed class PreviewModeController : IModeController
    {
        private FlatGraph _flatGraph;
        private Dictionary<GraphNode, Vector2> _autoPositions = new Dictionary<GraphNode, Vector2>();
        private PreviewLayoutEngine.Result _layout;
        private float _nodeRadius;
        private GraphNode _selectedNode;
        private int _currentSeed;

        public GraphNode SelectedNode => _selectedNode;
        public int CurrentSeed => _currentSeed;
        public PreviewLayoutEngine.Result Layout => _layout;

        public PreviewModeController(float nodeRadius)
        {
            _nodeRadius = nodeRadius;
        }

        public void SetGraph(FlatGraph graph)
        {
            _flatGraph = graph;
            RegenerateLayout();
        }

        public void SetSeed(int seed)
        {
            _currentSeed = seed;
        }

        public void RegenerateLayout()
        {
            if (_flatGraph == null)
            {
                _autoPositions.Clear();
                _layout = null;
                return;
            }

            // Compute the layout
            _layout = PreviewLayoutEngine.Compute(_flatGraph);

            // Store the computed positions
            _autoPositions = _layout != null
                ? _layout.positions
                : new Dictionary<GraphNode, Vector2>();
        }

        // =========================================================
        // IModeController Implementation
        // =========================================================

        public void HandleInput(Event e, Vector2 mousePos, Rect canvasRect, CameraController camera)
        {
            // Left mouse button - Select node (read-only)
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _selectedNode = HitTestNode(mousePos, canvasRect, camera);
                e.Use();
            }
        }

        public Dictionary<GraphNode, Vector2> GetNodePositions()
        {
            return _autoPositions;
        }

        public void OnModeEnter()
        {
            // Ensure we have positions when entering preview mode
            if (_flatGraph != null && _autoPositions.Count == 0)
                RegenerateLayout();
        }

        public void OnModeExit()
        {
            // Nothing to clean up
        }

        public void DrawOverlays(Rect canvasRect, CameraController camera)
        {
            // Preview mode has no overlays currently
        }

        public void Update()
        {
            // No per-frame updates needed in preview mode
        }

        // =========================================================
        // Helper Methods
        // =========================================================

        private GraphNode HitTestNode(Vector2 screenPos, Rect canvasRect, CameraController camera)
        {
            float clickRadius = _nodeRadius * 1.2f;

            foreach (var kvp in _autoPositions)
            {
                if (kvp.Key == null) continue;

                if (CoordinateConverter.IsPointInCircle(
                    screenPos,
                    kvp.Value,
                    clickRadius,
                    canvasRect,
                    camera.Center,
                    camera.Zoom))
                {
                    return kvp.Key;
                }
            }

            return null;
        }
    }
}