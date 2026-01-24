#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Minimal planar graph renderer:
    /// - Straight edges
    /// - Simple nodes
    /// - No hierarchy, no curved routing, no cycle discs
    /// </summary>
    public sealed class PreviewGraphRenderer
    {
        private const float NodeRadius = 22f;
        private const float EdgeWidth = 2f;

        private static readonly Color EdgeColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color NodeColor = new Color(0.2f, 0.6f, 1f, 1f);
        private static readonly Color NodeColorSelected = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color NodeColorStart = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color NodeColorGoal = new Color(1f, 0.3f, 0.3f, 1f);

        public void DrawBackground(Rect canvasRect)
        {
            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        public void DrawGrid(Rect canvasRect, CameraController camera)
        {
            float gridSpacing = 100f * camera.Zoom;
            if (gridSpacing < 10f) return;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.08f);

            float startX = canvasRect.x - (camera.Center.x * camera.Zoom) % gridSpacing;
            for (float x = startX; x < canvasRect.xMax; x += gridSpacing)
                Handles.DrawLine(new Vector2(x, canvasRect.y), new Vector2(x, canvasRect.yMax));

            float startY = canvasRect.y + (camera.Center.y * camera.Zoom) % gridSpacing;
            for (float y = startY; y < canvasRect.yMax; y += gridSpacing)
                Handles.DrawLine(new Vector2(canvasRect.x, y), new Vector2(canvasRect.xMax, y));

            Handles.EndGUI();
        }

        public void DrawFlatGraph(
            FlatGraph graph,
            Dictionary<GraphNode, Vector2> positions,
            Rect canvasRect,
            CameraController camera,
            GraphNode selected,
            GraphNode start,
            GraphNode goal)
        {
            if (graph == null || positions == null || positions.Count == 0)
                return;

            Handles.BeginGUI();

            DrawEdges(graph, positions, canvasRect, camera);
            DrawNodes(graph, positions, canvasRect, camera, selected, start, goal);

            Handles.EndGUI();
        }

        private void DrawEdges(FlatGraph graph, Dictionary<GraphNode, Vector2> pos, Rect canvasRect, CameraController camera)
        {
            if (graph.edges == null) return;

            var drawn = new HashSet<(GraphNode, GraphNode)>();

            Handles.color = EdgeColor;

            for (int i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e?.from == null || e.to == null) continue;

                // Dedup undirected visually
                var k = (e.from, e.to);
                var rk = (e.to, e.from);
                if (drawn.Contains(k) || drawn.Contains(rk))
                    continue;
                drawn.Add(k);

                if (!pos.TryGetValue(e.from, out var a)) continue;
                if (!pos.TryGetValue(e.to, out var b)) continue;

                Vector2 aS = camera.WorldToScreen(a, canvasRect);
                Vector2 bS = camera.WorldToScreen(b, canvasRect);

                Handles.DrawAAPolyLine(EdgeWidth, aS, bS);
            }
        }

        private void DrawNodes(
            FlatGraph graph,
            Dictionary<GraphNode, Vector2> pos,
            Rect canvasRect,
            CameraController camera,
            GraphNode selected,
            GraphNode start,
            GraphNode goal)
        {
            if (graph.nodes == null) return;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                var n = graph.nodes[i];
                if (n == null) continue;
                if (!pos.TryGetValue(n, out var w)) continue;

                Vector2 s = camera.WorldToScreen(w, canvasRect);
                float r = NodeRadius * camera.Zoom;

                Color c = NodeColor;
                if (n == selected) c = NodeColorSelected;
                else if (n == start) c = NodeColorStart;
                else if (n == goal) c = NodeColorGoal;

                Handles.color = c;
                Handles.DrawSolidDisc(s, Vector3.forward, r);

                Handles.color = Color.white;
                Handles.DrawWireDisc(s, Vector3.forward, r, 2f);

                if (camera.Zoom > 0.55f && !string.IsNullOrEmpty(n.label))
                {
                    var style = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = Mathf.RoundToInt(10 * camera.Zoom)
                    };
                    style.normal.textColor = Color.white;

                    var p = s + new Vector2(0, r + 10f * camera.Zoom);
                    GUI.Label(new Rect(p.x - 70, p.y, 140, 18), n.label, style);
                }
            }
        }
    }
}
#endif
