using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Renders hierarchical graph layouts with cycle-aware edge routing.
    /// Edges within a cycle are straight lines.
    /// Edges between cycles use curved routing to avoid crossings.
    /// </summary>
    public sealed class PreviewGraphRenderer
    {
        // =========================================================
        // COLORS & STYLES
        // =========================================================

        private static readonly Color CycleBorderColor = new Color(0.3f, 0.3f, 0.4f, 0.3f);
        private static readonly Color EdgeColorIntra = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color EdgeColorInter = new Color(0.5f, 0.7f, 1f, 0.8f);
        private static readonly Color NodeColor = new Color(0.2f, 0.6f, 1f, 1f);
        private static readonly Color NodeColorSelected = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color NodeColorStart = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color NodeColorGoal = new Color(1f, 0.3f, 0.3f, 1f);

        private const float NodeRadius = 25f;
        private const float EdgeWidth = 2f;
        private const float CycleBorderWidth = 2f;

        // =========================================================
        // DRAWING BACKGROUND
        // =========================================================

        public void DrawBackground(Rect canvasRect)
        {
            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        // =========================================================
        // DRAWING GRID
        // =========================================================

        public void DrawGrid(Rect canvasRect, CameraController camera)
        {
            float gridSpacing = 100f * camera.Zoom;

            if (gridSpacing < 10f)
                return;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.1f);

            // Vertical lines
            float startX = canvasRect.x - (camera.Center.x * camera.Zoom) % gridSpacing;
            for (float x = startX; x < canvasRect.xMax; x += gridSpacing)
            {
                Handles.DrawLine(
                    new Vector2(x, canvasRect.y),
                    new Vector2(x, canvasRect.yMax)
                );
            }

            // Horizontal lines
            float startY = canvasRect.y + (camera.Center.y * camera.Zoom) % gridSpacing;
            for (float y = startY; y < canvasRect.yMax; y += gridSpacing)
            {
                Handles.DrawLine(
                    new Vector2(canvasRect.x, y),
                    new Vector2(canvasRect.xMax, y)
                );
            }

            Handles.EndGUI();
        }

        // =========================================================
        // DRAWING HIERARCHICAL LAYOUT
        // =========================================================

        /// <summary>
        /// Draw complete hierarchical graph
        /// </summary>
        public void DrawHierarchicalGraph(
            PreviewLayoutEngine.LayoutResult layout,
            Rect canvasRect,
            CameraController camera,
            GraphNode selectedNode = null)
        {
            if (layout?.root == null)
                return;

            Handles.BeginGUI();

            // 1. Draw cycle boundaries (bottom layer)
            DrawCycleBoundaries(layout.allCycles, canvasRect, camera);

            // 2. Draw edges (middle layer)
            DrawEdges(layout, canvasRect, camera);

            // 3. Draw nodes (top layer)
            DrawNodes(layout, canvasRect, camera, selectedNode);

            Handles.EndGUI();
        }

        // =========================================================
        // CYCLE BOUNDARIES
        // =========================================================

        private void DrawCycleBoundaries(
            List<PreviewLayoutEngine.LayoutCycle> cycles,
            Rect canvasRect,
            CameraController camera)
        {
            foreach (var cycle in cycles)
            {
                if (cycle == null) continue;

                Vector2 screenCenter = camera.WorldToScreen(cycle.center, canvasRect);
                float screenRadius = cycle.radius * camera.Zoom;

                // Draw cycle circle
                Handles.color = CycleBorderColor;
                Handles.DrawSolidDisc(screenCenter, Vector3.forward, screenRadius);

                // Draw thicker border
                Handles.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
                Handles.DrawWireDisc(screenCenter, Vector3.forward, screenRadius, CycleBorderWidth);

                // Draw depth label
                var labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                labelStyle.fontSize = 10;
                labelStyle.alignment = TextAnchor.MiddleCenter;

                string depthLabel = $"Depth {cycle.depth}";
                Vector2 labelPos = screenCenter - new Vector2(0, screenRadius + 15f);
                GUI.Label(new Rect(labelPos.x - 50, labelPos.y, 100, 20), depthLabel, labelStyle);
            }
        }

        // =========================================================
        // EDGES
        // =========================================================

        private void DrawEdges(
            PreviewLayoutEngine.LayoutResult layout,
            Rect canvasRect,
            CameraController camera)
        {
            // We need to categorize edges:
            // - Intra-cycle: both nodes in same cycle → straight line
            // - Inter-cycle: nodes in different cycles → curved line

            var drawnEdges = new HashSet<(GraphNode, GraphNode)>();

            foreach (var cycle in layout.allCycles)
            {
                if (cycle?.source?.edges == null) continue;

                foreach (var edge in cycle.source.edges)
                {
                    if (edge?.from == null || edge.to == null) continue;

                    // Check if already drawn
                    var edgeKey = (edge.from, edge.to);
                    var reverseKey = (edge.to, edge.from);

                    if (drawnEdges.Contains(edgeKey) || drawnEdges.Contains(reverseKey))
                        continue;

                    drawnEdges.Add(edgeKey);

                    // Get positions
                    if (!layout.allPositions.TryGetValue(edge.from, out Vector2 fromWorld))
                        continue;
                    if (!layout.allPositions.TryGetValue(edge.to, out Vector2 toWorld))
                        continue;

                    // Check if intra-cycle or inter-cycle
                    bool isIntraCycle = AreBothNodesInCycle(edge.from, edge.to, cycle);

                    if (isIntraCycle)
                    {
                        DrawStraightEdge(fromWorld, toWorld, canvasRect, camera, EdgeColorIntra);
                    }
                    else
                    {
                        DrawCurvedEdge(fromWorld, toWorld, canvasRect, camera, EdgeColorInter);
                    }
                }
            }
        }

        private bool AreBothNodesInCycle(GraphNode a, GraphNode b, PreviewLayoutEngine.LayoutCycle cycle)
        {
            if (cycle?.source?.nodes == null)
                return false;

            bool hasA = cycle.source.nodes.Contains(a);
            bool hasB = cycle.source.nodes.Contains(b);

            return hasA && hasB;
        }

        private void DrawStraightEdge(Vector2 fromWorld, Vector2 toWorld, Rect canvasRect, CameraController camera, Color color)
        {
            Vector2 fromScreen = camera.WorldToScreen(fromWorld, canvasRect);
            Vector2 toScreen = camera.WorldToScreen(toWorld, canvasRect);

            Handles.color = color;
            Handles.DrawAAPolyLine(EdgeWidth, fromScreen, toScreen);
        }

        private void DrawCurvedEdge(Vector2 fromWorld, Vector2 toWorld, Rect canvasRect, CameraController camera, Color color)
        {
            Vector2 fromScreen = camera.WorldToScreen(fromWorld, canvasRect);
            Vector2 toScreen = camera.WorldToScreen(toWorld, canvasRect);

            // Calculate control point for Bezier curve
            Vector2 mid = (fromScreen + toScreen) * 0.5f;
            Vector2 perpendicular = new Vector2(-(toScreen.y - fromScreen.y), toScreen.x - fromScreen.x);

            if (perpendicular.magnitude > 0.001f)
                perpendicular = perpendicular.normalized;

            // Curve outward (amount based on distance)
            float distance = Vector2.Distance(fromScreen, toScreen);
            float curveAmount = Mathf.Min(distance * 0.3f, 100f);
            Vector2 controlPoint = mid + perpendicular * curveAmount;

            Handles.color = color;
            Handles.DrawBezier(fromScreen, toScreen, controlPoint, controlPoint, color, null, EdgeWidth);
        }

        // =========================================================
        // NODES
        // =========================================================

        private void DrawNodes(
            PreviewLayoutEngine.LayoutResult layout,
            Rect canvasRect,
            CameraController camera,
            GraphNode selectedNode)
        {
            foreach (var cycle in layout.allCycles)
            {
                if (cycle?.source?.nodes == null) continue;

                foreach (var node in cycle.source.nodes)
                {
                    if (node == null) continue;
                    if (!cycle.nodePositions.TryGetValue(node, out Vector2 worldPos)) continue;

                    DrawNode(node, worldPos, canvasRect, camera, selectedNode, cycle);
                }
            }
        }

        private void DrawNode(
            GraphNode node,
            Vector2 worldPos,
            Rect canvasRect,
            CameraController camera,
            GraphNode selectedNode,
            PreviewLayoutEngine.LayoutCycle cycle)
        {
            Vector2 screenPos = camera.WorldToScreen(worldPos, canvasRect);
            float screenRadius = NodeRadius * camera.Zoom;

            // Determine color
            Color color = NodeColor;
            if (node == selectedNode)
                color = NodeColorSelected;
            else if (node == cycle.source.startNode)
                color = NodeColorStart;
            else if (node == cycle.source.goalNode)
                color = NodeColorGoal;

            // Draw node circle
            Handles.color = color;
            Handles.DrawSolidDisc(screenPos, Vector3.forward, screenRadius);

            // Draw border
            Handles.color = Color.white;
            Handles.DrawWireDisc(screenPos, Vector3.forward, screenRadius, 2f);

            // Draw label
            if (camera.Zoom > 0.5f && !string.IsNullOrEmpty(node.label))
            {
                var labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontSize = Mathf.RoundToInt(10 * camera.Zoom);
                labelStyle.alignment = TextAnchor.MiddleCenter;

                Vector2 labelPos = screenPos + new Vector2(0, screenRadius + 10f * camera.Zoom);
                GUI.Label(
                    new Rect(labelPos.x - 50, labelPos.y, 100, 20),
                    node.label,
                    labelStyle
                );
            }
        }
    }
}