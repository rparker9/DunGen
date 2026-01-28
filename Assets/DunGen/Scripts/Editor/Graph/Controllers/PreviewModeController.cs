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
        private GraphEdge _selectedEdge;
        private int _currentSeed;

        public GraphNode SelectedNode => _selectedNode; 
        public GraphEdge SelectedEdge => _selectedEdge;
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
            // Left mouse button - Select node or edge (read-only)
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Try node first
                var hitNode = HitTestNode(mousePos, canvasRect, camera);
                if (hitNode != null)
                {
                    _selectedNode = hitNode;
                    _selectedEdge = null; // Clear edge selection
                    e.Use();
                    return;
                }

                // Try edge
                var hitEdge = HitTestEdge(mousePos, canvasRect, camera);
                if (hitEdge != null)
                {
                    _selectedEdge = hitEdge;
                    _selectedNode = null; // Clear node selection
                    e.Use();
                    return;
                }

                // Clicked empty space - clear all selections
                _selectedNode = null;
                _selectedEdge = null;
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

        private GraphEdge HitTestEdge(Vector2 screenPos, Rect canvasRect, CameraController camera)
        {
            if (_flatGraph == null || _flatGraph.edges == null)
                return null;

            float tolerance = 8f; // Pixels
            GraphEdge closestEdge = null;
            float closestDist = tolerance;

            foreach (var edge in _flatGraph.edges)
            {
                if (edge == null || edge.from == null || edge.to == null)
                    continue;

                if (!_autoPositions.TryGetValue(edge.from, out Vector2 fromWorld))
                    continue;
                if (!_autoPositions.TryGetValue(edge.to, out Vector2 toWorld))
                    continue;

                // Convert to screen space
                Vector2 fromScreen = camera.WorldToScreen(fromWorld, canvasRect);
                Vector2 toScreen = camera.WorldToScreen(toWorld, canvasRect);

                // Calculate distance from point to line segment
                float dist = DistanceToLineSegment(screenPos, fromScreen, toScreen);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEdge = edge;
                }
            }

            return closestEdge;
        }

        private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float lineLength = line.magnitude;

            if (lineLength < 0.001f)
                return Vector2.Distance(point, lineStart);

            // Project point onto line
            float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, line) / (lineLength * lineLength));
            Vector2 projection = lineStart + t * line;

            return Vector2.Distance(point, projection);
        }
    }
}