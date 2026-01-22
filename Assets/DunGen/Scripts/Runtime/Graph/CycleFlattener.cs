using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Flattens a nested cycle structure into a flat graph.
    /// UPDATED: Now performs rewrite splicing:
    /// - A rewrite site's placeholder node is REMOVED.
    /// - The replacement pattern is inserted.
    /// - Incoming edges are reconnected to the replacement's entrance (startNode).
    /// - Outgoing edges are reconnected from the replacement's exit (goalNode).
    /// </summary>
    public static class CycleFlattener
    {
        /// <summary>
        /// Flatten a cycle with nested replacements into a single flat graph,
        /// splicing each replacement so it cleanly replaces the placeholder node.
        /// </summary>
        public static FlatGraph FlattenNestedCycle(DungeonCycle rootCycle)
        {
            if (rootCycle == null)
                return new FlatGraph(new List<CycleNode>(), new List<CycleEdge>());

            // Start with a shallow copy of the root nodes (node instances are shared intentionally).
            var nodes = new List<CycleNode>(rootCycle.nodes ?? new List<CycleNode>());

            // Clone edges so we can safely remove/replace without mutating authored cycles.
            var edges = CloneEdges(rootCycle.edges);

            // Splice all rewrites recursively.
            var processedSites = new HashSet<RewriteSite>();
            SpliceRewritesRecursive(rootCycle, nodes, edges, processedSites);

            return new FlatGraph(nodes, edges);
        }

        // =========================================================
        // REWRITE SPLICING
        // =========================================================

        private static void SpliceRewritesRecursive(
            DungeonCycle cycle,
            List<CycleNode> nodes,
            List<CycleEdge> edges,
            HashSet<RewriteSite> processedSites)
        {
            if (cycle == null || cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
                return;

            foreach (var site in cycle.rewriteSites)
            {
                if (site == null || site.placeholder == null)
                    continue;

                if (!processedSites.Add(site))
                    continue;

                if (!site.HasReplacementPattern() || site.replacementPattern == null)
                    continue;

                // First: recursively splice inside the replacement.
                // (So the inserted pattern is already "clean" internally.)
                var replacementFlat = FlattenNestedCycle(site.replacementPattern);

                // Determine entrance/exit for splicing.
                var entrance = site.replacementPattern.startNode;
                var exit = site.replacementPattern.goalNode;

                if (entrance == null || exit == null)
                {
                    Debug.LogWarning(
                        $"[CycleFlattener] Replacement missing start/goal; cannot splice placeholder '{site.placeholder.label}'.");
                    continue;
                }

                // Step 2.5: snapshot the placeholder's previous connections BEFORE removal.
                var incoming = new List<CycleEdge>();
                var outgoing = new List<CycleEdge>();

                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    if (e == null) continue;

                    if (e.to == site.placeholder) incoming.Add(e);
                    if (e.from == site.placeholder) outgoing.Add(e);
                }

                // Remove all edges touching the placeholder.
                edges.RemoveAll(e => e != null && (e.from == site.placeholder || e.to == site.placeholder));

                // Remove the placeholder node itself.
                nodes.Remove(site.placeholder);

                // Merge replacement nodes/edges (avoid duplicates defensively).
                if (replacementFlat.nodes != null)
                {
                    foreach (var n in replacementFlat.nodes)
                    {
                        if (n != null && !nodes.Contains(n))
                            nodes.Add(n);
                    }
                }

                if (replacementFlat.edges != null)
                {
                    foreach (var e in replacementFlat.edges)
                    {
                        if (e != null)
                            edges.Add(e);
                    }
                }

                // Reconnect:
                // - Any edge that previously went X -> placeholder becomes X -> entrance
                foreach (var e in incoming)
                {
                    if (e == null || e.from == null) continue;
                    if (e.from == site.placeholder) continue; // ignore weird self-loops

                    edges.Add(CloneEdgeWithNewEndpoints(e, e.from, entrance));
                }

                // - Any edge that previously went placeholder -> Y becomes exit -> Y
                foreach (var e in outgoing)
                {
                    if (e == null || e.to == null) continue;
                    if (e.to == site.placeholder) continue; // ignore weird self-loops

                    edges.Add(CloneEdgeWithNewEndpoints(e, exit, e.to));
                }

                // Continue: if the parent cycle has more rewrite sites, we keep going.
                // NOTE: We do NOT need to recurse into site.replacementPattern here, because
                // replacementFlat was produced by FlattenNestedCycle which already splices recursively.
            }
        }

        // =========================================================
        // EDGE CLONING HELPERS
        // =========================================================

        private static List<CycleEdge> CloneEdges(List<CycleEdge> source)
        {
            var result = new List<CycleEdge>();
            if (source == null) return result;

            foreach (var e in source)
            {
                if (e == null) continue;
                result.Add(CloneEdgeWithNewEndpoints(e, e.from, e.to));
            }

            return result;
        }

        private static CycleEdge CloneEdgeWithNewEndpoints(CycleEdge source, CycleNode newFrom, CycleNode newTo)
        {
            // Mirror how you already deep-copy edges in ProceduralDungeonGenerator/CycleTemplate.
            var e = new CycleEdge(newFrom, newTo, source.bidirectional, source.isBlocked, source.hasSightline);

            if (source.requiredKeys != null)
            {
                foreach (var keyId in source.requiredKeys)
                    e.AddRequiredKey(keyId);
            }

            return e;
        }
    }
}
