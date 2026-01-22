using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Author mode controller: Create and edit cycle templates manually.
    /// Interactive, editable, manual node placement.
    /// </summary>
    public sealed class AuthorModeController : IModeController
    {
        /// <summary>
        /// Current interaction state in author mode.
        /// </summary>
        private enum AuthorState
        {
            Idle,           // Default state, ready for input
            PlacingNode,    // User is placing a new node
            DraggingNode,   // User is dragging an existing node
            ConnectingEdge  // User is creating an edge between nodes
        }

        private DungeonCycle _cycle;
        private Dictionary<CycleNode, Vector2> _manualPositions = new Dictionary<CycleNode, Vector2>();
        private float _nodeRadius;
        private CycleNode _selectedNode;
        private CycleEdge _selectedEdge;

        // Dragging/connecting state
        private CycleNode _draggedNode;
        private CycleNode _connectFromNode;
        private Vector2 _dragStartMousePos;
        private Vector2 _dragStartNodePos;

        // State machine
        private AuthorState _state = AuthorState.Idle;

        public CycleNode SelectedNode => _selectedNode;
        public CycleEdge SelectedEdge => _selectedEdge;

        public AuthorModeController(float nodeRadius)
        {
            _nodeRadius = nodeRadius;
        }

        public void SetCycle(DungeonCycle cycle)
        {
            _cycle = cycle;

            // Clear old positions from previous cycle
            _manualPositions.Clear();

            // Initialize positions for new cycle nodes
            if (_cycle != null && _cycle.nodes != null)
            {
                foreach (var node in _cycle.nodes)
                {
                    if (node != null)
                    {
                        // Place nodes at origin initially (will be overridden by loaded positions)
                        _manualPositions[node] = Vector2.zero;
                    }
                }
            }
        }

        public void StartPlacingNode()
        {
            _state = AuthorState.PlacingNode;
        }

        public void StartConnectingFrom(CycleNode fromNode)
        {
            _connectFromNode = fromNode;
            _state = AuthorState.ConnectingEdge;
        }

        public void DeleteSelectedNode()
        {
            if (_selectedNode == null || _cycle == null)
                return;

            // Don't allow deleting start or goal nodes
            if (_selectedNode.HasRole(NodeRoleType.Start) || _selectedNode.HasRole(NodeRoleType.Goal))
            {
                Debug.LogWarning("Cannot delete Start or Goal nodes");
                return;
            }

            // Remove node from cycle
            _cycle.nodes.Remove(_selectedNode);
            _manualPositions.Remove(_selectedNode);

            // Remove connected edges
            _cycle.edges.RemoveAll(e => e.from == _selectedNode || e.to == _selectedNode);

            // Remove from rewrite sites
            _cycle.rewriteSites.RemoveAll(s => s.placeholder == _selectedNode);

            _selectedNode = null;
        }

        public void DeleteSelectedEdge(CycleEdge edge)
        {
            if (edge == null || _cycle == null)
                return;

            _cycle.edges.Remove(edge);
        }

        // =========================================================
        // IModeController Implementation
        // =========================================================

        public void HandleInput(Event e, Vector2 mousePos, Rect canvasRect, CameraController camera)
        {
            Vector2 worldPos = camera.ScreenToWorld(mousePos, canvasRect);

            switch (_state)
            {
                case AuthorState.PlacingNode:
                    HandlePlacingNodeInput(e, worldPos);
                    break;

                case AuthorState.Idle:
                    HandleIdleInput(e, mousePos, worldPos, canvasRect, camera);
                    break;

                case AuthorState.DraggingNode:
                    HandleDraggingNodeInput(e, worldPos);
                    break;

                case AuthorState.ConnectingEdge:
                    HandleConnectingEdgeInput(e, mousePos, canvasRect, camera);
                    break;
            }
        }

        public Dictionary<CycleNode, Vector2> GetNodePositions()
        {
            return _manualPositions;
        }

        public void OnModeEnter()
        {
            _state = AuthorState.Idle;
        }

        public void OnModeExit()
        {
            _state = AuthorState.Idle;
            _connectFromNode = null;
            _draggedNode = null;
        }

        public void DrawOverlays(Rect canvasRect, CameraController camera)
        {
            // Draw connection line preview when connecting edges
            if (_state == AuthorState.ConnectingEdge && _connectFromNode != null)
            {
                if (_manualPositions.TryGetValue(_connectFromNode, out Vector2 fromWorld))
                {
                    Vector2 fromScreen = camera.WorldToScreen(fromWorld, canvasRect);
                    Vector2 toScreen = Event.current.mousePosition;

                    Handles.BeginGUI();
                    Handles.color = new Color(0.5f, 0.8f, 1f, 0.7f);
                    Handles.DrawAAPolyLine(3f, fromScreen, toScreen);
                    Handles.EndGUI();
                }
            }

            // Draw placement preview when placing node
            if (_state == AuthorState.PlacingNode)
            {
                Vector2 mouseWorld = camera.ScreenToWorld(Event.current.mousePosition, canvasRect);
                Vector2 mouseScreen = camera.WorldToScreen(mouseWorld, canvasRect);
                float screenRadius = _nodeRadius * camera.Zoom;

                Handles.BeginGUI();
                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Handles.DrawSolidDisc(mouseScreen, Vector3.forward, screenRadius);
                Handles.EndGUI();
            }
        }

        public void Update()
        {
            // Handle escape key to cancel current operation
            if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _state = AuthorState.Idle;
                _connectFromNode = null;
                _draggedNode = null;
                Event.current.Use();
            }
        }

        // =========================================================
        // State-Specific Input Handlers
        // =========================================================

        private void HandlePlacingNodeInput(Event e, Vector2 worldPos)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceNode(worldPos);
                _state = AuthorState.Idle;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _state = AuthorState.Idle;
                e.Use();
            }
        }

        private void HandleIdleInput(Event e, Vector2 mousePos, Vector2 worldPos, Rect canvasRect, CameraController camera)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hit = HitTestNode(mousePos, canvasRect, camera);

                if (hit != null)
                {
                    _selectedNode = hit;
                    _selectedEdge = null; // Clear edge selection when node is selected

                    if (e.shift)
                    {
                        // Shift+click = start connecting edge
                        _connectFromNode = hit;
                        _state = AuthorState.ConnectingEdge;
                    }
                    else
                    {
                        // Regular click = start dragging
                        _draggedNode = hit;
                        _dragStartMousePos = mousePos;
                        _dragStartNodePos = _manualPositions[hit];
                        _state = AuthorState.DraggingNode;
                    }
                }
                else
                {
                    // No node hit - check for edge hit
                    var edgeHit = HitTestEdge(mousePos, canvasRect, camera);

                    if (edgeHit != null)
                    {
                        _selectedEdge = edgeHit;
                        _selectedNode = null; // Clear node selection when edge is selected
                    }
                    else
                    {
                        // Clicked empty space - clear all selections
                        _selectedNode = null;
                        _selectedEdge = null;
                    }
                }

                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                if (_selectedNode != null)
                {
                    DeleteSelectedNode();
                    e.Use();
                }
                else if (_selectedEdge != null)
                {
                    DeleteSelectedEdge(_selectedEdge);
                    _selectedEdge = null;
                    e.Use();
                }
            }
        }

        private void HandleDraggingNodeInput(Event e, Vector2 worldPos)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_draggedNode != null)
                {
                    _manualPositions[_draggedNode] = worldPos;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _state = AuthorState.Idle;
                _draggedNode = null;
                e.Use();
            }
        }

        private void HandleConnectingEdgeInput(Event e, Vector2 mousePos, Rect canvasRect, CameraController camera)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var hitTo = HitTestNode(mousePos, canvasRect, camera);

                if (hitTo != null && hitTo != _connectFromNode)
                {
                    CreateEdge(_connectFromNode, hitTo);
                }

                _state = AuthorState.Idle;
                _connectFromNode = null;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _state = AuthorState.Idle;
                _connectFromNode = null;
                e.Use();
            }
        }

        // =========================================================
        // Node/Edge Creation
        // =========================================================

        private void PlaceNode(Vector2 worldPos)
        {
            if (_cycle == null)
                return;

            var newNode = new CycleNode();
            newNode.label = $"Node {_cycle.nodes.Count}";

            _cycle.nodes.Add(newNode);
            _manualPositions[newNode] = worldPos;
            _selectedNode = newNode;
        }

        private void CreateEdge(CycleNode from, CycleNode to)
        {
            if (_cycle == null || from == null || to == null)
                return;

            // Check if edge already exists
            foreach (var edge in _cycle.edges)
            {
                if ((edge.from == from && edge.to == to) ||
                    (edge.bidirectional && edge.from == to && edge.to == from))
                {
                    Debug.LogWarning("Edge already exists");
                    return;
                }
            }

            var newEdge = new CycleEdge(from, to, bidirectional: true);
            _cycle.edges.Add(newEdge);
        }

        // =========================================================
        // Helper Methods
        // =========================================================

        private CycleNode HitTestNode(Vector2 screenPos, Rect canvasRect, CameraController camera)
        {
            float clickRadius = _nodeRadius * 1.2f;

            foreach (var kvp in _manualPositions)
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

        private CycleEdge HitTestEdge(Vector2 screenPos, Rect canvasRect, CameraController camera)
        {
            if (_cycle == null || _cycle.edges == null)
                return null;

            float tolerance = 8f; // Pixels
            CycleEdge closestEdge = null;
            float closestDist = tolerance;

            foreach (var edge in _cycle.edges)
            {
                if (edge == null || edge.from == null || edge.to == null)
                    continue;

                if (!_manualPositions.TryGetValue(edge.from, out Vector2 fromWorld))
                    continue;
                if (!_manualPositions.TryGetValue(edge.to, out Vector2 toWorld))
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