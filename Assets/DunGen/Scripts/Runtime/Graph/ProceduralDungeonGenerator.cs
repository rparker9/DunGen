using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Procedurally generates dungeons by recursively applying cycle rewrites.
    /// </summary>
    public class ProceduralDungeonGenerator
    {
        private DungeonGenerationSettings _settings;
        private System.Random _rng;
        private int _currentDepth;
        private int _totalNodes;

        public ProceduralDungeonGenerator(DungeonGenerationSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Generate a complete dungeon cycle with nested rewrites
        /// </summary>
        public DungeonCycle Generate(int seed = 0)
        {
            // Initialize RNG
            int actualSeed = seed != 0 ? seed : _settings.seed != 0 ? _settings.seed : Environment.TickCount;
            _rng = new System.Random(actualSeed);

            _currentDepth = 0;
            _totalNodes = 0;

            // Validate settings
            if (!_settings.IsValid(out string errorMessage))
            {
                Debug.LogError($"[ProceduralDungeonGenerator] Invalid settings: {errorMessage}");
                return null;
            }

            // Pick random starting template
            var rootTemplate = _settings.GetRandomTemplate(_rng);
            if (rootTemplate == null || rootTemplate.cycle == null)
            {
                Debug.LogError("[ProceduralDungeonGenerator] Failed to get root template");
                return null;
            }

            // Create a copy of the root cycle (don't modify the template)
            var rootCycle = CopyCycle(rootTemplate.cycle);
            _totalNodes = rootCycle.nodes != null ? rootCycle.nodes.Count : 0;

            Debug.Log($"[ProceduralDungeonGenerator] Starting with template '{rootTemplate.templateName}' ({_totalNodes} nodes)");

            // Recursively apply rewrites
            ApplyRewritesRecursive(rootCycle, 0);

            Debug.Log($"[ProceduralDungeonGenerator] Generation complete. Total nodes: {_totalNodes}, Depth: {_currentDepth}");

            return rootCycle;
        }

        /// <summary>
        /// Recursively apply rewrites to a cycle
        /// </summary>
        private void ApplyRewritesRecursive(DungeonCycle cycle, int depth)
        {
            if (cycle == null || cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
                return;

            // Stop if we've hit depth or node limit
            if (depth >= _settings.maxRewriteDepth)
            {
                Debug.Log($"[ProceduralDungeonGenerator] Max depth {_settings.maxRewriteDepth} reached");
                return;
            }

            if (_totalNodes >= _settings.maxNodes)
            {
                Debug.Log($"[ProceduralDungeonGenerator] Max nodes {_settings.maxNodes} reached");
                return;
            }

            // Shuffle rewrite sites for random order
            var shuffledSites = new List<RewriteSite>(cycle.rewriteSites);
            Shuffle(shuffledSites, _rng);

            // Apply rewrites up to max per cycle
            int rewritesApplied = 0;
            foreach (var site in shuffledSites)
            {
                if (site == null || site.placeholder == null)
                    continue;

                // Check limits
                if (rewritesApplied >= _settings.maxRewritesPerCycle)
                    break;

                if (_totalNodes >= _settings.maxNodes)
                    break;

                // Probabilistic rewrite
                if (_rng.NextDouble() > _settings.rewriteProbability)
                    continue;

                // Pick random replacement template
                var replacementTemplate = _settings.GetRandomTemplate(_rng);
                if (replacementTemplate == null || replacementTemplate.cycle == null)
                    continue;

                // Create a copy of the replacement
                var replacement = CopyCycle(replacementTemplate.cycle);

                // Apply the rewrite
                site.replacementPattern = replacement;
                rewritesApplied++;

                // Update node count (approximate - doesn't account for placeholder removal)
                int newNodes = replacement.nodes != null ? replacement.nodes.Count : 0;
                _totalNodes += newNodes;

                Debug.Log($"[ProceduralDungeonGenerator] Depth {depth}: Applied '{replacementTemplate.templateName}' to '{site.placeholder.label}' (+{newNodes} nodes)");

                // Recursively rewrite the replacement
                ApplyRewritesRecursive(replacement, depth + 1);
            }

            _currentDepth = Mathf.Max(_currentDepth, depth);
        }

        /// <summary>
        /// Create a shallow copy of a cycle (shares nodes/edges but new rewrite sites list)
        /// </summary>
        private DungeonCycle CopyCycle(DungeonCycle source)
        {
            var copy = new DungeonCycle();

            // Clear default nodes/edges
            copy.nodes.Clear();
            copy.edges.Clear();

            // Share nodes and edges (we don't need deep copies for generation)
            if (source.nodes != null)
            {
                foreach (var node in source.nodes)
                    copy.nodes.Add(node);
            }

            if (source.edges != null)
            {
                foreach (var edge in source.edges)
                    copy.edges.Add(edge);
            }

            // Copy rewrite sites (but not their replacements)
            copy.rewriteSites.Clear();
            if (source.rewriteSites != null)
            {
                foreach (var site in source.rewriteSites)
                {
                    if (site != null && site.placeholder != null)
                    {
                        // Create new site, no replacement yet
                        copy.rewriteSites.Add(new RewriteSite(site.placeholder));
                    }
                }
            }

            copy.startNode = source.startNode;
            copy.goalNode = source.goalNode;

            return copy;
        }

        /// <summary>
        /// Fisher-Yates shuffle
        /// </summary>
        private void Shuffle<T>(List<T> list, System.Random rng)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}