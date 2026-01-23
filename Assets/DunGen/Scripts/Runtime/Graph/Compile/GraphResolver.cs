using DunGen;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a nested dungeon grammar into a single concrete graph.
///
/// What this does:
/// - Removes rewrite placeholder nodes.
/// - Inserts replacement patterns.
/// - Reconnects incoming edges to the replacement entrance (startNode).
/// - Reconnects outgoing edges from the replacement exit (goalNode).
///
/// Output is a resolved graph suitable for rendering, analysis,
/// or translation into physical space.
/// </summary>
public static class GraphResolver
{
    /// <summary>
    /// Resolve a root dungeon cycle into a single graph
    /// with all rewrite sites fully applied.
    /// </summary>
    public static ResolvedGraph ResolveGraph(DungeonCycle rootCycle)
    {
        if (rootCycle == null)
            return new ResolvedGraph(new List<GraphNode>(), new List<GraphEdge>());

        // Start with the root node list (node instances are reused intentionally).
        var nodes = new List<GraphNode>(rootCycle.nodes ?? new List<GraphNode>());

        // Clone edges so authored grammar data is never mutated.
        var edges = CloneEdgeList(rootCycle.edges);

        // Resolve all rewrite sites.
        var processedSites = new HashSet<RewriteSite>();
        ResolveRewriteSites(rootCycle, nodes, edges, processedSites);

        return new ResolvedGraph(nodes, edges);
    }

    // =========================================================
    // REWRITE RESOLUTION
    // =========================================================

    private static void ResolveRewriteSites(
        DungeonCycle cycle,
        List<GraphNode> nodes,
        List<GraphEdge> edges,
        HashSet<RewriteSite> processedSites)
    {
        if (cycle == null || cycle.rewriteSites == null || cycle.rewriteSites.Count == 0)
            return;

        foreach (var site in cycle.rewriteSites)
        {
            if (site == null || site.placeholder == null)
                continue;

            // Ensure each rewrite site is resolved once.
            if (!processedSites.Add(site))
                continue;

            // Skip sites without a valid replacement.
            if (!site.HasReplacementPattern() || site.replacementPattern == null)
                continue;

            // Resolve the replacement pattern first so it is internally complete.
            var resolvedReplacement = ResolveGraph(site.replacementPattern);

            var replacementEntrance = site.replacementPattern.startNode;
            var replacementExit = site.replacementPattern.goalNode;

            if (replacementEntrance == null || replacementExit == null)
            {
                Debug.LogWarning(
                    $"[GraphResolver] Replacement missing start/goal; cannot replace '{site.placeholder.label}'.");
                continue;
            }

            // Capture the placeholder's connections before removal.
            var incomingEdges = new List<GraphEdge>();
            var outgoingEdges = new List<GraphEdge>();

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;

                if (edge.to == site.placeholder) incomingEdges.Add(edge);
                if (edge.from == site.placeholder) outgoingEdges.Add(edge);
            }

            // Remove placeholder and all edges touching it.
            edges.RemoveAll(edge =>
                edge != null &&
                (edge.from == site.placeholder || edge.to == site.placeholder));

            nodes.Remove(site.placeholder);

            // Merge resolved replacement nodes and edges.
            if (resolvedReplacement.nodes != null)
            {
                foreach (var node in resolvedReplacement.nodes)
                {
                    if (node != null && !nodes.Contains(node))
                        nodes.Add(node);
                }
            }

            if (resolvedReplacement.edges != null)
            {
                foreach (var edge in resolvedReplacement.edges)
                {
                    if (edge != null)
                        edges.Add(edge);
                }
            }

            // Reconnect incoming edges to the replacement entrance.
            foreach (var edge in incomingEdges)
            {
                if (edge == null || edge.from == null) continue;
                if (edge.from == site.placeholder) continue;

                edges.Add(CloneEdgeWithNewEndpoints(edge, edge.from, replacementEntrance));
            }

            // Reconnect outgoing edges from the replacement exit.
            foreach (var edge in outgoingEdges)
            {
                if (edge == null || edge.to == null) continue;
                if (edge.to == site.placeholder) continue;

                edges.Add(CloneEdgeWithNewEndpoints(edge, replacementExit, edge.to));
            }

            // No further recursion needed here:
            // resolvedReplacement is already fully resolved.
        }
    }

    // =========================================================
    // EDGE CLONING
    // =========================================================

    private static List<GraphEdge> CloneEdgeList(List<GraphEdge> sourceEdges)
    {
        var clones = new List<GraphEdge>();
        if (sourceEdges == null)
            return clones;

        foreach (var edge in sourceEdges)
        {
            if (edge == null)
                continue;

            clones.Add(CloneEdgeWithNewEndpoints(edge, edge.from, edge.to));
        }

        return clones;
    }

    private static GraphEdge CloneEdgeWithNewEndpoints(
        GraphEdge source,
        GraphNode newFrom,
        GraphNode newTo)
    {
        var edge = new GraphEdge(
            newFrom,
            newTo,
            source.bidirectional,
            source.isBlocked,
            source.hasSightline);

        if (source.requiredKeys != null)
        {
            foreach (var keyId in source.requiredKeys)
                edge.AddRequiredKey(keyId);
        }

        return edge;
    }
}
