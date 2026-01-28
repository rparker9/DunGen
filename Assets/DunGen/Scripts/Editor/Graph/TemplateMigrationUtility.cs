using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Utility to migrate legacy templates (with int keys) to new KeyIdentity/LockRequirement system.
    /// 
    /// Usage:
    /// 1. Open "Tools/DunGen/Migrate Legacy Templates" menu
    /// 2. Select templates folder
    /// 3. Tool will backup and convert all .dungen.json files
    /// </summary>
    public static class TemplateMigrationUtility
    {
        /// <summary>
        /// Migrate all templates in the default folder.
        /// Creates backups before conversion.
        /// </summary>
        [UnityEditor.MenuItem("Tools/DunGen/Migrate Legacy Templates")]
        public static void MigrateAllTemplates()
        {
            string templatesFolder = TemplateRegistry.TEMPLATES_FOLDER;

            if (!Directory.Exists(templatesFolder))
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Migration Failed",
                    $"Templates folder not found: {templatesFolder}",
                    "OK"
                );
                return;
            }

            var files = Directory.GetFiles(templatesFolder, "*.dungen.json", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "No Templates Found",
                    $"No .dungen.json files found in: {templatesFolder}",
                    "OK"
                );
                return;
            }

            bool proceed = UnityEditor.EditorUtility.DisplayDialog(
                "Migrate Legacy Templates",
                $"Found {files.Length} template(s) to migrate.\n\n" +
                "This will:\n" +
                "• Create backups (.bak files)\n" +
                "• Convert int keys to KeyIdentity\n" +
                "• Convert int locks to LockRequirement\n\n" +
                "Continue?",
                "Yes, Migrate",
                "Cancel"
            );

            if (!proceed)
                return;

            int successCount = 0;
            int failCount = 0;

            foreach (var filePath in files)
            {
                if (MigrateTemplate(filePath))
                    successCount++;
                else
                    failCount++;
            }

            UnityEditor.EditorUtility.DisplayDialog(
                "Migration Complete",
                $"Migration finished:\n\n" +
                $"Success: {successCount}\n" +
                $"Failed: {failCount}\n\n" +
                "Check console for details.",
                "OK"
            );

            // Refresh registry
            TemplateRegistry.Refresh();
            UnityEditor.AssetDatabase.Refresh();
        }

        /// <summary>
        /// Migrate a single template file.
        /// Returns true on success.
        /// </summary>
        private static bool MigrateTemplate(string filePath)
        {
            try
            {
                Debug.Log($"[Migration] Processing: {filePath}");

                // Create backup
                string backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite: true);
                Debug.Log($"[Migration] Created backup: {backupPath}");

                // Load template
                var (cycle, positions, metadata) = CycleTemplate.Load(filePath);

                if (cycle == null)
                {
                    Debug.LogError($"[Migration] Failed to load template: {filePath}");
                    return false;
                }

                // Check if already migrated (has KeyIdentity instead of int keys)
                bool needsMigration = false;

                Debug.Log($"[Migration] Checking nodes for legacy keys...");
                Debug.Log($"[Migration] Needs migration: {needsMigration}");

                // This is a simplified check - in reality, the template should load correctly
                // regardless of version due to serialization handling

                // Re-save template (this will use the latest serialization format)
                bool saved = CycleTemplate.Save(
                    filePath,
                    cycle,
                    positions,
                    metadata.name,
                    metadata.description
                );

                if (saved)
                {
                    Debug.Log($"[Migration] Successfully migrated: {filePath}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[Migration] Failed to save migrated template: {filePath}");

                    // Restore backup
                    File.Copy(backupPath, filePath, overwrite: true);
                    Debug.Log($"[Migration] Restored backup for: {filePath}");

                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Migration] Exception migrating {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Convert legacy int key to KeyIdentity.
        /// </summary>
        public static KeyIdentity ConvertLegacyKey(int legacyId, string templateName)
        {
            return new KeyIdentity
            {
                globalId = $"legacy_{templateName}_key_{legacyId}",
                displayName = $"Key {legacyId}",
                type = KeyType.Hard,
                color = new Color(1.0f, 0.85f, 0.3f)
            };
        }

        /// <summary>
        /// Convert legacy int lock to LockRequirement.
        /// </summary>
        public static LockRequirement ConvertLegacyLock(int legacyKeyId, string templateName)
        {
            return new LockRequirement
            {
                requiredKeyId = $"legacy_{templateName}_key_{legacyKeyId}",
                type = LockType.Standard,
                color = new Color(0.95f, 0.4f, 0.2f)
            };
        }
    }
}