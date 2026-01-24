using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Inspector panel for preview mode.
    /// Displays hierarchical graph statistics and selected node information.
    /// </summary>
    public sealed class PreviewInspector
    {
        private NodeStyleProvider _styleProvider;

        public PreviewInspector(NodeStyleProvider styleProvider)
        {
            _styleProvider = styleProvider;
        }

        public void DrawInspector(
            Rect rect,
            PreviewLayoutEngine.Result layout,
            FlatGraph flatGraph,
            GraphNode selectedNode,
            DungeonCycle generatedCycle,
            int currentSeed)

        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Preview Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Generation info
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Seed:", currentSeed.ToString());
            EditorGUILayout.Space();

            // Planar layout stats
            if (layout != null)
            {
                EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);

                if (flatGraph == null)
                {
                    EditorGUILayout.LabelField("Graph:", "(none)");
                }
                else
                {
                    EditorGUILayout.LabelField("Nodes:", flatGraph.NodeCount.ToString());
                    EditorGUILayout.LabelField("Edges:", flatGraph.EdgeCount.ToString());
                }

                EditorGUILayout.LabelField("Planar:", layout.isPlanar ? "Yes" : "No");

                if (!string.IsNullOrEmpty(layout.warning))
                    EditorGUILayout.HelpBox(layout.warning, layout.isPlanar ? MessageType.Info : MessageType.Warning);

                EditorGUILayout.Space();
            }

            // Selected node info
            if (selectedNode != null)
            {
                DrawSelectedNodeInfo(selectedNode, generatedCycle, flatGraph);
            }
            else if (generatedCycle != null)
            {
                DrawOverallCycleInfo(generatedCycle);
            }

            EditorGUILayout.Space();
            DrawControls();

            GUILayout.EndArea();
        }

        // =========================================================
        // LAYOUT STATISTICS
        // =========================================================

        private void DrawLayoutStats(PreviewLayoutEngine.Result layoutResult)
        {
           
        }

        // =========================================================
        // SELECTED NODE
        // =========================================================

        private void DrawSelectedNodeInfo(
            GraphNode selectedNode,
            DungeonCycle generatedCycle,
            FlatGraph flatGraph)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);

            // Node label
            string displayLabel = selectedNode.label;
            if (string.IsNullOrEmpty(displayLabel))
                displayLabel = "(unnamed)";
            EditorGUILayout.LabelField("Label:", displayLabel);

            // Roles
            if (selectedNode.roles != null && selectedNode.roles.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Roles:", EditorStyles.boldLabel);
                foreach (var role in selectedNode.roles)
                {
                    if (role != null)
                        EditorGUILayout.LabelField("  • " + role.type.ToString());
                }
            }

            // Keys granted
            if (selectedNode.GrantsAnyKey())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Keys Granted:", EditorStyles.boldLabel);
                foreach (var keyId in selectedNode.grantedKeys)
                    EditorGUILayout.LabelField($"  • Key {keyId}");
            }

            // Connected edges
            EditorGUILayout.Space();
            DrawConnectedEdges(selectedNode, flatGraph);
        }

        private void DrawConnectedEdges(GraphNode selectedNode, FlatGraph flatGraph)
        {
            EditorGUILayout.LabelField("Connected Edges:", EditorStyles.boldLabel);

            if (flatGraph?.edges == null)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            int edgeCount = 0;

            foreach (var edge in flatGraph.edges)
            {
                if (edge == null) continue;

                bool isOutgoing = edge.from == selectedNode;
                bool isIncoming = edge.to == selectedNode;
                if (!isOutgoing && !isIncoming) continue;

                edgeCount++;

                string direction = isOutgoing ? "->" : "<-";
                GraphNode otherNode = isOutgoing ? edge.to : edge.from;
                string otherLabel = otherNode?.label ?? "(unnamed)";
                string edgeDesc = $"{direction} {otherLabel}";

                var properties = new System.Collections.Generic.List<string>();
                if (edge.bidirectional) properties.Add("<->");
                if (edge.isBlocked) properties.Add("BLOCKED");
                if (edge.hasSightline) properties.Add("SIGHT");
                if (edge.RequiresAnyKey())
                {
                    string keys = string.Join(",", edge.requiredKeys);
                    properties.Add($"Reqd Keys: {keys}");
                }

                if (properties.Count > 0)
                    edgeDesc += $" ({string.Join(", ", properties)})";

                EditorGUILayout.LabelField("  • " + edgeDesc);
            }

            if (edgeCount == 0)
                EditorGUILayout.LabelField("(none)");
        }

        // =========================================================
        // OVERALL CYCLE INFO
        // =========================================================

        private void DrawOverallCycleInfo(DungeonCycle generatedCycle)
        {
            EditorGUILayout.LabelField("Generated Dungeon", EditorStyles.boldLabel);

            if (generatedCycle != null)
            {
                int totalNodes = CountNodesRecursive(generatedCycle);
                int totalCycles = CountCyclesRecursive(generatedCycle);

                EditorGUILayout.LabelField("Root Nodes:", generatedCycle.nodes?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("Total Nodes:", totalNodes.ToString());
                EditorGUILayout.LabelField("Total Cycles:", totalCycles.ToString());

                if (generatedCycle.startNode != null)
                    EditorGUILayout.LabelField("Start Node:", generatedCycle.startNode.label ?? "(unnamed)");
                if (generatedCycle.goalNode != null)
                    EditorGUILayout.LabelField("Goal Node:", generatedCycle.goalNode.label ?? "(unnamed)");
            }
        }

        private int CountNodesRecursive(DungeonCycle cycle)
        {
            if (cycle == null) return 0;

            int count = cycle.nodes?.Count ?? 0;

            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site?.replacementPattern != null)
                        count += CountNodesRecursive(site.replacementPattern);
                }
            }

            return count;
        }

        private int CountCyclesRecursive(DungeonCycle cycle)
        {
            if (cycle == null) return 0;

            int count = 1; // This cycle

            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site?.replacementPattern != null)
                        count += CountCyclesRecursive(site.replacementPattern);
                }
            }

            return count;
        }

        // =========================================================
        // CONTROLS
        // =========================================================

        private void DrawControls()
        {
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• MMB drag: Pan\n• Scroll: Zoom\n• LMB: Select node\n\nColors:\n• Green = Start/Entrance\n• Red = Goal/Exit\n• Blue = Normal node\n• Yellow = Selected",
                MessageType.Info
            );
        }
    }
}