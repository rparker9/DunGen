using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Handles all rendering for the graph editor: grid, nodes, edges, labels.
    /// UPDATED: adds cycle-boundary rings + slot marker rendering.
    /// </summary>
    public sealed class AuthoringGraphRenderer
    {
        private static readonly Color BackgroundColor = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color GridMinorColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color GridMajorColor = new Color(1f, 1f, 1f, 0.10f);

        private static readonly Color EdgeNormalColor = new Color(0.80f, 0.80f, 0.80f, 0.90f);
        private static readonly Color EdgeBlockedColor = new Color(0.80f, 0.35f, 0.35f, 0.65f);
        private static readonly Color EdgeSightlineColor = new Color(0.70f, 0.90f, 0.95f, 0.25f);
        private static readonly Color EdgeLockedColor = new Color(0.95f, 0.65f, 0.25f, 0.85f);

        private static readonly Color KeyColor = new Color(1.0f, 0.85f, 0.3f);
        private static readonly Color LockColor = new Color(0.95f, 0.4f, 0.2f);
        private static readonly Color SelectionColor = Color.cyan;

        // Cycle ring base color
        private static readonly Color CycleRingColor = new Color(1f, 1f, 1f, 0.14f);

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

            Vector2 topLeft = camera.ScreenToWorld(Vector2.zero, canvasRect);
            Vector2 bottomRight = camera.ScreenToWorld(new Vector2(canvasRect.width, canvasRect.height), canvasRect);

            float minX = Mathf.Min(topLeft.x, bottomRight.x);
            float maxX = Mathf.Max(topLeft.x, bottomRight.x);
            float minY = Mathf.Min(topLeft.y, bottomRight.y);
            float maxY = Mathf.Max(topLeft.y, bottomRight.y);

            DrawGridLines(canvasRect, camera, minX, maxX, minY, maxY, gridMinorSpacing, GridMinorColor);
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

            float startX = Mathf.Floor(minX / spacing) * spacing;
            for (float x = startX; x <= maxX; x += spacing)
            {
                Vector2 screenStart = camera.WorldToScreen(new Vector2(x, minY), canvasRect);
                Vector2 screenEnd = camera.WorldToScreen(new Vector2(x, maxY), canvasRect);
                Handles.DrawLine(screenStart, screenEnd);
            }

            float startY = Mathf.Floor(minY / spacing) * spacing;
            for (float y = startY; y <= maxY; y += spacing)
            {
                Vector2 screenStart = camera.WorldToScreen(new Vector2(minX, y), canvasRect);
                Vector2 screenEnd = camera.WorldToScreen(new Vector2(maxX, y), canvasRect);
                Handles.DrawLine(screenStart, screenEnd);
            }
        }

        // =========================================================
        // CYCLE RINGS (hierarchical bounds)
        // =========================================================

        public void DrawCycleBounds(
            List<AuthoringLayoutEngine.CycleVisualBounds> bounds,
            Rect canvasRect,
            CameraController camera)
        {
            if (bounds == null || bounds.Count == 0)
                return;

            Handles.BeginGUI();

            // Draw outer-to-inner so inner rings sit on top a bit.
            for (int i = 0; i < bounds.Count; i++)
            {
                var b = bounds[i];

                Vector2 screenCenter = camera.WorldToScreen(b.Center, canvasRect);
                float screenRadius = b.Radius * camera.Zoom;

                // Fade by depth
                float a = Mathf.Clamp01(CycleRingColor.a * Mathf.Pow(0.75f, b.Depth));
                Handles.color = new Color(CycleRingColor.r, CycleRingColor.g, CycleRingColor.b, a);

                Handles.DrawWireDisc(screenCenter, Vector3.forward, screenRadius);
            }

            Handles.EndGUI();
        }

        // =========================================================
        // EDGES
        // =========================================================

        /// <summary>
        /// Draws all edges in the graph.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="positions"></param>
        /// <param name="canvasRect"></param>
        /// <param name="camera"></param>
        public void DrawEdges(
            FlatGraph graph,
            Dictionary<GraphNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider)
        {
            if (graph == null || graph.edges == null)
                return;

            foreach (var edge in graph.edges)
            {
                if (edge == null || edge.from == null || edge.to == null)
                    continue;

                if (!positions.TryGetValue(edge.from, out Vector2 fromPos))
                    continue;

                if (!positions.TryGetValue(edge.to, out Vector2 toPos))
                    continue;

                DrawEdge(edge, fromPos, toPos, canvasRect, camera, styleProvider);
            }
        }

        /// <summary>
        /// Draws a single edge between two world positions.
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="worldFrom"></param>
        /// <param name="worldTo"></param>
        /// <param name="canvasRect"></param>
        /// <param name="camera"></param>
        private void DrawEdge(
            GraphEdge edge,
            Vector2 worldFrom,
            Vector2 worldTo,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider)
        {
            // Convert to screen space first
            Vector2 screenFrom = camera.WorldToScreen(worldFrom, canvasRect);
            Vector2 screenTo = camera.WorldToScreen(worldTo, canvasRect);

            Color edgeColor = EdgeNormalColor;

            // Determine edge color based on properties
            if (edge.isBlocked)
                edgeColor = EdgeBlockedColor;
            else if (edge.hasSightline)
                edgeColor = EdgeSightlineColor;
            else if (edge.RequiresAnyKey())
                edgeColor = EdgeLockedColor;

            // Direction from A to B
            Vector2 dir = (screenTo - screenFrom).normalized;

            // Trim so arrows don't overlap nodes
            // We get the node size to determine how much to trim
            float nodeRadius = NodeStyleProvider.NodeSize * camera.Zoom;
            float trim = nodeRadius + 4f; // extra padding
            Vector2 lineStart = screenFrom + dir * trim;
            Vector2 lineEnd = screenTo - dir * trim;


            Handles.BeginGUI();

            Handles.color = edgeColor;

            float arrowDepth = Mathf.Clamp(10f * camera.Zoom, 6f, 18f);
            float arrowWidth = Mathf.Clamp(6f * camera.Zoom, 4f, 12f);

            float lineWidth = edge.isBlocked
                ? Mathf.Clamp(5f * camera.Zoom, 3f, 8f)
                : Mathf.Clamp(3f * camera.Zoom, 2f, 6f);

            Handles.DrawAAPolyLine(lineWidth, lineStart, lineEnd);

            Vector2 tipAtB = screenTo - dir * trim;
            Vector2 tipAtA = screenFrom + dir * trim;

            float tipInset = Mathf.Clamp(arrowDepth * 0.35f, 0f, trim * 0.75f);

            Vector2 tipB = tipAtB + dir * tipInset;
            Vector2 tipA = tipAtA - dir * tipInset;

            DrawArrowHead(tipB, dir, arrowDepth, arrowWidth);

            if (edge.bidirectional)
                DrawArrowHead(tipA, -dir, arrowDepth, arrowWidth);

            DrawEdgeLockIcon(edge, lineStart, lineEnd, camera);

            Handles.EndGUI();
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

        private void DrawEdgeLockIcon(GraphEdge edge, Vector2 lineStart, Vector2 lineEnd, CameraController camera)
        {
            if (!edge.RequiresAnyKey())
                return;

            Vector2 midpoint = (lineStart + lineEnd) * 0.5f;
            float iconSize = Mathf.Clamp(10f * camera.Zoom, 6f, 14f);

            Handles.color = LockColor;
            Handles.DrawSolidDisc(midpoint, Vector3.forward, iconSize);

            Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Handles.DrawSolidDisc(midpoint, Vector3.forward, iconSize * 0.4f);

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
            FlatGraph graph,
            Dictionary<GraphNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider,
            DungeonCycle rootCycle,
            GraphNode selectedNode)
        {
            if (graph == null || graph.nodes == null)
                return;

            foreach (var node in graph.nodes)
            {
                if (node == null)
                    continue;

                if (!positions.TryGetValue(node, out Vector2 worldPos))
                    continue;

                DrawNode(node, worldPos, canvasRect, camera, styleProvider, rootCycle, NodeStyleProvider.NodeSize, selectedNode);
            }
        }

        private void DrawNode(
            GraphNode node,
            Vector2 worldPos,
            Rect canvasRect,
            CameraController camera,
            NodeStyleProvider styleProvider,
            DungeonCycle rootCycle,
            float nodeRadius,
            GraphNode selectedNode)
        {
            Vector2 screenPos = camera.WorldToScreen(worldPos, canvasRect);
            float screenRadius = nodeRadius * camera.Zoom;

            Color nodeColor = styleProvider.GetNodeColor(node, rootCycle);
            bool isSelected = node == selectedNode;
            bool isSlot = styleProvider.IsSlotMarkerNode(node);

            Handles.BeginGUI();

            if (isSelected)
            {
                Handles.color = SelectionColor;
                Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius + 3f);
            }

            if (isSlot)
            {
                // Slot marker: hollow / faint
                Handles.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, nodeColor.a);
                Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius);

                // Small inner dot to keep it �present�
                Handles.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, nodeColor.a * 0.6f);
                Handles.DrawSolidDisc(screenPos, Vector3.forward, screenRadius * 0.25f);
            }
            else
            {
                Handles.color = nodeColor;
                Handles.DrawSolidDisc(screenPos, Vector3.forward, screenRadius);

                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius);
            }

            Handles.EndGUI();

            string label = styleProvider.GetNodeLabel(node);
            DrawNodeLabel(screenPos, label, screenRadius);

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

        private void DrawNodeKeyIcon(GraphNode node, Vector2 screenPos, float screenRadius, CameraController camera)
        {
            if (node == null || !node.GrantsAnyKey())
                return;

            float iconSize = Mathf.Clamp(8f * camera.Zoom, 5f, 12f);

            int maxKeys = 8;
            float arcPerKey = (Mathf.PI * 2f) / maxKeys;
            float startAngle = Mathf.PI * -0.5f;

            Handles.BeginGUI();

            for (int i = 0; i < node.grantedKeys.Count && i < maxKeys; i++)
            {
                float angle = startAngle - (i * arcPerKey);

                float keyRadius = screenRadius + iconSize * 0.8f;
                Vector2 keyPos = screenPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * keyRadius;

                Handles.color = KeyColor;
                Handles.DrawSolidDisc(keyPos, Vector3.forward, iconSize);

                Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
                Handles.DrawSolidDisc(keyPos, Vector3.forward, iconSize * 0.35f);
            }

            Handles.EndGUI();
        }
    }
}