using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Editable inspector panel for Author Canvas.
    /// Allows editing node/edge properties, roles, keys, locks, and special properties.
    /// UPDATED: Added start/goal node controls and replacement template selector.
    /// </summary>
    public sealed class AuthorInspector
    {
        // Edge selection state
        private GraphEdge _selectedEdge;

        public GraphEdge SelectedEdge => _selectedEdge;

        public AuthorInspector()
        {
        }

        public void SetSelectedEdge(GraphEdge edge)
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
            GraphNode selectedNode,
            System.Action onTemplateChanged)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Author Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Template info
            if (currentTemplate != null)
            {
                EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Nodes:", currentTemplate.nodes.Count.ToString());
                EditorGUILayout.LabelField("Edges:", currentTemplate.edges.Count.ToString());

                // Show start/goal status
                string startLabel = currentTemplate.startNode != null ? currentTemplate.startNode.label : "(none)";
                string goalLabel = currentTemplate.goalNode != null ? currentTemplate.goalNode.label : "(none)";
                EditorGUILayout.LabelField("Start:", startLabel);
                EditorGUILayout.LabelField("Goal:", goalLabel);

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
                "- Click: Select node\n" +
                "- Drag: Move node\n" +
                "- Shift+Click: Connect nodes\n" +
                "- Delete: Remove node\n" +
                "- Escape: Cancel operation\n" +
                "- MMB: Pan\n" +
                "- Scroll: Zoom",
                MessageType.Info
            );

            GUILayout.EndArea();
        }

        // =========================================================
        // NODE EDITOR
        // =========================================================

        private void DrawNodeEditor(GraphNode node, DungeonCycle template, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // Editable label
            node.label = EditorGUILayout.TextField("Label:", node.label);

            EditorGUILayout.Space();

            // Special properties FIRST (start/goal/rewrite)
            DrawSpecialPropertiesEditor(node, template);

            EditorGUILayout.Space();

            // Keys section
            DrawKeysEditor(node);

            EditorGUILayout.Space();

            // Roles section
            DrawRolesEditor(node);

            EditorGUILayout.Space();

            // Connected edges (read-only)
            EditorGUILayout.LabelField("Connected Edges", EditorStyles.boldLabel);
            DrawConnectedEdges(node, template);

            if (EditorGUI.EndChangeCheck())
            {
                onChanged?.Invoke();
            }
        }

        private void DrawKeysEditor(GraphNode node)
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

        private void DrawRolesEditor(GraphNode node)
        {
            EditorGUILayout.LabelField("Additional Roles", EditorStyles.boldLabel);

            if (node.roles == null)
                node.roles = new List<NodeRole>();

            // Display existing roles (excluding special auto-managed roles)
            var specialRoles = new[] { NodeRoleType.Start, NodeRoleType.Goal, NodeRoleType.RewriteSite };

            for (int i = node.roles.Count - 1; i >= 0; i--)
            {
                if (node.roles[i] == null) continue;

                // Skip special roles (managed above)
                if (System.Array.IndexOf(specialRoles, node.roles[i].type) >= 0)
                    continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"- {node.roles[i].type}", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    node.roles.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add role dropdown
            EditorGUILayout.BeginHorizontal();

            // Get available roles (exclude Start/Goal/RewriteSite - they're managed above)
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

        private void DrawSpecialPropertiesEditor(GraphNode node, DungeonCycle template)
        {
            if (template == null)
                return;

            EditorGUILayout.LabelField("Special Properties", EditorStyles.boldLabel);

            // Start Node
            EditorGUI.BeginChangeCheck();
            bool isStartNode = template.startNode == node;
            bool shouldBeStartNode = EditorGUILayout.Toggle("Is Start Node:", isStartNode);

            if (EditorGUI.EndChangeCheck())
            {
                if (shouldBeStartNode && !isStartNode)
                {
                    // Set as start node (clear previous if any)
                    if (template.startNode != null)
                    {
                        // Remove Start role from old start node
                        template.startNode.roles.RemoveAll(r => r != null && r.type == NodeRoleType.Start);
                    }

                    template.startNode = node;

                    // Ensure Start role
                    if (!node.HasRole(NodeRoleType.Start))
                        node.AddRole(NodeRoleType.Start);
                }
                else if (!shouldBeStartNode && isStartNode)
                {
                    // Clear start node
                    template.startNode = null;

                    // Remove Start role
                    node.roles.RemoveAll(r => r != null && r.type == NodeRoleType.Start);
                }
            }

            // Goal Node
            EditorGUI.BeginChangeCheck();
            bool isGoalNode = template.goalNode == node;
            bool shouldBeGoalNode = EditorGUILayout.Toggle("Is Goal Node:", isGoalNode);

            if (EditorGUI.EndChangeCheck())
            {
                if (shouldBeGoalNode && !isGoalNode)
                {
                    // Set as goal node (clear previous if any)
                    if (template.goalNode != null)
                    {
                        // Remove Goal role from old goal node
                        template.goalNode.roles.RemoveAll(r => r != null && r.type == NodeRoleType.Goal);
                    }

                    template.goalNode = node;

                    // Ensure Goal role
                    if (!node.HasRole(NodeRoleType.Goal))
                        node.AddRole(NodeRoleType.Goal);
                }
                else if (!shouldBeGoalNode && isGoalNode)
                {
                    // Clear goal node
                    template.goalNode = null;

                    // Remove Goal role
                    node.roles.RemoveAll(r => r != null && r.type == NodeRoleType.Goal);
                }
            }

            // Rewrite Site
            if (template.rewriteSites == null)
                template.rewriteSites = new List<RewriteSite>();

            bool isRewriteSite = template.rewriteSites.Exists(s => s != null && s.placeholder == node);

            EditorGUI.BeginChangeCheck();
            bool shouldBeRewriteSite = EditorGUILayout.Toggle("Is Rewrite Site:", isRewriteSite);

            if (EditorGUI.EndChangeCheck())
            {
                if (shouldBeRewriteSite && !isRewriteSite)
                {
                    // Add as rewrite site
                    template.rewriteSites.Add(new RewriteSite(node));

                    // Ensure RewriteSite role
                    if (!node.HasRole(NodeRoleType.RewriteSite))
                        node.AddRole(NodeRoleType.RewriteSite);
                }
                else if (!shouldBeRewriteSite && isRewriteSite)
                {
                    // Remove from rewrite sites
                    template.rewriteSites.RemoveAll(s => s != null && s.placeholder == node);

                    // Remove RewriteSite role
                    node.roles.RemoveAll(r => r != null && r.type == NodeRoleType.RewriteSite);
                }
            }

            // Show replacement template selector if this is a rewrite site
            if (isRewriteSite)
            {
                var site = template.rewriteSites.Find(s => s != null && s.placeholder == node);
                if (site != null)
                {
                    DrawReplacementTemplateSelector(site);
                }
            }
        }

        private void DrawReplacementTemplateSelector(RewriteSite site)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Replacement Template", EditorStyles.miniBoldLabel);

            // Ensure template is loaded if GUID is set
            if (!string.IsNullOrEmpty(site.replacementTemplateGuid))
            {
                site.EnsureTemplateLoaded();
            }

            // Show current template if any
            if (site.replacementTemplate != null)
            {
                EditorGUILayout.LabelField("Current:", site.replacementTemplate.name);
                EditorGUILayout.LabelField("GUID:", site.replacementTemplate.guid);

                if (GUILayout.Button("Clear Template"))
                {
                    site.replacementTemplate = null;
                    site.replacementTemplateGuid = null;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Current:", "(none - will use random)");
            }

            // Select template button
            if (GUILayout.Button("Select Template..."))
            {
                var templates = TemplateRegistry.GetAll();

                if (templates.Count == 0)
                {
                    EditorUtility.DisplayDialog("No Templates", "No templates available. Create some templates first.", "OK");
                    return;
                }

                GenericMenu menu = new GenericMenu();

                foreach (var template in templates)
                {
                    string templateName = template.name;
                    menu.AddItem(new GUIContent(templateName), false, () =>
                    {
                        site.SetReplacementTemplate(template);
                    });
                }

                menu.ShowAsContext();
            }
        }

        private void DrawConnectedEdges(GraphNode node, DungeonCycle template)
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

                string direction = isOutgoing ? " -> " : " <- ";
                GraphNode otherNode = isOutgoing ? edge.to : edge.from;
                string otherLabel = otherNode != null && !string.IsNullOrEmpty(otherNode.label)
                    ? otherNode.label
                    : "?";

                string edgeDesc = $"{direction} {otherLabel}";

                var properties = new List<string>();

                if (edge.bidirectional)
                    properties.Add(" <-> ");
                if (edge.isBlocked)
                    properties.Add("BLOCKED");
                if (edge.RequiresAnyKey())
                    properties.Add($"REQD KEY {string.Join(",", edge.requiredKeys)}");

                if (properties.Count > 0)
                    edgeDesc += $" ({string.Join(", ", properties)})";

                // Make edges clickable - use actual button
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("-", GUILayout.Width(15));
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

        private void DrawEdgeEditor(GraphEdge edge, DungeonCycle template, System.Action onChanged)
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

        private void DrawLocksEditor(GraphEdge edge)
        {
            EditorGUILayout.LabelField("Required Keys (Locks)", EditorStyles.boldLabel);

            if (edge.requiredKeys == null)
                edge.requiredKeys = new List<int>();

            // Display existing locks
            for (int i = edge.requiredKeys.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                edge.requiredKeys[i] = EditorGUILayout.IntField($"Key {i + 1}:", edge.requiredKeys[i]);

                if (GUILayout.Button("×", GUILayout.Width(25)))
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