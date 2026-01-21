using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Handles the inspector panel UI for the graph preview window
    /// UPDATED: Shows node keys and edge locks
    /// </summary>
    public sealed class PreviewInspector
    {
        private NodeStyleProvider _styleProvider;

        public PreviewInspector(NodeStyleProvider styleProvider)
        {
            _styleProvider = styleProvider;
        }

        // =========================================================
        // INSPECTOR DRAWING
        // =========================================================

        public void DrawInspector(
            Rect rect,
            RewrittenGraph flatGraph,
            CycleNode selectedNode,
            DungeonCycle overallCycle,
            ref CycleType selectedCycleType,
            System.Action<CycleNode, CycleType> onApplyRewrite)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Flat graph stats
            if (flatGraph != null)
            {
                EditorGUILayout.LabelField("Flat Graph", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Nodes:", flatGraph.nodes.Count.ToString());
                EditorGUILayout.LabelField("Edges:", flatGraph.edges.Count.ToString());

                // Count locked edges and key nodes
                int lockedEdges = 0;
                int keyNodes = 0;
                if (flatGraph.edges != null)
                {
                    foreach (var edge in flatGraph.edges)
                    {
                        if (edge != null && edge.RequiresAnyKey())
                            lockedEdges++;
                    }
                }
                if (flatGraph.nodes != null)
                {
                    foreach (var node in flatGraph.nodes)
                    {
                        if (node != null && node.GrantsAnyKey())
                            keyNodes++;
                    }
                }
                EditorGUILayout.LabelField("Locked Edges:", lockedEdges.ToString());
                EditorGUILayout.LabelField("Key Nodes:", keyNodes.ToString());

                EditorGUILayout.Space();
            }

            // Selected node info
            if (selectedNode != null)
            {
                DrawSelectedNodeInfo(selectedNode, overallCycle, flatGraph, ref selectedCycleType, onApplyRewrite);
            }
            else
            {
                DrawOverallPatternInfo(overallCycle);
            }

            // Controls help
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "- MMB drag: Pan\n- Scroll: Zoom around mouse\n- LMB: Select node\n- Center/Fit to reframe\n",
                MessageType.Info
            );

            GUILayout.EndArea();
        }

        private void DrawSelectedNodeInfo(
            CycleNode selectedNode,
            DungeonCycle overallCycle,
            RewrittenGraph flatGraph,
            ref CycleType selectedCycleType,
            System.Action<CycleNode, CycleType> onApplyRewrite)
        {
            bool isSub = _styleProvider.IsSubcycleNode(selectedNode);
            bool isRewriteSite = FindRewriteSiteRecursive(overallCycle, selectedNode) != null;

            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Display:", _styleProvider.GetNodeLabel(selectedNode));
            EditorGUILayout.Toggle("Subcycle Node", isSub);
            EditorGUILayout.Toggle("Rewrite Site", isRewriteSite);

            // Keys granted by this node
            if (selectedNode.GrantsAnyKey())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Keys Granted", EditorStyles.boldLabel);
                foreach (var keyId in selectedNode.grantedKeys)
                {
                    EditorGUILayout.LabelField($"- Key {keyId}");
                }
            }

            // Roles list
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Roles", EditorStyles.boldLabel);

            if (selectedNode.roles == null || selectedNode.roles.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var role in selectedNode.roles)
                {
                    if (role == null) continue;
                    EditorGUILayout.LabelField("- " + role.type.ToString());
                }
            }

            // Connected edges
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connected Edges", EditorStyles.boldLabel);
            DrawConnectedEdges(selectedNode, flatGraph);

            // Rewrite controls
            if (isRewriteSite)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rewrite", EditorStyles.boldLabel);
                selectedCycleType = (CycleType)EditorGUILayout.EnumPopup("Replacement", selectedCycleType);

                if (GUILayout.Button("Apply Rewrite"))
                {
                    onApplyRewrite?.Invoke(selectedNode, selectedCycleType);
                }
            }
        }

        // Draw information about edges connected to selected node
        private void DrawConnectedEdges(CycleNode selectedNode, RewrittenGraph flatGraph)
        {
            if (flatGraph == null || flatGraph.edges == null || selectedNode == null)
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

                // Build edge description
                string direction = isOutgoing ? "->" : "<-";
                CycleNode otherNode = isOutgoing ? edge.to : edge.from;
                string otherLabel = _styleProvider.GetNodeLabel(otherNode);

                string edgeDesc = $"{direction} {otherLabel}";

                // Add edge properties
                var properties = new System.Collections.Generic.List<string>();

                if (edge.bidirectional)
                    properties.Add("<->");
                if (edge.isBlocked)
                    properties.Add("BLOCKED");
                if (edge.hasSightline)
                    properties.Add("SIGHT");

                // Show lock info (edge property)
                if (edge.RequiresAnyKey())
                {
                    string keys = string.Join(",", edge.requiredKeys);
                    properties.Add($"Reqd Keys: {keys}");
                }

                if (properties.Count > 0)
                    edgeDesc += $" ({string.Join(", ", properties)})";

                EditorGUILayout.LabelField("- " + edgeDesc);
            }

            if (edgeCount == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
        }

        private void DrawOverallPatternInfo(DungeonCycle overallCycle)
        {
            EditorGUILayout.LabelField("Overall Pattern", EditorStyles.boldLabel);
            if (overallCycle != null)
            {
                EditorGUILayout.LabelField("Type:", overallCycle.type.ToString());
                EditorGUILayout.LabelField("Pattern Nodes:", overallCycle.nodes.Count.ToString());
                EditorGUILayout.LabelField("Rewrite Sites:", overallCycle.rewriteSites.Count.ToString());
            }
        }

        // =========================================================
        // HELPER: Find rewrite site
        // =========================================================

        private static RewriteSite FindRewriteSiteRecursive(DungeonCycle pattern, CycleNode node)
        {
            if (pattern == null || node == null || pattern.rewriteSites == null)
                return null;

            // Check direct sites
            foreach (var site in pattern.rewriteSites)
            {
                if (site != null && site.placeholder == node)
                    return site;
            }

            // Check nested sites
            foreach (var site in pattern.rewriteSites)
            {
                if (site != null && site.HasReplacement())
                {
                    var found = FindRewriteSiteRecursive(site.replacementPattern, node);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }
    }
}