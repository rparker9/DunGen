using System;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// A rewrite site: a placeholder node that can be rewritten by inserting a subcycle.
    ///
    /// UPDATED: Uses TemplateHandle instead of ScriptableObject reference.
    /// </summary>
    [Serializable]
    public sealed class RewriteSite
    {
        public CycleNode placeholder;

        // Template GUID (used during serialization)
        public string replacementTemplateGuid;

        // Cached handle (loaded from GUID)
        [NonSerialized]
        public TemplateHandle replacementTemplate;

        // Runtime replacement (populated during generation)
        [NonSerialized]
        public DungeonCycle replacementPattern;

        public RewriteSite(CycleNode placeholder)
        {
            this.placeholder = placeholder;
        }

        /// <summary>
        /// Check if this site has a template assigned.
        /// </summary>
        public bool HasReplacementTemplate()
        {
            return !string.IsNullOrEmpty(replacementTemplateGuid);
        }

        /// <summary>
        /// Check if this site has a runtime pattern (generated).
        /// </summary>
        public bool HasReplacementPattern()
        {
            return replacementPattern != null;
        }

        /// <summary>
        /// Load the template handle if not already loaded.
        /// </summary>
        public void EnsureTemplateLoaded()
        {
            if (replacementTemplate == null && !string.IsNullOrEmpty(replacementTemplateGuid))
            {
                replacementTemplate = TemplateRegistry.GetByGuid(replacementTemplateGuid);

                if (replacementTemplate == null)
                {
                    Debug.LogWarning($"[RewriteSite] Could not find template with GUID: {replacementTemplateGuid}");
                }
            }
        }

        /// <summary>
        /// Set the replacement template (updates GUID).
        /// </summary>
        public void SetReplacementTemplate(TemplateHandle template)
        {
            replacementTemplate = template;
            replacementTemplateGuid = template?.guid;
        }
    }
}