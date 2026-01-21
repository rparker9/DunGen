using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Editable inspector panel for Author Canvas.
    /// Allows editing node/edge properties, roles, keys, and locks.
    /// </summary>
    public sealed class AuthorInspector
    {
        private NodeStyleProvider _styleProvider;

        // Edge selection state
        private CycleEdge _selectedEdge;

        public CycleEdge SelectedEdge => _selectedEdge;

        public AuthorInspector(NodeStyleProvider styleProvider)
        {
            _styleProvider = styleProvider;
        }

        public void SetSelectedEdge(CycleEdge edge)
        {
            _selectedEdge = edge;
        }

        public void ClearEdgeSelection()
        {
            _selectedEdge = null;
        }

        // =========================================================
        // MAIN INSPECTOR
        // =========================================================

        public void DrawInspector(
            Rect rect,
            DungeonCycle currentTemplate,
            CycleNode selectedNode,
            System.Action onTemplateChanged)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Author Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Template info
            if (currentTemplate != null)
            {
                EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Type:", currentTemplate.type.ToString());
                EditorGUILayout.LabelField("Nodes:", currentTemplate.nodes.Count.ToString());
                EditorGUILayout.LabelField("Edges:", currentTemplate.edges.Count.ToString());
                EditorGUILayout.Space();
            }

            // Selected node info (editable)
            if (selectedNode != null)
            {
                DrawNodeEditor(selectedNode, currentTemplate, onTemplateChanged);
            }
            // Selected edge info (editable)
            else if (_selectedEdge != null)
            {
                DrawEdgeEditor(_selectedEdge, currentTemplate, onTemplateChanged);
            }
            else
            {
                EditorGUILayout.HelpBox("Select a node or edge to edit its properties", MessageType.Info);
            }

            // Controls help
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                ". Click: Select node\n" +
                ". Drag: Move node\n" +
                ". Shift+Click: Connect nodes\n" +
                ". Delete: Remove node\n" +
                ". Escape: Cancel operation\n" +
                ". MMB: Pan\n" +
                ". Scroll: Zoom",
                MessageType.Info
            );

            GUILayout.EndArea();
        }

        // =========================================================
        // NODE EDITOR
        // =========================================================

        private void DrawNodeEditor(CycleNode node, DungeonCycle template, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // Editable label
            node.label = EditorGUILayout.TextField("Label:", node.label);

            EditorGUILayout.Space();

            // Keys section
            DrawKeysEditor(node);

            EditorGUILayout.Space();

            // Roles section
            DrawRolesEditor(node);

            EditorGUILayout.Space();

            // Rewrite site toggle
            DrawRewriteSiteEditor(node, template);

            EditorGUILayout.Space();

            // Connected edges (read-only)
            EditorGUILayout.LabelField("Connected Edges", EditorStyles.boldLabel);
            DrawConnectedEdges(node, template);

            if (EditorGUI.EndChangeCheck())
            {
                onChanged?.Invoke();
            }
        }

        private void DrawKeysEditor(CycleNode node)
        {
            EditorGUILayout.LabelField("Keys Granted", EditorStyles.boldLabel);

            if (node.grantedKeys == null)
                node.grantedKeys = new List<int>();

            // Display existing keys
            for (int i = node.grantedKeys.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                node.grantedKeys[i] = EditorGUILayout.IntField($"Key {i + 1}:", node.grantedKeys[i]);

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    node.grantedKeys.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add key button
            if (GUILayout.Button("+ Add Key"))
            {
                int newKeyId = node.grantedKeys.Count > 0
                    ? node.grantedKeys[node.grantedKeys.Count - 1] + 1
                    : 1;
                node.grantedKeys.Add(newKeyId);
            }
        }

        private void DrawRolesEditor(CycleNode node)
        {
            EditorGUILayout.LabelField("Roles", EditorStyles.boldLabel);

            if (node.roles == null)
                node.roles = new List<NodeRole>();

            // Display existing roles
            for (int i = node.roles.Count - 1; i >= 0; i--)
            {
                if (node.roles[i] == null) continue;

                EditorGUILayout.BeginHorizontal();

                // Don't allow removing Start/Goal roles
                bool isStructural = node.roles[i].type == NodeRoleType.Start ||
                                   node.roles[i].type == NodeRoleType.Goal;

                GUI.enabled = !isStructural;
                EditorGUILayout.LabelField($". {node.roles[i].type}", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    node.roles.RemoveAt(i);
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }

            // Add role dropdown
            EditorGUILayout.BeginHorizontal();

            // Get available roles (exclude Start/Goal/RewriteSite)
            var availableRoles = new List<NodeRoleType>
            {
                NodeRoleType.Barrier,
                NodeRoleType.Secret,
                NodeRoleType.Danger,
                NodeRoleType.FalseGoal,
                NodeRoleType.Patrol,
                NodeRoleType.Reward
            };

            if (GUILayout.Button("+ Add Role"))
            {
                GenericMenu menu = new GenericMenu();
                foreach (var roleType in availableRoles)
                {
                    // Check if role already exists
                    bool hasRole = node.HasRole(roleType);
                    menu.AddItem(new GUIContent(roleType.ToString()), hasRole, () =>
                    {
                        if (!hasRole)
                            node.AddRole(roleType);
                    });
                }
                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRewriteSiteEditor(CycleNode node, DungeonCycle template)
        {
            if (template == null || template.rewriteSites == null)
                return;

            EditorGUILayout.LabelField("Rewrite Site", EditorStyles.boldLabel);

            bool isRewriteSite = template.rewriteSites.Exists(s => s != null && s.placeholder == node);

            EditorGUI.BeginChangeCheck();
            bool shouldBeRewriteSite = EditorGUILayout.Toggle("Is Rewrite Site:", isRewriteSite);

            if (EditorGUI.EndChangeCheck())
            {
                if (shouldBeRewriteSite && !isRewriteSite)
                {
                    // Add as rewrite site
                    template.rewriteSites.Add(new RewriteSite(node));
                }
                else if (!shouldBeRewriteSite && isRewriteSite)
                {
                    // Remove from rewrite sites
                    template.rewriteSites.RemoveAll(s => s != null && s.placeholder == node);
                }
            }
        }

        private void DrawConnectedEdges(CycleNode node, DungeonCycle template)
        {
            if (template == null || template.edges == null)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            int edgeCount = 0;

            foreach (var edge in template.edges)
            {
                if (edge == null) continue;

                bool isOutgoing = edge.from == node;
                bool isIncoming = edge.to == node;

                if (!isOutgoing && !isIncoming) continue;

                edgeCount++;

                string direction = isOutgoing ? "->" : "<-";
                CycleNode otherNode = isOutgoing ? edge.to : edge.from;
                string otherLabel = otherNode != null && !string.IsNullOrEmpty(otherNode.label)
                    ? otherNode.label
                    : "<unknown>";

                string edgeDesc = $"{direction} {otherLabel}";

                var properties = new List<string>();

                if (edge.bidirectional)
                    properties.Add("<->");
                if (edge.isBlocked)
                    properties.Add("BLOCKED");
                if (edge.RequiresAnyKey())
                    properties.Add($"LOCK {string.Join(",", edge.requiredKeys)}");

                if (properties.Count > 0)
                    edgeDesc += $" ({string.Join(", ", properties)})";

                // Make edges clickable - use actual button
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(".", GUILayout.Width(15));
                    if (GUILayout.Button(edgeDesc, GUI.skin.label))
                    {
                        _selectedEdge = edge;
                    }
                }
            }

            if (edgeCount == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
        }

        // =========================================================
        // EDGE EDITOR
        // =========================================================

        private void DrawEdgeEditor(CycleEdge edge, DungeonCycle template, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Selected Edge", EditorStyles.boldLabel);

            if (edge.from != null && edge.to != null)
            {
                EditorGUILayout.LabelField("From:", edge.from.label);
                EditorGUILayout.LabelField("To:", edge.to.label);
            }

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            // Edge properties
            edge.bidirectional = EditorGUILayout.Toggle("Bidirectional:", edge.bidirectional);
            edge.isBlocked = EditorGUILayout.Toggle("Blocked:", edge.isBlocked);
            edge.hasSightline = EditorGUILayout.Toggle("Has Sightline:", edge.hasSightline);

            EditorGUILayout.Space();

            // Locks section
            DrawLocksEditor(edge);

            EditorGUILayout.Space();

            // Delete edge button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Delete Edge"))
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Edge",
                    "Are you sure you want to delete this edge?",
                    "Delete",
                    "Cancel"))
                {
                    if (template != null)
                    {
                        template.edges.Remove(edge);
                        _selectedEdge = null;
                        onChanged?.Invoke();
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            if (EditorGUI.EndChangeCheck())
            {
                onChanged?.Invoke();
            }
        }

        private void DrawLocksEditor(CycleEdge edge)
        {
            EditorGUILayout.LabelField("Required Keys (Locks)", EditorStyles.boldLabel);

            if (edge.requiredKeys == null)
                edge.requiredKeys = new List<int>();

            // Display existing locks
            for (int i = edge.requiredKeys.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                edge.requiredKeys[i] = EditorGUILayout.IntField($"Key {i + 1}:", edge.requiredKeys[i]);

                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    edge.requiredKeys.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add lock button
            if (GUILayout.Button("+ Add Lock"))
            {
                int newKeyId = edge.requiredKeys.Count > 0
                    ? edge.requiredKeys[edge.requiredKeys.Count - 1] + 1
                    : 1;
                edge.requiredKeys.Add(newKeyId);
            }
        }
    }
}