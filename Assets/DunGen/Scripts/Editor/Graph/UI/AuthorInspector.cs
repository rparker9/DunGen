using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Inspector panel for author mode.
    /// Displays node properties and allows editing keys, locks, roles, etc.
    /// UPDATED: Supports KeyIdentity and LockRequirement editing.
    /// </summary>
    public sealed class AuthorInspector
    {
        private GraphEdge _selectedEdge;
        private Vector2 _scrollPos;

        // Key/Lock editing state
        private KeyType _newKeyType = KeyType.Hard;
        private LockType _newLockType = LockType.Standard;

        // New key input fields
        private string _newKeyIdInput = "";
        private string _newKeyDisplayNameInput = "";
        private bool _newKeyIdInput_initialized = false;

        // New lock input fields
        private string _newLockRequiredKeyId = "key_1";

        public void SetSelectedEdge(GraphEdge edge)
        {
            _selectedEdge = edge;
        }

        public void ClearEdgeSelection()
        {
            _selectedEdge = null;
        }

        public void DrawInspector(
            Rect rect,
            DungeonCycle template,
            GraphNode selectedNode,
            System.Action onTemplateChanged)
        {
            GUILayout.BeginArea(rect);

            EditorGUILayout.LabelField("Author Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (selectedNode != null)
            {
                DrawNodeInspector(selectedNode, template, onTemplateChanged);
            }
            else if (_selectedEdge != null)
            {
                DrawEdgeInspector(_selectedEdge, onTemplateChanged);
            }
            else if (template != null)
            {
                DrawTemplateInspector(template, onTemplateChanged);
            }
            else
            {
                EditorGUILayout.HelpBox("No selection", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // =========================================================
        // NODE INSPECTOR
        // =========================================================

        private void DrawNodeInspector(GraphNode node, DungeonCycle template, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Label
            EditorGUILayout.LabelField("Label:", EditorStyles.boldLabel);
            string newLabel = EditorGUILayout.TextField(node.label);
            if (newLabel != node.label)
            {
                node.label = newLabel;
                onChanged?.Invoke();
            }

            EditorGUILayout.Space();

            // Roles
            DrawNodeRoles(node, template, onChanged);

            EditorGUILayout.Space();

            // Granted Keys
            DrawNodeKeys(node, onChanged);

            EditorGUILayout.Space();

            // Controls
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• Click node: Select\n" +
                "• Drag node: Move\n" +
                "• Shift+Click: Connect edge\n" +
                "• Delete: Remove node",
                MessageType.Info
            );
        }

        private void DrawNodeRoles(GraphNode node, DungeonCycle template, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Roles:", EditorStyles.boldLabel);

            if (node.roles == null || node.roles.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                for (int i = node.roles.Count - 1; i >= 0; i--)
                {
                    var role = node.roles[i];
                    if (role == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"• {role.type}");

                        if (GUILayout.Button("X", GUILayout.Width(30)))
                        {
                            node.RemoveRole(role.type);
                            onChanged?.Invoke();
                        }
                    }
                }
            }

            // Add role buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Start"))
                {
                    node.AddRole(NodeRoleType.Start);
                    if (template != null) template.startNode = node;
                    onChanged?.Invoke();
                }

                if (GUILayout.Button("+ Goal"))
                {
                    node.AddRole(NodeRoleType.Goal);
                    if (template != null) template.goalNode = node;
                    onChanged?.Invoke();
                }

                if (GUILayout.Button("+ Rewrite"))
                {
                    node.AddRole(NodeRoleType.RewriteSite);
                    if (template != null)
                    {
                        var site = new RewriteSite(node);
                        template.rewriteSites.Add(site);
                    }
                    onChanged?.Invoke();
                }
            }
        }

        private void DrawNodeKeys(GraphNode node, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Granted Keys:", EditorStyles.boldLabel);

            if (node.grantedKeys == null)
                node.grantedKeys = new List<KeyIdentity>();

            if (node.grantedKeys.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                for (int i = node.grantedKeys.Count - 1; i >= 0; i--)
                {
                    var key = node.grantedKeys[i];
                    if (key == null) continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Color indicator
                            var prevColor = GUI.backgroundColor;
                            GUI.backgroundColor = key.color;
                            GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                            GUI.backgroundColor = prevColor;

                            // Key type
                            EditorGUILayout.LabelField($"{key.type} Key", EditorStyles.boldLabel);

                            GUILayout.FlexibleSpace();

                            // Remove button
                            if (GUILayout.Button("X", GUILayout.Width(30)))
                            {
                                node.grantedKeys.RemoveAt(i);
                                onChanged?.Invoke();
                            }
                        }

                        // Editable fields
                        string newId = EditorGUILayout.TextField("Key ID:", key.globalId);
                        if (newId != key.globalId)
                        {
                            key.globalId = newId;
                            onChanged?.Invoke();
                        }

                        string newDisplayName = EditorGUILayout.TextField("Display Name:", key.displayName);
                        if (newDisplayName != key.displayName)
                        {
                            key.displayName = newDisplayName;
                            onChanged?.Invoke();
                        }

                        KeyType newType = (KeyType)EditorGUILayout.EnumPopup("Type:", key.type);
                        if (newType != key.type)
                        {
                            key.type = newType;
                            key.color = GetDefaultColorForKeyType(newType);
                            onChanged?.Invoke();
                        }

                        Color newColor = EditorGUILayout.ColorField("Color:", key.color);
                        if (newColor != key.color)
                        {
                            key.color = newColor;
                            onChanged?.Invoke();
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            // Add new key
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Add New Key", EditorStyles.boldLabel);

                // Temporary state for new key (stored as instance variables at class level)
                if (!_newKeyIdInput_initialized)
                {
                    _newKeyIdInput = $"key_{node.grantedKeys.Count + 1}";
                    _newKeyDisplayNameInput = $"Key {node.grantedKeys.Count + 1}";
                    _newKeyIdInput_initialized = true;
                }

                _newKeyType = (KeyType)EditorGUILayout.EnumPopup("Type:", _newKeyType);
                _newKeyIdInput = EditorGUILayout.TextField("Key ID:", _newKeyIdInput);
                _newKeyDisplayNameInput = EditorGUILayout.TextField("Display Name:", _newKeyDisplayNameInput);

                if (GUILayout.Button("Add Key"))
                {
                    var newKey = new KeyIdentity
                    {
                        globalId = _newKeyIdInput,
                        displayName = _newKeyDisplayNameInput,
                        type = _newKeyType,
                        color = GetDefaultColorForKeyType(_newKeyType)
                    };
                    node.grantedKeys.Add(newKey);

                    // Reset for next key
                    _newKeyIdInput = $"key_{node.grantedKeys.Count + 1}";
                    _newKeyDisplayNameInput = $"Key {node.grantedKeys.Count + 1}";

                    onChanged?.Invoke();
                }
            }
        }

        // =========================================================
        // EDGE INSPECTOR
        // =========================================================

        private void DrawEdgeInspector(GraphEdge edge, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Selected Edge", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Edge properties
            EditorGUILayout.LabelField("Properties:", EditorStyles.boldLabel);

            bool newBidirectional = EditorGUILayout.Toggle("Bidirectional:", edge.bidirectional);
            if (newBidirectional != edge.bidirectional)
            {
                edge.bidirectional = newBidirectional;
                onChanged?.Invoke();
            }

            bool newBlocked = EditorGUILayout.Toggle("Blocked:", edge.isBlocked);
            if (newBlocked != edge.isBlocked)
            {
                edge.isBlocked = newBlocked;
                onChanged?.Invoke();
            }

            bool newSightline = EditorGUILayout.Toggle("Has Sightline:", edge.hasSightline);
            if (newSightline != edge.hasSightline)
            {
                edge.hasSightline = newSightline;
                onChanged?.Invoke();
            }

            EditorGUILayout.Space();

            // Lock requirements
            DrawEdgeLocks(edge, onChanged);

            EditorGUILayout.Space();

            // Controls
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• Click edge: Select\n" +
                "• Delete: Remove edge",
                MessageType.Info
            );
        }

        private void DrawEdgeLocks(GraphEdge edge, System.Action onChanged)
        {
            EditorGUILayout.LabelField("Lock Requirements:", EditorStyles.boldLabel);

            if (edge.requiredKeys == null)
                edge.requiredKeys = new List<LockRequirement>();

            if (edge.requiredKeys.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                for (int i = edge.requiredKeys.Count - 1; i >= 0; i--)
                {
                    var req = edge.requiredKeys[i];
                    if (req == null) continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Color indicator
                            var prevColor = GUI.backgroundColor;
                            GUI.backgroundColor = req.color;
                            GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                            GUI.backgroundColor = prevColor;

                            // Lock type
                            EditorGUILayout.LabelField($"{req.type} Lock", EditorStyles.boldLabel);

                            GUILayout.FlexibleSpace();

                            // Remove button
                            if (GUILayout.Button("X", GUILayout.Width(30)))
                            {
                                edge.requiredKeys.RemoveAt(i);
                                onChanged?.Invoke();
                            }
                        }

                        // Editable fields
                        string newKeyId = EditorGUILayout.TextField("Required Key ID:", req.requiredKeyId);
                        if (newKeyId != req.requiredKeyId)
                        {
                            req.requiredKeyId = newKeyId;
                            onChanged?.Invoke();
                        }

                        LockType newType = (LockType)EditorGUILayout.EnumPopup("Type:", req.type);
                        if (newType != req.type)
                        {
                            req.type = newType;
                            req.color = GetDefaultColorForLockType(newType);
                            onChanged?.Invoke();
                        }

                        Color newColor = EditorGUILayout.ColorField("Color:", req.color);
                        if (newColor != req.color)
                        {
                            req.color = newColor;
                            onChanged?.Invoke();
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            // Add new lock
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Add New Lock", EditorStyles.boldLabel);

                // Initialize default values if needed
                if (string.IsNullOrEmpty(_newLockRequiredKeyId))
                {
                    _newLockRequiredKeyId = "key_1";
                }

                _newLockType = (LockType)EditorGUILayout.EnumPopup("Type:", _newLockType);
                _newLockRequiredKeyId = EditorGUILayout.TextField("Required Key ID:", _newLockRequiredKeyId);

                EditorGUILayout.HelpBox(
                    "Specify the Key ID this lock requires.\n" +
                    "Example: 'key_1', 'red_key', 'ability_swim'",
                    MessageType.Info
                );

                if (GUILayout.Button("Add Lock"))
                {
                    var newLock = new LockRequirement
                    {
                        requiredKeyId = _newLockRequiredKeyId,
                        type = _newLockType,
                        color = GetDefaultColorForLockType(_newLockType)
                    };
                    edge.requiredKeys.Add(newLock);
                    onChanged?.Invoke();
                }
            }
        }

        // =========================================================
        // TEMPLATE INSPECTOR
        // =========================================================

        private void DrawTemplateInspector(DungeonCycle template, System.Action onTemplateChanged)
        {
            EditorGUILayout.LabelField("Template Overview", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Nodes:", template.nodes?.Count.ToString() ?? "0");
            EditorGUILayout.LabelField("Edges:", template.edges?.Count.ToString() ?? "0");
            EditorGUILayout.LabelField("Rewrite Sites:", template.rewriteSites?.Count.ToString() ?? "0");

            EditorGUILayout.Space();

            if (template.startNode != null)
                EditorGUILayout.LabelField("Start Node:", template.startNode.label);
            else
                EditorGUILayout.HelpBox("No start node set", MessageType.Warning);

            if (template.goalNode != null)
                EditorGUILayout.LabelField("Goal Node:", template.goalNode.label);
            else
                EditorGUILayout.HelpBox("No goal node set", MessageType.Warning);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• Add Node: Click '+ Node' button\n" +
                "• Add Edge: Shift+Click two nodes\n" +
                "• Move Node: Drag node\n" +
                "• Delete: Select and press Delete",
                MessageType.Info
            );
        }

        // =========================================================
        // HELPER METHODS
        // =========================================================

        private Color GetDefaultColorForKeyType(KeyType type)
        {
            switch (type)
            {
                case KeyType.Hard: return new Color(1.0f, 0.85f, 0.3f);
                case KeyType.Soft: return new Color(0.5f, 0.85f, 1.0f);
                case KeyType.Ability: return new Color(0.5f, 1.0f, 0.5f);
                case KeyType.Item: return new Color(1.0f, 0.5f, 0.2f);
                case KeyType.Trigger: return new Color(0.8f, 0.3f, 1.0f);
                case KeyType.Narrative: return new Color(0.3f, 0.6f, 1.0f);
                default: return Color.yellow;
            }
        }

        private Color GetDefaultColorForLockType(LockType type)
        {
            switch (type)
            {
                case LockType.Standard: return new Color(0.95f, 0.4f, 0.2f);
                case LockType.Terrain: return new Color(0.6f, 0.3f, 0.1f);
                case LockType.Ability: return new Color(0.4f, 0.8f, 0.4f);
                case LockType.Puzzle: return new Color(0.7f, 0.3f, 0.9f);
                case LockType.OneWay: return new Color(1.0f, 0.6f, 0.0f);
                case LockType.Narrative: return new Color(0.3f, 0.5f, 0.9f);
                case LockType.Boss: return new Color(0.9f, 0.1f, 0.1f);
                default: return Color.red;
            }
        }
    }
}