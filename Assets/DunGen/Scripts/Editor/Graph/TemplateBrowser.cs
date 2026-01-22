using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Browser window for viewing, loading, and managing cycle templates.
    /// FULLY UPDATED: Uses JSON file system with TemplateHandle and TemplateRegistry.
    /// </summary>
    public class TemplateBrowser : EditorWindow
    {
        private Vector2 _scrollPos;
        private TemplateHandle _selectedTemplate;
        private string _searchFilter = "";

        // Cache for template stats to avoid repeated deserialization
        private Dictionary<TemplateHandle, TemplateStats> _statsCache = new Dictionary<TemplateHandle, TemplateStats>();

        private class TemplateStats
        {
            public int nodeCount;
            public int edgeCount;
            public int rewriteSiteCount;
            public bool isValid;
            public string errorMessage;
        }

        [MenuItem("Tools/DunGen/Template Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<TemplateBrowser>("Template Browser");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            RefreshTemplateList();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space();

            DrawTemplateList();

            EditorGUILayout.Space();

            if (_selectedTemplate != null)
            {
                DrawTemplateDetails();
            }
        }

        // =========================================================
        // TOOLBAR
        // =========================================================

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    RefreshTemplateList();
                }

                GUILayout.Space(10);

                EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarTextField);

                if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchFilter = "";
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                var templates = TemplateRegistry.GetAll();
                EditorGUILayout.LabelField($"{templates.Count} templates", GUILayout.Width(100));
            }
        }

        // =========================================================
        // TEMPLATE LIST
        // =========================================================

        private void DrawTemplateList()
        {
            EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));

            var filteredTemplates = GetFilteredTemplates();

            if (filteredTemplates.Count == 0)
            {
                EditorGUILayout.HelpBox("No templates found. Create templates in Author Canvas.", MessageType.Info);
            }
            else
            {
                foreach (var template in filteredTemplates)
                {
                    DrawTemplateItem(template);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTemplateItem(TemplateHandle template)
        {
            bool isSelected = template == _selectedTemplate;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // Validation status indicator
                var stats = GetOrCreateStats(template);

                if (!stats.isValid)
                {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    GUILayout.Label("[!]", GUILayout.Width(30));
                    GUI.contentColor = prevColor;
                }
                else
                {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = Color.green;
                    GUILayout.Label("[OK]", GUILayout.Width(30));
                    GUI.contentColor = prevColor;
                }

                // Info
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(template.name, EditorStyles.boldLabel);

                    // Show stats from cache (avoids repeated deserialization)
                    string statsText = stats.isValid
                        ? $"Nodes: {stats.nodeCount}, Edges: {stats.edgeCount}, Rewrites: {stats.rewriteSiteCount}"
                        : "Invalid template";

                    var style = new GUIStyle(EditorStyles.miniLabel);
                    if (!stats.isValid)
                        style.normal.textColor = Color.red;

                    EditorGUILayout.LabelField(statsText, style);
                }

                GUILayout.FlexibleSpace();

                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    _selectedTemplate = template;
                }
            }
        }

        // =========================================================
        // TEMPLATE DETAILS
        // =========================================================

        private void DrawTemplateDetails()
        {
            EditorGUILayout.LabelField("Template Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Name:", _selectedTemplate.name);
                EditorGUILayout.LabelField("GUID:", _selectedTemplate.guid);
                EditorGUILayout.LabelField("File:", Path.GetFileName(_selectedTemplate.filePath));

                if (!string.IsNullOrEmpty(_selectedTemplate.description))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Description:");
                    EditorGUILayout.LabelField(_selectedTemplate.description, EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space();

                // Detailed stats
                var stats = GetOrCreateStats(_selectedTemplate);

                EditorGUILayout.LabelField("Nodes:", stats.nodeCount.ToString());
                EditorGUILayout.LabelField("Edges:", stats.edgeCount.ToString());
                EditorGUILayout.LabelField("Rewrite Sites:", stats.rewriteSiteCount.ToString());

                EditorGUILayout.Space();

                // Validation
                if (stats.isValid)
                {
                    EditorGUILayout.HelpBox("[OK] Template is valid", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"[!] Invalid: {stats.errorMessage}", MessageType.Error);
                }

                // Warning if edges are missing
                if (stats.isValid && stats.edgeCount == 0 && stats.nodeCount > 2)
                {
                    EditorGUILayout.HelpBox(
                        "[!] Template has nodes but no edges. This may indicate corruption. " +
                        "Try loading and re-saving in Author Canvas.",
                        MessageType.Warning
                    );
                }

                EditorGUILayout.Space();

                // Actions
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open JSON File"))
                    {
                        EditorUtility.RevealInFinder(_selectedTemplate.filePath);
                    }

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("Delete"))
                    {
                        DeleteTemplate(_selectedTemplate);
                    }
                    GUI.backgroundColor = Color.white;
                }

                // Additional actions for debugging
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Stats"))
                    {
                        _statsCache.Remove(_selectedTemplate);
                        Repaint();
                    }

                    if (GUILayout.Button("Validate JSON"))
                    {
                        ValidateTemplateJSON(_selectedTemplate);
                    }
                }
            }
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        private void RefreshTemplateList()
        {
            _statsCache.Clear(); // Clear cache when refreshing
            TemplateRegistry.Refresh(); // Reload registry from disk
        }

        private List<TemplateHandle> GetFilteredTemplates()
        {
            var allTemplates = TemplateRegistry.GetAll();

            if (string.IsNullOrEmpty(_searchFilter))
                return allTemplates;

            return allTemplates.Where(t =>
                t.name.ToLower().Contains(_searchFilter.ToLower()) ||
                (t.description != null && t.description.ToLower().Contains(_searchFilter.ToLower()))
            ).ToList();
        }

        private void DeleteTemplate(TemplateHandle template)
        {
            if (EditorUtility.DisplayDialog(
                "Delete Template",
                $"Are you sure you want to delete '{template.name}'?\nThis cannot be undone.",
                "Delete",
                "Cancel"))
            {
                try
                {
                    // Delete the JSON file
                    if (File.Exists(template.filePath))
                    {
                        File.Delete(template.filePath);
                        Debug.Log($"[TemplateBrowser] Deleted template file: {template.filePath}");

                        // Also delete .meta file if it exists
                        string metaPath = template.filePath + ".meta";
                        if (File.Exists(metaPath))
                        {
                            File.Delete(metaPath);
                        }
                    }

                    // Refresh registry
                    TemplateRegistry.Refresh();
                    _statsCache.Clear();

                    if (_selectedTemplate == template)
                    {
                        _selectedTemplate = null;
                    }

                    AssetDatabase.Refresh();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TemplateBrowser] Failed to delete template: {ex.Message}");
                    EditorUtility.DisplayDialog("Delete Failed", $"Failed to delete template:\n{ex.Message}", "OK");
                }
            }
        }

        private void ValidateTemplateJSON(TemplateHandle template)
        {
            try
            {
                var (cycle, positions, metadata) = CycleTemplateIO.Load(template.filePath);

                if (cycle == null)
                {
                    EditorUtility.DisplayDialog(
                        "Validation Failed",
                        "Failed to load template. Check console for details.",
                        "OK"
                    );
                    return;
                }

                string report = $"Template: {metadata.name}\n\n" +
                               $"Nodes: {cycle.nodes.Count}\n" +
                               $"Edges: {cycle.edges.Count}\n" +
                               $"Rewrite Sites: {cycle.rewriteSites.Count}\n" +
                               $"Positions: {positions?.Count ?? 0}\n\n" +
                               $"Start Node: {cycle.startNode?.label ?? "NULL"}\n" +
                               $"Goal Node: {cycle.goalNode?.label ?? "NULL"}";

                // Check for issues
                List<string> issues = new List<string>();

                if (cycle.edges.Count == 0 && cycle.nodes.Count > 2)
                    issues.Add("- No edges (possible corruption)");

                if (cycle.startNode == null)
                    issues.Add("- Missing start node");

                if (cycle.goalNode == null)
                    issues.Add("- Missing goal node");

                if (cycle.rewriteSites != null)
                {
                    foreach (var site in cycle.rewriteSites)
                    {
                        if (site.placeholder != null && !site.placeholder.HasRole(NodeRoleType.RewriteSite))
                            issues.Add($"- Rewrite site '{site.placeholder.label}' missing RewriteSite role");
                    }
                }

                if (issues.Count > 0)
                {
                    report += "\n\nISSUES FOUND:\n" + string.Join("\n", issues);
                }
                else
                {
                    report += "\n\n? No issues found";
                }

                EditorUtility.DisplayDialog("Template Validation", report, "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Validation Error",
                    $"Exception during validation:\n{ex.Message}",
                    "OK"
                );
            }
        }

        // =========================================================
        // STATS CACHING
        // =========================================================

        /// <summary>
        /// Get cached stats for a template, or compute and cache if not present.
        /// This avoids repeated deserialization which is expensive.
        /// </summary>
        private TemplateStats GetOrCreateStats(TemplateHandle template)
        {
            if (_statsCache.TryGetValue(template, out var cached))
                return cached;

            var stats = new TemplateStats();

            try
            {
                // Load template file
                var (cycle, positions, metadata) = CycleTemplateIO.Load(template.filePath);

                if (cycle != null)
                {
                    stats.isValid = true;
                    stats.nodeCount = cycle.nodes?.Count ?? 0;
                    stats.edgeCount = cycle.edges?.Count ?? 0;
                    stats.rewriteSiteCount = cycle.rewriteSites?.Count ?? 0;
                }
                else
                {
                    stats.isValid = false;
                    stats.errorMessage = "Failed to load cycle data";
                }
            }
            catch (System.Exception ex)
            {
                stats.isValid = false;
                stats.errorMessage = ex.Message;
            }

            // Cache for next time
            _statsCache[template] = stats;

            return stats;
        }
    }
}