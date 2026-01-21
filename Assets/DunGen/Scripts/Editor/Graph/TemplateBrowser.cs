using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Browser window for viewing, loading, and managing cycle templates.
    /// </summary>
    public class TemplateBrowser : EditorWindow
    {
        private List<CycleTemplate> _templates = new List<CycleTemplate>();
        private Vector2 _scrollPos;
        private CycleTemplate _selectedTemplate;
        private string _searchFilter = "";

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

                EditorGUILayout.LabelField($"{_templates.Count} templates", GUILayout.Width(100));
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

        private void DrawTemplateItem(CycleTemplate template)
        {
            bool isSelected = template == _selectedTemplate;

            Color bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : Color.clear;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // Thumbnail
                Texture2D thumbnail = template.thumbnail != null ? template.thumbnail : EditorGUIUtility.whiteTexture;
                GUILayout.Label(thumbnail, GUILayout.Width(50), GUILayout.Height(50));

                // Info
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(template.templateName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Type: {template.cycleType}");

                    if (template.cycle != null)
                    {
                        int nodeCount = template.cycle.nodes != null ? template.cycle.nodes.Count : 0;
                        int edgeCount = template.cycle.edges != null ? template.cycle.edges.Count : 0;
                        EditorGUILayout.LabelField($"Nodes: {nodeCount}, Edges: {edgeCount}", EditorStyles.miniLabel);
                    }
                }

                GUILayout.FlexibleSpace();

                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    _selectedTemplate = template;
                    Selection.activeObject = template;
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
                EditorGUILayout.LabelField("Name:", _selectedTemplate.templateName);
                EditorGUILayout.LabelField("Type:", _selectedTemplate.cycleType.ToString());

                if (!string.IsNullOrEmpty(_selectedTemplate.description))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Description:");
                    EditorGUILayout.LabelField(_selectedTemplate.description, EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space();

                // Validation
                bool isValid = _selectedTemplate.IsValid(out string errorMessage);
                if (isValid)
                {
                    EditorGUILayout.HelpBox("Template is valid", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Invalid: {errorMessage}", MessageType.Error);
                }

                EditorGUILayout.Space();

                // Actions
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load in Author"))
                    {
                        LoadInAuthor(_selectedTemplate);
                    }

                    if (GUILayout.Button("Load in Preview"))
                    {
                        LoadInPreview(_selectedTemplate);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping in Project"))
                    {
                        EditorGUIUtility.PingObject(_selectedTemplate);
                    }

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("Delete"))
                    {
                        DeleteTemplate(_selectedTemplate);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
        }

        // =========================================================
        // ACTIONS
        // =========================================================

        private void RefreshTemplateList()
        {
            _templates.Clear();

            string[] guids = AssetDatabase.FindAssets("t:CycleTemplate");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<CycleTemplate>(path);

                if (template != null)
                {
                    _templates.Add(template);
                }
            }

            // Sort by name
            _templates = _templates.OrderBy(t => t.templateName).ToList();
        }

        private List<CycleTemplate> GetFilteredTemplates()
        {
            if (string.IsNullOrEmpty(_searchFilter))
                return _templates;

            return _templates.Where(t =>
                t.templateName.ToLower().Contains(_searchFilter.ToLower()) ||
                t.cycleType.ToString().ToLower().Contains(_searchFilter.ToLower()) ||
                (t.description != null && t.description.ToLower().Contains(_searchFilter.ToLower()))
            ).ToList();
        }

        private void LoadInAuthor(CycleTemplate template)
        {
            var window = GetWindow<DunGenAuthorCanvas>("DunGen Author");
            window.Focus();

            // TODO: Add public method to Author Canvas to load template
            EditorUtility.DisplayDialog("Load in Author",
                "Use 'Load' button in Author Canvas toolbar to load this template.",
                "OK");
        }

        private void LoadInPreview(CycleTemplate template)
        {
            var window = GetWindow<DunGenPreviewCanvas>("DunGen Preview");
            window.Focus();

            // TODO: Add public method to Preview Canvas to load template
            EditorUtility.DisplayDialog("Load in Preview",
                "Use 'Load Template' button in Preview Canvas toolbar to load this template.",
                "OK");
        }

        private void DeleteTemplate(CycleTemplate template)
        {
            if (EditorUtility.DisplayDialog(
                "Delete Template",
                $"Are you sure you want to delete '{template.templateName}'?\nThis cannot be undone.",
                "Delete",
                "Cancel"))
            {
                string path = AssetDatabase.GetAssetPath(template);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();

                _templates.Remove(template);

                if (_selectedTemplate == template)
                {
                    _selectedTemplate = null;
                }
            }
        }
    }
}