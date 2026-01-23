using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// A flattened graph containing all nodes and edges from a potentially nested cycle structure.
    /// Replaces the old "RewrittenGraph" concept - now just a simple flat collection.
    /// </summary>
    public sealed class ResolvedGraph
    {
        public readonly List<GraphNode> nodes;
        public readonly List<GraphEdge> edges;

        public ResolvedGraph(List<GraphNode> nodes, List<GraphEdge> edges)
        {
            this.nodes = nodes ?? new List<GraphNode>();
            this.edges = edges ?? new List<GraphEdge>();
        }

        /// <summary>
        /// Create an empty flat graph.
        /// </summary>
        public static ResolvedGraph Empty()
        {
            return new ResolvedGraph(new List<GraphNode>(), new List<GraphEdge>());
        }

        /// <summary>
        /// Get count of nodes in this flat graph.
        /// </summary>
        public int NodeCount => nodes?.Count ?? 0;

        /// <summary>
        /// Get count of edges in this flat graph.
        /// </summary>
        public int EdgeCount => edges?.Count ?? 0;

        /// <summary>
        /// Check if this flat graph is empty (no nodes).
        /// </summary>
        public bool IsEmpty => NodeCount == 0;

        /// <summary>
        /// Find a node by label (convenience method).
        /// </summary>
        public GraphNode FindNode(string label)
        {
            if (nodes == null || string.IsNullOrEmpty(label))
                return null;

            foreach (var node in nodes)
            {
                if (node != null && node.label == label)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// Find all nodes with a specific role.
        /// </summary>
        public List<GraphNode> FindNodesWithRole(NodeRoleType roleType)
        {
            var result = new List<GraphNode>();

            if (nodes == null)
                return result;

            foreach (var node in nodes)
            {
                if (node != null && node.HasRole(roleType))
                    result.Add(node);
            }

            return result;
        }

        /// <summary>
        /// Get all edges connected to a specific node.
        /// </summary>
        public List<GraphEdge> GetConnectedEdges(GraphNode node)
        {
            var result = new List<GraphEdge>();

            if (edges == null || node == null)
                return result;

            foreach (var edge in edges)
            {
                if (edge != null && (edge.from == node || edge.to == node))
                    result.Add(edge);
            }

            return result;
        }
    }
}