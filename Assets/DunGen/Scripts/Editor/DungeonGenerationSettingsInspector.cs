using UnityEditor;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Custom inspector for DungeonGenerationSettings.
    /// Shows available templates from registry.
    /// </summary>
    [CustomEditor(typeof(DungeonGenerationSettings))]
    public class DungeonGenerationSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Show available templates section
            EditorGUILayout.LabelField("Template Registry", EditorStyles.boldLabel);

            var templates = TemplateRegistry.GetAll();

            if (templates == null || templates.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No templates found in registry.\n\n" +
                    "Create templates in: Tools > DunGen > Author Canvas\n" +
                    $"Templates folder: {TemplateRegistry.TEMPLATES_FOLDER}",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Found {templates.Count} template(s) in registry",
                    MessageType.Info
                );

                EditorGUILayout.Space();

                // List templates
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    foreach (var template in templates)
                    {
                        EditorGUILayout.LabelField($"• {template.name}", EditorStyles.miniLabel);
                    }
                }
            }

            EditorGUILayout.Space();

            // Refresh button
            if (GUILayout.Button("Refresh Registry"))
            {
                TemplateRegistry.Refresh();
                Repaint();
            }

            EditorGUILayout.Space();

            // Validation
            var settings = (DungeonGenerationSettings)target;
            if (settings.IsValid(out string error))
            {
                EditorGUILayout.HelpBox("[OK] Settings are valid", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"[!] Invalid: {error}", MessageType.Error);
            }
        }
    }
}