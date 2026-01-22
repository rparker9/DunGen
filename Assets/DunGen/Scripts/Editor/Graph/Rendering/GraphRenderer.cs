using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Handles all rendering for the graph editor: grid, nodes, edges, labels.
    /// UPDATED: Locks on edges, keys on nodes
    /// </summary>
    public sealed class GraphRenderer
    {
        // Visual constants
        private static readonly Color BackgroundColor = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color GridMinorColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color GridMajorColor = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color EdgeNormalColor = new Color(0.80f, 0.80f, 0.80f, 0.90f);
        private static readonly Color EdgeBlockedColor = new Color(0.80f, 0.35f, 0.35f, 0.65f);
        private static readonly Color EdgeSightlineColor = new Color(0.70f, 0.90f, 0.95f, 0.25f);
        private static readonly Color EdgeLockedColor = new Color(0.95f, 0.65f, 0.25f, 0.85f); // Locked edges
        private static readonly Color KeyColor = new Color(1.0f, 0.85f, 0.3f); // Key indicator on nodes
        private static readonly Color LockColor = new Color(0.95f, 0.4f, 0.2f); // Lock indicator on edges
        private static readonly Color SelectionColor = Color.cyan;

        // =========================================================
        // BACKGROUND & GRID
        // =========================================================

        public void DrawBackground(Rect canvasRect)
        {
            EditorGUI.DrawRect(canvasRect, BackgroundColor);
        }

        public void DrawGrid(Rect canvasRect, CameraController camera)
        {
            float gridMinorSpacing = 50f;
            float gridMajorSpacing = 200f;

            Handles.BeginGUI();

            // Calculate visible world bounds
            Vector2 topLeft = camera.ScreenToWorld(Vector2.zero, canvasRect);
            Vector2 bottomRight = camera.ScreenToWorld(new Vector2(canvasRect.width, canvasRect.height), canvasRect);

            // Ensure proper ordering
            float minX = Mathf.Min(topLeft.x, bottomRight.x);
            float maxX = Mathf.Max(topLeft.x, bottomRight.x);
            float minY = Mathf.Min(topLeft.y, bottomRight.y);
            float maxY = Mathf.Max(topLeft.y, bottomRight.y);

            // Draw minor grid
            DrawGridLines(canvasRect, camera, minX, maxX, minY, maxY, gridMinorSpacing, GridMinorColor);

            // Draw major grid
            DrawGridLines(canvasRect, camera, minX, maxX, minY, maxY, gridMajorSpacing, GridMajorColor);

            Handles.EndGUI();
        }

        private void DrawGridLines(
            Rect canvasRect,
            CameraController camera,
            float minX, float maxX, float minY, float maxY,
            float spacing,
            Color color)
        {
            Handles.color = color;

            // Vertical lines
            float startX = Mathf.Floor(minX / spacing) * spacing;
            for (float x = startX; x <= maxX; x += spacing)
            {
                Vector2 screenStart = camera.WorldToScreen(new Vector2(x, minY), canvasRect);
                Vector2 screenEnd = camera.WorldToScreen(new Vector2(x, maxY), canvasRect);
                Handles.DrawLine(screenStart, screenEnd);
            }

            // Horizontal lines
            float startY = Mathf.Floor(minY / spacing) * spacing;
            for (float y = startY; y <= maxY; y += spacing)
            {
                Vector2 screenStart = camera.WorldToScreen(new Vector2(minX, y), canvasRect);
                Vector2 screenEnd = camera.WorldToScreen(new Vector2(maxX, y), canvasRect);
                Handles.DrawLine(screenStart, screenEnd);
            }
        }

        // =========================================================
        // EDGES
        // =========================================================

        public void DrawEdges(
            RewrittenGraph graph,
            Dictionary<CycleNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            float nodeRadius,
            CycleEdge selectedEdge = null)
        {
            if (graph == null || graph.edges == null)
                return;

            Handles.BeginGUI();

            foreach (var edge in graph.edges)
            {
                if (edge == null || edge.from == null || edge.to == null)
                    continue;

                if (!positions.TryGetValue(edge.from, out Vector2 fromPos))
                    continue;
                if (!positions.TryGetValue(edge.to, out Vector2 toPos))
                    continue;

                bool isSelected = edge == selectedEdge;
                DrawEdge(edge, fromPos, toPos, canvasRect, camera, nodeRadius, isSelected);
            }

            Handles.EndGUI();
        }

        private void DrawEdge(
            CycleEdge edge,
            Vector2 fromWorld,
            Vector2 toWorld,
            Rect canvasRect,
            CameraController camera,
            float nodeRadius,
            bool isSelected = false)
        {
            Vector2 screenFrom = camera.WorldToScreen(fromWorld, canvasRect);
            Vector2 screenTo = camera.WorldToScreen(toWorld, canvasRect);

            Vector2 v = screenTo - screenFrom;
            float len = v.magnitude;
            if (len < 0.001f) return;

            Vector2 dir = v / len;

            float nodeRpx = nodeRadius * camera.Zoom;
            float pad = 2f;
            float trim = nodeRpx + pad;

            // Arrow sizing
            float arrowDepth = 14f * camera.Zoom * 0.5f;
            float arrowWidth = 7f * camera.Zoom * 0.5f;

            // Calculate usable line length
            float usable = len - trim * 2f;

            Vector2 lineStart, lineEnd;
            if (usable <= 0f)
            {
                Vector2 mid = (screenFrom + screenTo) * 0.5f;
                Vector2 half = dir * Mathf.Max(1f, len * 0.25f);
                lineStart = mid - half;
                lineEnd = mid + half;
            }
            else
            {
                lineStart = screenFrom + dir * trim;
                lineEnd = screenTo - dir * trim;
            }

            // Choose color based on edge properties
            Color edgeColor = EdgeNormalColor;
            if (edge.isBlocked)
                edgeColor = EdgeBlockedColor;
            else if (edge.hasSightline)
                edgeColor = EdgeSightlineColor;
            else if (edge.RequiresAnyKey())
                edgeColor = EdgeLockedColor; // Locked edges

            // Highlight selected edge
            if (isSelected)
            {
                edgeColor = SelectionColor;
            }

            Handles.color = edgeColor;

            // Draw line (thicker if selected)
            float lineWidth = isSelected
                ? Mathf.Clamp(5f * camera.Zoom, 3f, 8f)
                : Mathf.Clamp(3f * camera.Zoom, 2f, 6f);
            Handles.DrawAAPolyLine(lineWidth, lineStart, lineEnd);

            // Draw arrows
            Vector2 tipAtB = screenTo - dir * trim;
            Vector2 tipAtA = screenFrom + dir * trim;

            float tipInset = Mathf.Clamp(arrowDepth * 0.35f, 0f, trim * 0.75f);

            Vector2 tipB = tipAtB + dir * tipInset;
            Vector2 tipA = tipAtA - dir * tipInset;

            DrawArrowHead(tipB, dir, arrowDepth, arrowWidth);

            if (edge.bidirectional)
                DrawArrowHead(tipA, -dir, arrowDepth, arrowWidth);

            // Draw lock icon on edge if it requires keys
            DrawEdgeLockIcon(edge, lineStart, lineEnd, camera);
        }

        private void DrawArrowHead(Vector2 tip, Vector2 dir, float depth, float width)
        {
            Vector2 d = dir.normalized;
            Vector2 right = new Vector2(-d.y, d.x) * width;

            Vector3[] tri =
            {
                tip,
                tip - d * depth + right,
                tip - d * depth - right
            };

            Handles.DrawAAConvexPolygon(tri);
        }

        // Draw lock icon on edge midpoint
        private void DrawEdgeLockIcon(CycleEdge edge, Vector2 lineStart, Vector2 lineEnd, CameraController camera)
        {
            if (!edge.RequiresAnyKey())
                return;

            Vector2 midpoint = (lineStart + lineEnd) * 0.5f;
            float iconSize = Mathf.Clamp(10f * camera.Zoom, 6f, 14f);

            Handles.color = LockColor;
            Handles.DrawSolidDisc(midpoint, Vector3.forward, iconSize);

            // Draw keyhole
            Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Handles.DrawSolidDisc(midpoint, Vector3.forward, iconSize * 0.4f);

            // Draw required key IDs label
            if (edge.requiredKeys != null && edge.requiredKeys.Count > 0)
            {
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8
                };
                labelStyle.normal.textColor = Color.white;

                string keyText = edge.requiredKeys.Count == 1
                    ? edge.requiredKeys[0].ToString()
                    : $"{edge.requiredKeys.Count}K";

                var labelRect = new Rect(midpoint.x - 15f, midpoint.y - 20f, 30f, 15f);
                GUI.Label(labelRect, keyText, labelStyle);
            }
        }

        // =========================================================
        // NODES
        // =========================================================

        public void DrawNodes(
            RewrittenGraph graph,
            Dictionary<CycleNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider,
            DungeonCycle rootCycle,
            float nodeRadius,
            CycleNode selectedNode)
        {
            if (graph == null || graph.nodes == null)
                return;

            foreach (var node in graph.nodes)
            {
                if (node == null)
                    continue;

                if (!positions.TryGetValue(node, out Vector2 worldPos))
                    continue;

                DrawNode(node, worldPos, canvasRect, camera, styleProvider, rootCycle, nodeRadius, selectedNode);
            }
        }

        private void DrawNode(
            CycleNode node,
            Vector2 worldPos,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider,
            DungeonCycle rootCycle,
            float nodeRadius,
            CycleNode selectedNode)
        {
            Vector2 screenPos = camera.WorldToScreen(worldPos, canvasRect);
            float screenRadius = nodeRadius * camera.Zoom;

            // Determine node color
            Color nodeColor = styleProvider.GetNodeColor(node, rootCycle);
            bool isSelected = node == selectedNode;

            Handles.BeginGUI();

            // Draw selection highlight
            if (isSelected)
            {
                Handles.color = SelectionColor;
                Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius + 3f);
            }

            // Draw node circle
            Handles.color = nodeColor;
            Handles.DrawSolidDisc(screenPos, Vector3.forward, screenRadius);

            // Draw node outline
            Handles.color = new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius);

            Handles.EndGUI();

            // Draw node label
            string label = styleProvider.GetNodeLabel(node);
            DrawNodeLabel(screenPos, label, screenRadius);

            // NEW: Draw key icon if node grants keys
            DrawNodeKeyIcon(node, screenPos, screenRadius, camera);
        }

        private void DrawNodeLabel(Vector2 screenPos, string text, float radius)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var labelRect = new Rect(
                screenPos.x - radius,
                screenPos.y - 18f,
                radius * 2f,
                36f
            );

            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true
            };
            style.normal.textColor = Color.black;

            GUI.Label(labelRect, text, style);
        }

        // Draw key icons along the edge of the node circle, spaced clockwise
        private void DrawNodeKeyIcon(CycleNode node, Vector2 screenPos, float screenRadius, CameraController camera)
        {
            if (node == null || !node.GrantsAnyKey())
                return;

            float iconSize = Mathf.Clamp(8f * camera.Zoom, 5f, 12f);

            // Calculate spacing for up to 8 keys around the circle
            int maxKeys = 8;
            float arcPerKey = (Mathf.PI * 2f) / maxKeys; // 45 degrees per key slot

            // Start at top (90 degrees / PI/2) and go clockwise
            float startAngle = Mathf.PI * -0.5f;

            Handles.BeginGUI();

            // Draw each key
            for (int i = 0; i < node.grantedKeys.Count && i < maxKeys; i++)
            {
                int keyId = node.grantedKeys[i];

                // Calculate position going clockwise (negative angle increment)
                float angle = startAngle - (i * arcPerKey);

                // Position on circle edge (slightly outside the node circle)
                float keyRadius = screenRadius + iconSize * 0.5f;
                Vector2 keyPos = screenPos + new Vector2(
                    Mathf.Cos(angle) * keyRadius,
                    Mathf.Sin(angle) * keyRadius
                );

                // Draw key body
                Handles.color = KeyColor;
                Handles.DrawSolidDisc(keyPos, Vector3.forward, iconSize);

                // Draw key teeth pointing outward from circle
                Vector2 teethDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 teethOffset = teethDir * iconSize * 0.6f;
                Handles.DrawAAPolyLine(2f, keyPos, keyPos + teethOffset);

                Handles.EndGUI();

                // Draw key ID label
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 7,
                    fontStyle = FontStyle.Bold
                };
                labelStyle.normal.textColor = Color.black;

                string keyText = $"K{keyId}";

                // Draw label at center of key icon
                var labelRect = new Rect(
                    keyPos.x - iconSize * 0.6f,
                    keyPos.y - iconSize * 0.6f,
                    iconSize * 1.2f,
                    iconSize * 1.2f
                );

                GUI.Label(labelRect, keyText, labelStyle);

                Handles.BeginGUI();
            }

            Handles.EndGUI();
        }

        // =========================================================
        // HIT TESTING
        // =========================================================

        public CycleNode HitTestNode(
            Vector2 screenPos,
            Dictionary<CycleNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            float nodeRadius)
        {
            float clickRadius = nodeRadius * 1.2f;

            CycleNode hit = null;

            foreach (var kvp in positions)
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
                    hit = kvp.Key;
                }
            }

            return hit;
        }

        /// <summary>
        /// Test if a screen position is near an edge (within tolerance).
        /// Returns the closest edge if within range, null otherwise.
        /// </summary>
        public CycleEdge HitTestEdge(
            Vector2 screenPos,
            List<CycleEdge> edges,
            Dictionary<CycleNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            float tolerance = 5f)
        {
            if (edges == null || positions == null)
                return null;

            CycleEdge closestEdge = null;
            float closestDist = tolerance;

            foreach (var edge in edges)
            {
                if (edge == null || edge.from == null || edge.to == null)
                    continue;

                if (!positions.TryGetValue(edge.from, out Vector2 fromWorld))
                    continue;
                if (!positions.TryGetValue(edge.to, out Vector2 toWorld))
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

        /// <summary>
        /// Calculate shortest distance from point to line segment
        /// </summary>
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