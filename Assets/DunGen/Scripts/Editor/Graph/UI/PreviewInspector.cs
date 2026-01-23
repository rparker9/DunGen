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
            PreviewLayoutEngine.LayoutResult layoutResult,
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

            // Hierarchical graph stats
            if (layoutResult != null)
            {
                DrawLayoutStats(layoutResult);
                EditorGUILayout.Space();
            }

            // Selected node info
            if (selectedNode != null)
            {
                DrawSelectedNodeInfo(selectedNode, generatedCycle, layoutResult);
            }
            else if (generatedCycle != null)
            {
                DrawOverallCycleInfo(generatedCycle);
            }

            EditorGUILayout.Space();
            DrawControls();

            GUILayout.EndArea();
        }

        public int GetMaxDepth(PreviewLayoutEngine.LayoutResult layoutResult)
        {
            int max = 0;
            foreach (var cycle in layoutResult.allCycles)
            {
                if (cycle.depth > max)
                    max = cycle.depth;
            }
            return max;
        }

        // =========================================================
        // LAYOUT STATISTICS
        // =========================================================

        private void DrawLayoutStats(PreviewLayoutEngine.LayoutResult layoutResult)
        {
            EditorGUILayout.LabelField("Hierarchical Layout", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Total Cycles:", layoutResult.allCycles.Count.ToString());
            EditorGUILayout.LabelField("Total Nodes:", layoutResult.allPositions.Count.ToString());
            EditorGUILayout.LabelField("Max Depth:", GetMaxDepth(layoutResult).ToString());

            // Count edges across all cycles
            int totalEdges = CountTotalEdges(layoutResult);
            EditorGUILayout.LabelField("Total Edges:", totalEdges.ToString());

            // Count special nodes
            int keyNodes = 0;
            int startNodes = 0;
            int goalNodes = 0;

            foreach (var kvp in layoutResult.allPositions)
            {
                var node = kvp.Key;
                if (node == null) continue;

                if (node.GrantsAnyKey())
                    keyNodes++;
                if (node.HasRole(NodeRoleType.Start))
                    startNodes++;
                if (node.HasRole(NodeRoleType.Goal))
                    goalNodes++;
            }

            EditorGUILayout.LabelField("Key Nodes:", keyNodes.ToString());
            EditorGUILayout.LabelField("Start Nodes:", startNodes.ToString());
            EditorGUILayout.LabelField("Goal Nodes:", goalNodes.ToString());
        }

        private int CountTotalEdges(PreviewLayoutEngine.LayoutResult layoutResult)
        {
            var countedEdges = new System.Collections.Generic.HashSet<GraphEdge>();
            int count = 0;

            foreach (var cycle in layoutResult.allCycles)
            {
                if (cycle?.source?.edges == null) continue;

                foreach (var edge in cycle.source.edges)
                {
                    if (edge != null && !countedEdges.Contains(edge))
                    {
                        countedEdges.Add(edge);
                        count++;
                    }
                }
            }

            return count;
        }

        // =========================================================
        // SELECTED NODE
        // =========================================================

        private void DrawSelectedNodeInfo(
            GraphNode selectedNode,
            DungeonCycle generatedCycle,
            PreviewLayoutEngine.LayoutResult layoutResult)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);

            // Node label
            string displayLabel = selectedNode.label;
            if (string.IsNullOrEmpty(displayLabel))
                displayLabel = "(unnamed)";
            EditorGUILayout.LabelField("Label:", displayLabel);

            // Find which cycle this node belongs to
            PreviewLayoutEngine.LayoutCycle ownerCycle = FindNodeOwnerCycle(selectedNode, layoutResult);
            if (ownerCycle != null)
            {
                EditorGUILayout.LabelField("Cycle Depth:", ownerCycle.depth.ToString());

                if (ownerCycle.source.startNode == selectedNode)
                    EditorGUILayout.LabelField("Role:", "Start/Entrance");
                else if (ownerCycle.source.goalNode == selectedNode)
                    EditorGUILayout.LabelField("Role:", "Goal/Exit");
            }

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
            DrawConnectedEdges(selectedNode, layoutResult);
        }

        private PreviewLayoutEngine.LayoutCycle FindNodeOwnerCycle(
            GraphNode node,
            PreviewLayoutEngine.LayoutResult layoutResult)
        {
            if (layoutResult?.allCycles == null || node == null)
                return null;

            foreach (var cycle in layoutResult.allCycles)
            {
                if (cycle?.source?.nodes != null && cycle.source.nodes.Contains(node))
                    return cycle;
            }

            return null;
        }

        private void DrawConnectedEdges(GraphNode selectedNode, PreviewLayoutEngine.LayoutResult layoutResult)
        {
            EditorGUILayout.LabelField("Connected Edges:", EditorStyles.boldLabel);

            if (layoutResult?.allCycles == null)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            int edgeCount = 0;

            // Find all edges connected to this node across all cycles
            foreach (var cycle in layoutResult.allCycles)
            {
                if (cycle?.source?.edges == null) continue;

                foreach (var edge in cycle.source.edges)
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