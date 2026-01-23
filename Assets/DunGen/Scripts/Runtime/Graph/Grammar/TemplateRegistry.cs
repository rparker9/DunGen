using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Central registry for all cycle templates.
    /// Replaces asset database lookups with simple file scanning.
    /// </summary>
    public static class TemplateRegistry
    {
        private static Dictionary<string, TemplateHandle> _templates = new Dictionary<string, TemplateHandle>();
        private static bool _initialized = false;

        public const string TEMPLATES_FOLDER = "Assets/Resources/Data/CycleTemplates";
        public const string FILE_EXTENSION = ".dungen.json";

        /// <summary>
        /// Initialize the registry by scanning for template files.
        /// </summary>
        public static void Initialize()
        {
            _templates.Clear();

            // Ensure directory exists
            if (!Directory.Exists(TEMPLATES_FOLDER))
            {
                Directory.CreateDirectory(TEMPLATES_FOLDER);
                Debug.Log($"[TemplateRegistry] Created templates folder: {TEMPLATES_FOLDER}");
            }

            // Scan for template files
            string[] files = Directory.GetFiles(TEMPLATES_FOLDER, $"*{FILE_EXTENSION}", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                try
                {
                    // Load metadata only (not full cycle)
                    string json = File.ReadAllText(filePath);
                    var file = JsonUtility.FromJson<CycleTemplate.TemplateFile>(json);

                    if (file != null && file.metadata != null)
                    {
                        var handle = new TemplateHandle(
                            file.metadata.guid,
                            file.metadata.name,
                            filePath
                        )
                        {
                            description = file.metadata.description
                        };

                        _templates[file.metadata.guid] = handle;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TemplateRegistry] Failed to load template metadata from: {filePath}\n{ex.Message}");
                }
            }

            _initialized = true;
            Debug.Log($"[TemplateRegistry] Initialized with {_templates.Count} templates");
        }

        /// <summary>
        /// Get all templates.
        /// </summary>
        public static List<TemplateHandle> GetAll()
        {
            if (!_initialized)
                Initialize();

            return _templates.Values.ToList();
        }

        /// <summary>
        /// Get template by GUID.
        /// </summary>
        public static TemplateHandle GetByGuid(string guid)
        {
            if (!_initialized)
                Initialize();

            _templates.TryGetValue(guid, out var handle);
            return handle;
        }

        /// <summary>
        /// Get template by name (first match).
        /// </summary>
        public static TemplateHandle GetByName(string name)
        {
            if (!_initialized)
                Initialize();

            return _templates.Values.FirstOrDefault(t => t.name == name);
        }

        /// <summary>
        /// Refresh registry (call after adding/removing template files).
        /// </summary>
        public static void Refresh()
        {
            Initialize();
        }

        /// <summary>
        /// Generate a unique file path for a new template.
        /// </summary>
        public static string GenerateFilePath(string templateName)
        {
            // Sanitize name for filename
            string safeName = SanitizeFilename(templateName);

            // Ensure unique
            string baseFilePath = Path.Combine(TEMPLATES_FOLDER, safeName + FILE_EXTENSION);
            string filePath = baseFilePath;
            int counter = 1;

            while (File.Exists(filePath))
            {
                filePath = Path.Combine(TEMPLATES_FOLDER, $"{safeName}_{counter}{FILE_EXTENSION}");
                counter++;
            }

            return filePath;
        }

        private static string SanitizeFilename(string name)
        {
            // Remove invalid filename characters
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = string.Join("_", name.Split(invalid));

            // Limit length
            if (safe.Length > 50)
                safe = safe.Substring(0, 50);

            return safe;
        }
    }
}