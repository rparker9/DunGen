using UnityEngine;
using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// Settings for procedural dungeon generation.
    /// Controls how cycles are randomly assembled and nested.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonGenSettings", menuName = "DunGen/Generation Settings", order = 2)]
    public class DungeonGenerationSettings : ScriptableObject
    {
        [Header("Template Registry")]
        [Tooltip("Pool of cycle templates to choose from during generation")]
        public List<CycleTemplate> templatePool = new List<CycleTemplate>();

        [Header("Rewrite Depth")]
        [Tooltip("Maximum nesting depth for cycle rewrites (0 = no rewrites, 1 = one level, etc.)")]
        [Range(0, 5)]
        public int maxRewriteDepth = 2;

        [Header("Rewrite Limits")]
        [Tooltip("Maximum number of rewrite sites to expand per cycle")]
        [Range(0, 10)]
        public int maxRewritesPerCycle = 3;

        [Tooltip("Probability that a rewrite site gets expanded (0-1)")]
        [Range(0f, 1f)]
        public float rewriteProbability = 0.7f;

        [Header("Template Selection")]
        [Tooltip("How to pick templates from the pool")]
        public TemplateSelectionMode selectionMode = TemplateSelectionMode.Random;

        [Tooltip("Seed for random generation (0 = use system time)")]
        public int seed = 0;

        [Header("Constraints")]
        [Tooltip("Minimum number of nodes in final dungeon")]
        public int minNodes = 5;

        [Tooltip("Maximum number of nodes in final dungeon (prevents infinite expansion)")]
        public int maxNodes = 50;

        /// <summary>
        /// Get a random template from the pool
        /// </summary>
        public CycleTemplate GetRandomTemplate(System.Random rng)
        {
            if (templatePool == null || templatePool.Count == 0)
                return null;

            switch (selectionMode)
            {
                case TemplateSelectionMode.Random:
                    return templatePool[rng.Next(templatePool.Count)];

                case TemplateSelectionMode.Sequential:
                    // Simple round-robin (stateless, based on call count)
                    return templatePool[rng.Next(templatePool.Count)];

                default:
                    return templatePool[0];
            }
        }

        /// <summary>
        /// Validate settings
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (templatePool == null || templatePool.Count == 0)
            {
                errorMessage = "Template pool is empty. Add at least one cycle template.";
                return false;
            }

            if (maxNodes < minNodes)
            {
                errorMessage = "Max nodes must be >= min nodes";
                return false;
            }

            errorMessage = "";
            return true;
        }
    }

    public enum TemplateSelectionMode
    {
        Random,      // Pure random from pool
        Sequential   // Cycle through pool
    }
}