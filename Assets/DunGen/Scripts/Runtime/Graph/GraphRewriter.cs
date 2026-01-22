using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// Performs graph rewriting: expands rewrite sites into their replacement patterns,
    /// producing a flat graph suitable for rendering.
    /// 
    /// UPDATED: Handles node-based keys and edge-based locks
    /// </summary>
    public static class GraphRewriter
    {
        public static RewrittenGraph RewriteToFlatGraph(DungeonCycle rootPattern)
        {
            // If no root pattern, return empty graph
            if (rootPattern == null)
                return new RewrittenGraph(new List<CycleNode>(), new List<CycleEdge>());

            // Initialize nodes and edges from root pattern
            var nodes = new List<CycleNode>(rootPattern.nodes);
            var edges = new List<CycleEdge>(rootPattern.edges);

            ApplyRewritesRecursive(rootPattern, nodes, edges);

            return new RewrittenGraph(nodes, edges);
        }

        private static void ApplyRewritesRecursive(
            DungeonCycle pattern,
            List<CycleNode> nodes,
            List<CycleEdge> edges)
        {
            if (pattern == null || pattern.rewriteSites == null)
                return;

            // Process each rewrite site
            for (int i = 0; i < pattern.rewriteSites.Count; i++)
            {
                RewriteSite site = pattern.rewriteSites[i];
                if (site == null || !site.HasReplacement())
                    continue;

                CycleNode placeholder = site.placeholder;
                DungeonCycle replacement = site.replacementPattern;
                if (placeholder == null || replacement == null)
                    continue;

                // Expand the replacement pattern into the graph
                ExpandReplacementPattern(replacement, placeholder, nodes, edges);

                // Recursively process the replacement's own rewrite sites
                ApplyRewritesRecursive(replacement, nodes, edges);
            }
        }

        private static void ExpandReplacementPattern(
            DungeonCycle replacement,
            CycleNode placeholder,
            List<CycleNode> nodes,
            List<CycleEdge> edges)
        {
            if (replacement == null || placeholder == null)
                return;

            CycleNode entry = replacement.startNode;
            CycleNode exit = replacement.goalNode;

            if (entry == null || exit == null)
                return;

            // Add all replacement nodes (except those already present)
            if (replacement.nodes != null)
            {
                foreach (var node in replacement.nodes)
                {
                    if (node != null && !nodes.Contains(node))
                        nodes.Add(node);
                }
            }

            // Add all replacement edges (except those already present)
            if (replacement.edges != null)
            {
                foreach (var edge in replacement.edges)
                {
                    if (edge != null && !edges.Contains(edge))
                        edges.Add(edge);
                }
            }

            // Find all edges connected to the placeholder
            var incomingEdges = new List<CycleEdge>();
            var outgoingEdges = new List<CycleEdge>();

            foreach (var edge in edges)
            {
                if (edge == null) continue;

                if (edge.to == placeholder)
                    incomingEdges.Add(edge);
                else if (edge.from == placeholder)
                    outgoingEdges.Add(edge);
            }

            // Remove placeholder and its edges
            nodes.Remove(placeholder);
            foreach (var edge in incomingEdges)
                edges.Remove(edge);
            foreach (var edge in outgoingEdges)
                edges.Remove(edge);

            // Reconnect: incoming edges now point to entry
            // Transfer edge properties (locks)
            foreach (var edge in incomingEdges)
            {
                var newEdge = new CycleEdge(
                    edge.from,
                    entry,
                    edge.bidirectional,
                    edge.isBlocked,
                    edge.hasSightline);

                // Transfer lock requirements
                if (edge.requiredKeys != null)
                {
                    foreach (var keyId in edge.requiredKeys)
                        newEdge.AddRequiredKey(keyId);
                }

                edges.Add(newEdge);
            }

            // Reconnect: outgoing edges now come from exit
            // Transfer edge properties (locks)
            foreach (var edge in outgoingEdges)
            {
                var newEdge = new CycleEdge(
                    exit,
                    edge.to,
                    edge.bidirectional,
                    edge.isBlocked,
                    edge.hasSightline);

                // Transfer lock requirements
                if (edge.requiredKeys != null)
                {
                    foreach (var keyId in edge.requiredKeys)
                        newEdge.AddRequiredKey(keyId);
                }

                edges.Add(newEdge);
            }

            // Transfer semantic roles and keys from placeholder to replacement nodes
            TransferNodeProperties(placeholder, entry, exit);
        }

        private static void TransferNodeProperties(CycleNode placeholder, CycleNode entry, CycleNode exit)
        {
            if (placeholder == null)
                return;

            // Transfer roles
            if (placeholder.roles != null)
            {
                foreach (var role in placeholder.roles)
                {
                    if (role == null) continue;

                    switch (role.type)
                    {
                        // Structural roles - don't transfer
                        case NodeRoleType.Start:
                        case NodeRoleType.Goal:
                        case NodeRoleType.RewriteSite:
                            break;

                        // Rewards go to the exit (completion point)
                        case NodeRoleType.Reward:
                            if (exit != null && !exit.HasRole(role.type))
                                exit.AddRole(new NodeRole(role.type));
                            break;

                        // Identity/semantic roles go to entry
                        default:
                            if (entry != null && !entry.HasRole(role.type))
                                entry.AddRole(new NodeRole(role.type));
                            break;
                    }
                }
            }

            // Transfer keys: keys from placeholder go to exit (obtained on completion)
            if (placeholder.grantedKeys != null && exit != null)
            {
                foreach (var keyId in placeholder.grantedKeys)
                {
                    if (!exit.GrantsKey(keyId))
                        exit.AddGrantedKey(keyId);
                }
            }
        }
    }
}