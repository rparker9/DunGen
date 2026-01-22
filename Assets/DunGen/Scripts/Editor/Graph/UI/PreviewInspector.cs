using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Handles the inspector panel UI for preview mode.
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
            RewrittenGraph flatGraph,
            CycleNode selectedNode,
            DungeonCycle overallCycle)
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

            if (selectedNode != null)
            {
                DrawSelectedNodeInfo(selectedNode, overallCycle, flatGraph);
            }
            else
            {
                DrawOverallPatternInfo(overallCycle);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• MMB drag: Pan\n• Scroll: Zoom\n• LMB: Select\n\n?? = Lock\n?? = Key",
                MessageType.Info
            );

            GUILayout.EndArea();
        }

        private void DrawSelectedNodeInfo(CycleNode selectedNode, DungeonCycle overallCycle, RewrittenGraph flatGraph)
        {
            bool isSub = _styleProvider.IsSubcycleNode(selectedNode);
            bool isRewriteSite = FindRewriteSiteRecursive(overallCycle, selectedNode) != null;

            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Display:", _styleProvider.GetNodeLabel(selectedNode));
            EditorGUILayout.Toggle("Subcycle Node", isSub);
            EditorGUILayout.Toggle("Rewrite Site", isRewriteSite);

            if (selectedNode.GrantsAnyKey())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Keys Granted", EditorStyles.boldLabel);
                foreach (var keyId in selectedNode.grantedKeys)
                    EditorGUILayout.LabelField($"• Key {keyId}");
            }

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
                    EditorGUILayout.LabelField("• " + role.type.ToString());
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connected Edges", EditorStyles.boldLabel);
            DrawConnectedEdges(selectedNode, flatGraph);
        }

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
                string direction = isOutgoing ? "?" : "?";
                CycleNode otherNode = isOutgoing ? edge.to : edge.from;
                string otherLabel = _styleProvider.GetNodeLabel(otherNode);
                string edgeDesc = $"{direction} {otherLabel}";

                var properties = new System.Collections.Generic.List<string>();
                if (edge.bidirectional) properties.Add("?");
                if (edge.isBlocked) properties.Add("BLOCKED");
                if (edge.hasSightline) properties.Add("SIGHT");
                if (edge.RequiresAnyKey())
                {
                    string keys = string.Join(",", edge.requiredKeys);
                    properties.Add($"?? Keys: {keys}");
                }

                if (properties.Count > 0)
                    edgeDesc += $" ({string.Join(", ", properties)})";

                EditorGUILayout.LabelField("• " + edgeDesc);
            }

            if (edgeCount == 0)
                EditorGUILayout.LabelField("(none)");
        }

        private void DrawOverallPatternInfo(DungeonCycle overallCycle)
        {
            EditorGUILayout.LabelField("Overall Pattern", EditorStyles.boldLabel);
            if (overallCycle != null)
            {
                EditorGUILayout.LabelField("Pattern Nodes:", overallCycle.nodes.Count.ToString());
                EditorGUILayout.LabelField("Rewrite Sites:", overallCycle.rewriteSites.Count.ToString());
            }
        }

        private static RewriteSite FindRewriteSiteRecursive(DungeonCycle pattern, CycleNode node)
        {
            if (pattern == null || node == null || pattern.rewriteSites == null)
                return null;

            foreach (var site in pattern.rewriteSites)
            {
                if (site != null && site.placeholder == node)
                    return site;
            }

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