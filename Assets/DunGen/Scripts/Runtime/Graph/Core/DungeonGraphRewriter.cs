using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Dungeon grammar → graph pipeline with unique key/lock management.
    ///
    /// This class owns BOTH stages:
    /// 1) Grammar instantiation (nested):
    ///    Chooses replacement templates and assigns RewriteSite.replacementPattern,
    ///    producing a nested <see cref="DungeonCycle"/> tree.
    ///
    /// 2) Rewrite application (flatten):
    ///    Splices all replacement patterns into a single <see cref="FlatGraph"/> by:
    ///    - removing rewrite placeholders
    ///    - reconnecting incoming edges to replacement.startNode
    ///    - reconnecting outgoing edges from replacement.goalNode
    ///    - remapping all keys to globally unique IDs via KeyRegistry
    ///
    /// "Flat" means non-nested connectivity only (nodes+edges). Layout is a separate step.
    /// </summary>
    public static class DungeonGraphRewriter
    {
        /// <summary>
        /// One-shot pipeline: instantiate a nested cycle tree from <paramref name="settings"/> and
        /// then flatten it into a final connectivity graph with unique keys.
        /// </summary>
        public static FlatGraph CompileToFlatGraph(DungeonGenerationSettings settings, int seed)
        {
            var nested = CompileToNestedCycle(settings, seed);
            return FlattenToFlatGraph(nested);
        }

        /// <summary>
        /// One-shot pipeline, but also returns the nested cycle tree and key registry.
        /// </summary>
        public static FlatGraph CompileToFlatGraph(DungeonGenerationSettings settings, int seed, out DungeonCycle nestedRoot, out KeyRegistry keyRegistry)
        {
            nestedRoot = CompileToNestedCycle(settings, seed);

            // Create key registry for this generation
            keyRegistry = new KeyRegistry();

            return FlattenToFlatGraph(nestedRoot, keyRegistry);
        }

        /// <summary>
        /// Stage 1 only: Instantiate a nested <see cref="DungeonCycle"/> tree.
        /// </summary>
        public static DungeonCycle CompileToNestedCycle(DungeonGenerationSettings settings, int seed)
        {
            return InstantiateNestedCycle(settings, seed);
        }

        /// <summary>
        /// Stage 2 only: Flatten/splice a nested cycle tree into a single <see cref="FlatGraph"/>.
        /// </summary>
        public static FlatGraph FlattenToFlatGraph(DungeonCycle nestedRoot)
        {
            var keyRegistry = new KeyRegistry();
            return FlattenToFlatGraph(nestedRoot, keyRegistry);
        }

        /// <summary>
        /// Stage 2 with explicit key registry for tracking unique keys.
        /// </summary>
        public static FlatGraph FlattenToFlatGraph(DungeonCycle nestedRoot, KeyRegistry keyRegistry)
        {
            return RewriteToFlatGraph(nestedRoot, keyRegistry);
        }

        // =========================================================
        // PHASE A: Grammar instantiation (nested cycle tree)
        // =========================================================

        private static DungeonCycle InstantiateNestedCycle(DungeonGenerationSettings settings, int seed)
        {
            if (settings == null)
            {
                Debug.LogError("[DungeonGraphRewriter] Settings is null.");
                return null;
            }

            if (!settings.IsValid(out string errorMessage))
            {
                Debug.LogError($"[DungeonGraphRewriter] Invalid settings: {errorMessage}");
                return null;
            }

            int actualSeed = seed != 0 ? seed : settings.seed != 0 ? settings.seed : Environment.TickCount;
            var rng = new System.Random(actualSeed);

            int totalNodes = 0;
            int maxDepthReached = 0;

            var rootTemplateHandle = settings.GetRandomTemplate(rng);
            if (rootTemplateHandle == null)
            {
                Debug.LogError("[DungeonGraphRewriter] Failed to get root template");
                return null;
            }

            var (rootCycleLoaded, _) = rootTemplateHandle.Load();
            if (rootCycleLoaded == null)
            {
                Debug.LogError($"[DungeonGraphRewriter] Failed to load cycle from template '{rootTemplateHandle.name}'");
                return null;
            }

            // Clone root WITHOUT key registry (template keys stay as-is during nesting)
            var root = CloneCycleForNesting(rootCycleLoaded);
            totalNodes = root.nodes != null ? root.nodes.Count : 0;

            InstantiateRewriteSitesRecursive(settings, rng, root, depth: 0, ref totalNodes, ref maxDepthReached);

            Debug.Log($"[DungeonGraphRewriter] Nested generation complete. Total nodes: {totalNodes}, Max depth: {maxDepthReached}");
            return root;
        }

        private static void InstantiateRewriteSitesRecursive(
            DungeonGenerationSettings settings,
            System.Random rng,
            DungeonCycle cycle,
            int depth,
            ref int totalNodes,
            ref int maxDepthReached)
        {
            if (cycle == null || cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
                return;

            if (depth >= settings.maxRewriteDepth)
                return;

            if (totalNodes >= settings.maxNodes)
                return;

            var shuffledSites = new List<RewriteSite>(cycle.rewriteSites);
            Shuffle(shuffledSites, rng);

            int rewritesApplied = 0;
            foreach (var site in shuffledSites)
            {
                if (site?.placeholder == null)
                    continue;

                if (rewritesApplied >= settings.maxRewritesPerCycle)
                    break;

                if (totalNodes >= settings.maxNodes)
                    break;

                if (rng.NextDouble() > settings.rewriteProbability)
                    continue;

                TemplateHandle replacementHandle = null;

                if (site.HasReplacementTemplate())
                {
                    site.EnsureTemplateLoaded();
                    replacementHandle = site.replacementTemplate;
                }

                if (replacementHandle == null)
                    replacementHandle = settings.GetRandomTemplate(rng);

                if (replacementHandle == null)
                    continue;

                var (replacementLoaded, _) = replacementHandle.Load();
                if (replacementLoaded == null)
                    continue;

                var replacement = CloneCycleForNesting(replacementLoaded);

                site.replacementPattern = replacement;
                rewritesApplied++;

                int newNodes = replacement.nodes != null ? replacement.nodes.Count : 0;
                totalNodes += newNodes;

                InstantiateRewriteSitesRecursive(settings, rng, replacement, depth + 1, ref totalNodes, ref maxDepthReached);
            }

            if (depth > maxDepthReached)
                maxDepthReached = depth;
        }

        // =========================================================
        // PHASE B: Rewrite application (splice nested -> flat)
        // =========================================================

        private static FlatGraph RewriteToFlatGraph(DungeonCycle root, KeyRegistry keyRegistry)
        {
            if (root == null)
                return FlatGraph.Empty();

            var nodes = new List<GraphNode>();
            var edges = new List<GraphEdge>();

            FlattenInto(root, nodes, edges, keyRegistry);

            // Cleanup once at end (optional but defensive)
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                var e = edges[i];
                if (e == null || e.from == null || e.to == null)
                    edges.RemoveAt(i);
            }

            Debug.Log($"[DungeonGraphRewriter] Flattened to {nodes.Count} nodes, {edges.Count} edges, {keyRegistry.KeyCount} unique keys");
            keyRegistry.DebugPrintKeys();

            return new FlatGraph(nodes, edges);
        }

        private static void FlattenInto(DungeonCycle cycle, List<GraphNode> outNodes, List<GraphEdge> outEdges, KeyRegistry keyRegistry)
        {
            if (cycle == null)
                return;

            // Add this cycle's nodes with remapped keys
            if (cycle.nodes != null)
            {
                foreach (var node in cycle.nodes)
                {
                    if (node != null)
                    {
                        // Remap keys to globally unique IDs
                        RemapNodeKeys(node, keyRegistry, cycle.GetType().Name);
                        outNodes.Add(node);
                    }
                }
            }

            // Add this cycle's edges with remapped locks
            if (cycle.edges != null)
            {
                foreach (var edge in cycle.edges)
                {
                    if (edge != null)
                    {
                        // Remap lock requirements to global key IDs
                        RemapEdgeLocks(edge, keyRegistry);
                        outEdges.Add(edge);
                    }
                }
            }

            if (cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
                return;

            foreach (var site in cycle.rewriteSites)
            {
                if (site?.placeholder == null || site.replacementPattern == null)
                    continue;

                var placeholder = site.placeholder;
                var replacement = site.replacementPattern;

                // Recursively flatten the replacement
                FlattenInto(replacement, outNodes, outEdges, keyRegistry);

                var entry = replacement.startNode;
                var exit = replacement.goalNode;

                if (entry == null || exit == null)
                    continue;

                // Splice: redirect edges from placeholder to replacement entry/exit
                for (int i = 0; i < outEdges.Count; i++)
                {
                    var e = outEdges[i];
                    if (e == null) continue;

                    if (e.to == placeholder) e.to = entry;
                    if (e.from == placeholder) e.from = exit;
                }

                outNodes.Remove(placeholder);
            }
        }

        // =========================================================
        // KEY REMAPPING HELPERS
        // =========================================================

        /// <summary>
        /// Remap node's granted keys to globally unique IDs.
        /// Modifies the node in-place.
        /// </summary>
        private static void RemapNodeKeys(GraphNode node, KeyRegistry keyRegistry, string templateName)
        {
            if (node == null || node.grantedKeys == null || node.grantedKeys.Count == 0)
                return;

            var remappedKeys = new List<KeyIdentity>();

            foreach (var templateKey in node.grantedKeys)
            {
                if (templateKey == null)
                    continue;

                // Register this template key as a new global key
                var globalKey = keyRegistry.RegisterKey(
                    templateKey.globalId ?? templateKey.displayName,
                    templateName,
                    templateKey.type,
                    templateKey.displayName
                );

                // Preserve any template-specific metadata
                if (templateKey.metadata != null)
                {
                    foreach (var kvp in templateKey.metadata)
                    {
                        globalKey.metadata[kvp.Key] = kvp.Value;
                    }
                }

                remappedKeys.Add(globalKey);
            }

            node.grantedKeys = remappedKeys;
        }

        /// <summary>
        /// Remap edge's lock requirements to reference global key IDs.
        /// Modifies the edge in-place.
        /// </summary>
        private static void RemapEdgeLocks(GraphEdge edge, KeyRegistry keyRegistry)
        {
            if (edge == null || edge.requiredKeys == null || edge.requiredKeys.Count == 0)
                return;

            // For now, we can't remap locks yet because we don't know which global key they should reference.
            // This requires a more sophisticated approach where we track key mappings during cloning.
            // For MVP, we'll leave locks as-is and add a TODO.

            // TODO: Implement lock remapping once we have a key mapping table from template -> global IDs
        }

        // =========================================================
        // CLONING HELPERS (for nesting phase)
        // =========================================================

        /// <summary>
        /// Clone a cycle for nesting (before flattening).
        /// Template keys are preserved as-is (not yet remapped to global IDs).
        /// </summary>
        private static DungeonCycle CloneCycleForNesting(DungeonCycle source)
        {
            if (source == null)
                return null;

            var copy = new DungeonCycle();
            copy.nodes.Clear();
            copy.edges.Clear();
            copy.rewriteSites.Clear();

            var nodeMap = new Dictionary<GraphNode, GraphNode>();

            if (source.nodes != null)
            {
                foreach (var oldNode in source.nodes)
                {
                    if (oldNode == null) continue;
                    var newNode = CloneNode(oldNode);
                    copy.nodes.Add(newNode);
                    nodeMap[oldNode] = newNode;
                }
            }

            if (source.edges != null)
            {
                foreach (var oldEdge in source.edges)
                {
                    if (oldEdge == null) continue;
                    if (!nodeMap.TryGetValue(oldEdge.from, out var from)) continue;
                    if (!nodeMap.TryGetValue(oldEdge.to, out var to)) continue;

                    var newEdge = new GraphEdge(from, to, oldEdge.bidirectional, oldEdge.isBlocked, oldEdge.hasSightline);

                    if (oldEdge.requiredKeys != null)
                    {
                        foreach (var req in oldEdge.requiredKeys)
                        {
                            if (req != null)
                                newEdge.requiredKeys.Add(req.Clone());
                        }
                    }

                    copy.edges.Add(newEdge);
                }
            }

            if (source.rewriteSites != null)
            {
                foreach (var site in source.rewriteSites)
                {
                    if (site?.placeholder == null) continue;
                    if (!nodeMap.TryGetValue(site.placeholder, out var ph)) continue;

                    var newSite = new RewriteSite(ph);
                    newSite.replacementTemplateGuid = site.replacementTemplateGuid;
                    copy.rewriteSites.Add(newSite);
                }
            }

            if (source.startNode != null && nodeMap.TryGetValue(source.startNode, out var start))
                copy.startNode = start;

            if (source.goalNode != null && nodeMap.TryGetValue(source.goalNode, out var goal))
                copy.goalNode = goal;

            return copy;
        }

        private static GraphNode CloneNode(GraphNode source)
        {
            var copy = new GraphNode();
            copy.label = source.label;

            if (source.grantedKeys != null)
            {
                foreach (var key in source.grantedKeys)
                {
                    if (key != null)
                        copy.grantedKeys.Add(key.Clone());
                }
            }

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

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}