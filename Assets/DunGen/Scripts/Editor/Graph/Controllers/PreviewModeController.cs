using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Preview mode controller: Generate and view expanded cycles.
    /// Uses hierarchical preview layout for generated dungeons.
    /// </summary>
    public sealed class PreviewModeController : IModeController
    {
        private DungeonCycle _cycle;
        private Dictionary<GraphNode, Vector2> _autoPositions = new Dictionary<GraphNode, Vector2>();
        private PreviewLayoutEngine.LayoutResult _hierarchicalLayout;
        private float _nodeRadius;
        private GraphNode _selectedNode;
        private int _currentSeed;

        public GraphNode SelectedNode => _selectedNode;
        public int CurrentSeed => _currentSeed;
        public PreviewLayoutEngine.LayoutResult HierarchicalLayout => _hierarchicalLayout;

        public PreviewModeController(float nodeRadius)
        {
            _nodeRadius = nodeRadius;
        }

        public void SetCycle(DungeonCycle cycle)
        {
            _cycle = cycle;
            RegenerateLayout();
        }

        public void SetSeed(int seed)
        {
            _currentSeed = seed;
        }

        public void RegenerateLayout()
        {
            if (_cycle == null)
            {
                _autoPositions.Clear();
                _hierarchicalLayout = null;
                return;
            }

            // Use the preview layout engine for hierarchical layout
            _hierarchicalLayout = PreviewLayoutEngine.ComputeLayout(_cycle);

            // Extract flat positions for backward compatibility
            _autoPositions = _hierarchicalLayout != null
                ? _hierarchicalLayout.allPositions
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
            if (_cycle != null && _autoPositions.Count == 0)
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