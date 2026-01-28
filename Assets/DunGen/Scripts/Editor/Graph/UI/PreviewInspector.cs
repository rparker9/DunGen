using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Inspector panel for preview mode.
    /// Shows stats about the generated dungeon and selected nodes.
    /// UPDATED: Displays KeyRegistry information.
    /// </summary>
    public sealed class PreviewInspector
    {
        private Vector2 _scrollPos;
        private NodeStyleProvider _styleProvider;

        public PreviewInspector(NodeStyleProvider styleProvider)
        {
            _styleProvider = styleProvider;
        }

        public void DrawInspector(
            Rect rect,
            PreviewLayoutEngine.Result layoutResult,
            FlatGraph graph,
            GraphNode selectedNode,
            GraphEdge selectedEdge,
            DungeonCycle rootCycle,
            int seed,
            KeyRegistry keyRegistry)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Preview Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (selectedNode != null)
            {
                DrawNodeInspector(selectedNode, rootCycle);
            }
            else if (selectedEdge != null)
            {
                DrawEdgeInspector(selectedEdge, graph);
            }
            else
            {
                DrawDungeonStats(graph, layoutResult, seed, keyRegistry);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // =========================================================
        // DUNGEON STATS
        // =========================================================

        private void DrawDungeonStats(FlatGraph graph, PreviewLayoutEngine.Result layoutResult, int seed, KeyRegistry keyRegistry)
        {
            EditorGUILayout.LabelField("Dungeon Overview", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Seed:", seed.ToString());
            EditorGUILayout.Space();

            if (graph != null)
            {
                EditorGUILayout.LabelField("Nodes:", graph.NodeCount.ToString());
                EditorGUILayout.LabelField("Edges:", graph.EdgeCount.ToString());

                // Key stats
                if (keyRegistry != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Keys:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Total Unique Keys:", keyRegistry.KeyCount.ToString());

                    var allKeys = keyRegistry.GetAllKeys();
                    if (allKeys != null && allKeys.Count > 0)
                    {
                        // Count by type
                        var hardKeys = allKeys.Count(k => k.type == KeyType.Hard);
                        var softKeys = allKeys.Count(k => k.type == KeyType.Soft);
                        var abilityKeys = allKeys.Count(k => k.type == KeyType.Ability);
                        var itemKeys = allKeys.Count(k => k.type == KeyType.Item);
                        var triggerKeys = allKeys.Count(k => k.type == KeyType.Trigger);
                        var narrativeKeys = allKeys.Count(k => k.type == KeyType.Narrative);

                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (hardKeys > 0) EditorGUILayout.LabelField($"Hard: {hardKeys}");
                            if (softKeys > 0) EditorGUILayout.LabelField($"Soft: {softKeys}");
                            if (abilityKeys > 0) EditorGUILayout.LabelField($"Ability: {abilityKeys}");
                            if (itemKeys > 0) EditorGUILayout.LabelField($"Item: {itemKeys}");
                            if (triggerKeys > 0) EditorGUILayout.LabelField($"Trigger: {triggerKeys}");
                            if (narrativeKeys > 0) EditorGUILayout.LabelField($"Narrative: {narrativeKeys}");
                        }
                    }
                }

                // Lock stats
                int totalLocks = 0;
                if (graph.edges != null)
                {
                    foreach (var edge in graph.edges)
                    {
                        if (edge?.requiredKeys != null)
                            totalLocks += edge.requiredKeys.Count;
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Locks:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Total Locks:", totalLocks.ToString());
            }

            if (layoutResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Layout:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Planar:", layoutResult.isPlanar ? "Yes" : "No");
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Controls:\n" +
                "• Mouse wheel: Zoom\n" +
                "• Middle mouse: Pan\n" +
                "• Click node: Select",
                MessageType.Info
            );
        }

        // =========================================================
        // NODE INSPECTOR
        // =========================================================

        private void DrawNodeInspector(GraphNode node, DungeonCycle rootCycle)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Label:", node.label);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Roles:", EditorStyles.boldLabel);

            if (node.roles == null || node.roles.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var role in node.roles)
                {
                    if (role != null)
                        EditorGUILayout.LabelField($"• {role.type}");
                }
            }

            // Granted keys
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Granted Keys:", EditorStyles.boldLabel);

            if (node.grantedKeys == null || node.grantedKeys.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var key in node.grantedKeys)
                {
                    if (key == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prevColor = GUI.backgroundColor;
                        GUI.backgroundColor = key.color;
                        GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
                        GUI.backgroundColor = prevColor;

                        EditorGUILayout.LabelField($"{key.displayName} ({key.type})");
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Click a different node to inspect it, or click empty space to see dungeon stats.",
                MessageType.Info
            );
        }

        // =========================================================
        // EDGE INSPECTOR
        // =========================================================

        private void DrawEdgeInspector(GraphEdge edge, FlatGraph graph)
        {
            EditorGUILayout.LabelField("Selected Edge", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Connection info
            EditorGUILayout.LabelField("Connection:", EditorStyles.boldLabel);
            string fromLabel = edge.from?.label ?? "(unknown)";
            string toLabel = edge.to?.label ?? "(unknown)";
            EditorGUILayout.LabelField($"From: {fromLabel}");
            EditorGUILayout.LabelField($"To: {toLabel}");

            EditorGUILayout.Space();

            // Properties
            EditorGUILayout.LabelField("Properties:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Bidirectional: {(edge.bidirectional ? "Yes" : "No")}");
            EditorGUILayout.LabelField($"Blocked: {(edge.isBlocked ? "Yes" : "No")}");
            EditorGUILayout.LabelField($"Has Sightline: {(edge.hasSightline ? "Yes" : "No")}");

            EditorGUILayout.Space();

            // Lock requirements
            EditorGUILayout.LabelField("Lock Requirements:", EditorStyles.boldLabel);

            if (edge.requiredKeys == null || edge.requiredKeys.Count == 0)
            {
                EditorGUILayout.LabelField("(none - freely traversable)");
            }
            else
            {
                foreach (var req in edge.requiredKeys)
                {
                    if (req == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prevColor = GUI.backgroundColor;
                        GUI.backgroundColor = req.color;
                        GUILayout.Box("", GUILayout.Width(16), GUILayout.Height(16));
                        GUI.backgroundColor = prevColor;

                        EditorGUILayout.LabelField($"Requires: {req.requiredKeyId} ({req.type})");
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Click a node or different edge to inspect it, or click empty space to see dungeon stats.",
                MessageType.Info
            );
        }
    }
}