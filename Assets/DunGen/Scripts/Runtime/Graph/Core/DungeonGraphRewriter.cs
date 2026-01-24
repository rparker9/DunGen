using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Dungeon grammar → graph pipeline.
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
    ///
    /// "Flat" means non-nested connectivity only (nodes+edges). Layout is a separate step.
    /// </summary>
    public static class DungeonGraphRewriter
    {
        /// <summary>
        /// One-shot pipeline: instantiate a nested cycle tree from <paramref name="settings"/> and
        /// then flatten it into a final connectivity graph.
        ///
        /// Use this when you want the graph that will be laid out / mapped to space.
        /// </summary>
        /// <param name="settings">Generation constraints + template pool.</param>
        /// <param name="seed">
        /// Seed used for template selection and rewrite decisions.
        /// If 0, falls back to settings.seed, then Environment.TickCount.
        /// </param>
        /// <returns>The final, non-nested connectivity graph (nodes+edges).</returns>
        public static FlatGraph CompileToFlatGraph(DungeonGenerationSettings settings, int seed)
        {
            var nested = CompileToNestedCycle(settings, seed);
            return FlattenToFlatGraph(nested);
        }

        /// <summary>
        /// One-shot pipeline, but also returns the nested cycle tree used to produce the flat graph.
        ///
        /// This is useful for editor tooling (hierarchical inspectors, debug views, provenance).
        /// </summary>
        /// <param name="settings">Generation constraints + template pool.</param>
        /// <param name="seed">Seed used for rewrite/template selection.</param>
        /// <param name="nestedRoot">The instantiated nested cycle tree (grammar derivation).</param>
        /// <returns>The final, non-nested connectivity graph (nodes+edges).</returns>
        public static FlatGraph CompileToFlatGraph(DungeonGenerationSettings settings, int seed, out DungeonCycle nestedRoot)
        {
            nestedRoot = CompileToNestedCycle(settings, seed);
            return FlattenToFlatGraph(nestedRoot);
        }

        /// <summary>
        /// Stage 1 only: Instantiate a nested <see cref="DungeonCycle"/> tree by choosing replacement
        /// templates and populating <see cref="RewriteSite.replacementPattern"/>.
        ///
        /// Note: this does NOT change graph connectivity. Placeholders remain in the parent cycle.
        /// Use <see cref="FlattenToFlatGraph"/> to apply the rewrite and produce final connectivity.
        /// </summary>
        public static DungeonCycle CompileToNestedCycle(DungeonGenerationSettings settings, int seed)
        {
            return InstantiateNestedCycle(settings, seed);
        }

        /// <summary>
        /// Stage 2 only: Flatten/splice a nested cycle tree into a single <see cref="FlatGraph"/>.
        ///
        /// Input must have replacements populated (RewriteSite.replacementPattern).
        /// If a replacement is missing entry/exit (startNode/goalNode), that site is skipped.
        /// </summary>
        public static FlatGraph FlattenToFlatGraph(DungeonCycle nestedRoot)
        {
            return RewriteToFlatGraph(nestedRoot);
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

            var root = CloneCycle(rootCycleLoaded);
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

                var replacement = CloneCycle(replacementLoaded);

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
        private static FlatGraph RewriteToFlatGraph(DungeonCycle root)
        {
            if (root == null)
                return FlatGraph.Empty();

            var nodes = new List<GraphNode>();
            var edges = new List<GraphEdge>();

            FlattenInto(root, nodes, edges);

            // Cleanup once at end (optional but defensive)
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                var e = edges[i];
                if (e == null || e.from == null || e.to == null)
                    edges.RemoveAt(i);
            }

            return new FlatGraph(nodes, edges);
        }
        private static void FlattenInto(DungeonCycle cycle, List<GraphNode> outNodes, List<GraphEdge> outEdges)
        {
            if (cycle == null)
                return;

            if (cycle.nodes != null) outNodes.AddRange(cycle.nodes);
            if (cycle.edges != null) outEdges.AddRange(cycle.edges);

            if (cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
                return;

            foreach (var site in cycle.rewriteSites)
            {
                if (site?.placeholder == null || site.replacementPattern == null)
                    continue;

                var placeholder = site.placeholder;
                var replacement = site.replacementPattern;

                FlattenInto(replacement, outNodes, outEdges);

                var entry = replacement.startNode;
                var exit = replacement.goalNode;

                if (entry == null || exit == null)
                    continue;

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
        // CLONING HELPERS (from your compiler)
        // =========================================================

        private static DungeonCycle CloneCycle(DungeonCycle source)
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
                        foreach (var keyId in oldEdge.requiredKeys)
                            newEdge.AddRequiredKey(keyId);
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
                foreach (var keyId in source.grantedKeys)
                    copy.AddGrantedKey(keyId);
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
