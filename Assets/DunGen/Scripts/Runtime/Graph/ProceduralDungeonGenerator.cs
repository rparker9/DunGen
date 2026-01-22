using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Procedurally generates dungeons by recursively applying cycle rewrites.
    /// FIXED: Deep-copies cycles so nodes aren't shared between template and replacements.
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

            // Deep-copy the root cycle (don't modify the template!)
            var rootCycle = DeepCopyCycle(rootTemplate.cycle);
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
                // Choose template: prefer authored replacementTemplate if present, otherwise random.
                var replacementTemplate = site.replacementTemplate != null
                    ? site.replacementTemplate
                    : _settings.GetRandomTemplate(_rng);

                if (replacementTemplate == null || replacementTemplate.cycle == null)
                    continue;

                // Deep-copy so each insertion gets unique nodes (your existing fix).
                var replacement = DeepCopyCycle(replacementTemplate.cycle);

                // Apply the rewrite (runtime-only field now)
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
        /// Deep-copy a cycle so each rewrite gets unique node instances.
        /// This prevents the "skipped duplicate" issue in CycleFlattener.
        /// </summary>
        private DungeonCycle DeepCopyCycle(DungeonCycle source)
        {
            if (source == null)
                return null;

            var copy = new DungeonCycle();
            copy.nodes.Clear();
            copy.edges.Clear();
            copy.rewriteSites.Clear();

            // Create mapping from old nodes to new nodes
            var nodeMap = new Dictionary<CycleNode, CycleNode>();

            // Deep copy all nodes
            if (source.nodes != null)
            {
                foreach (var oldNode in source.nodes)
                {
                    if (oldNode != null)
                    {
                        var newNode = DeepCopyNode(oldNode);
                        copy.nodes.Add(newNode);
                        nodeMap[oldNode] = newNode;
                    }
                }
            }

            // Deep copy all edges (with remapped node references)
            if (source.edges != null)
            {
                foreach (var oldEdge in source.edges)
                {
                    if (oldEdge != null && nodeMap.ContainsKey(oldEdge.from) && nodeMap.ContainsKey(oldEdge.to))
                    {
                        var newEdge = new CycleEdge(
                            nodeMap[oldEdge.from],
                            nodeMap[oldEdge.to],
                            oldEdge.bidirectional,
                            oldEdge.isBlocked,
                            oldEdge.hasSightline
                        );

                        // Copy locks
                        if (oldEdge.requiredKeys != null)
                        {
                            foreach (var keyId in oldEdge.requiredKeys)
                                newEdge.AddRequiredKey(keyId);
                        }

                        copy.edges.Add(newEdge);
                    }
                }
            }

            // Copy rewrite sites WITHOUT their replacement patterns
            // (replacements will be populated during generation)
            if (source.rewriteSites != null)
            {
                foreach (var site in source.rewriteSites)
                {
                    if (site != null && site.placeholder != null && nodeMap.ContainsKey(site.placeholder))
                    {
                        var newSite = new RewriteSite(nodeMap[site.placeholder]);
                        // Don't copy replacementPattern - it will be generated fresh
                        copy.rewriteSites.Add(newSite);
                    }
                }
            }

            // Remap start and goal references
            if (source.startNode != null && nodeMap.ContainsKey(source.startNode))
                copy.startNode = nodeMap[source.startNode];

            if (source.goalNode != null && nodeMap.ContainsKey(source.goalNode))
                copy.goalNode = nodeMap[source.goalNode];

            return copy;
        }

        /// <summary>
        /// Create a deep copy of a node
        /// </summary>
        private static CycleNode DeepCopyNode(CycleNode source)
        {
            var copy = new CycleNode();
            copy.label = source.label;

            // Copy keys
            if (source.grantedKeys != null)
            {
                foreach (var keyId in source.grantedKeys)
                    copy.AddGrantedKey(keyId);
            }

            // Copy roles
            if (source.roles != null)
            {
                foreach (var role in source.roles)
                {
                    if (role != null)
                        copy.AddRole(role.type);
                }
            }

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