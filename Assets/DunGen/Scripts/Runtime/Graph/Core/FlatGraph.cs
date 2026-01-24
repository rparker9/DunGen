using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// A flat (non-nested) dungeon connectivity graph: nodes + edges only.
    ///
    /// Important: "Flat" refers to *structure*, not geometry.
    /// Layout (positions / routing) is computed separately by editor layout engines.
    /// </summary>
    public sealed class FlatGraph
    {
        public readonly List<GraphNode> nodes;
        public readonly List<GraphEdge> edges;

        public FlatGraph(List<GraphNode> nodes, List<GraphEdge> edges)
        {
            // Defensive: always non-null lists.
            this.nodes = nodes ?? new List<GraphNode>();
            this.edges = edges ?? new List<GraphEdge>();
        }

        /// <summary>Create an empty flat graph.</summary>
        public static FlatGraph Empty() => new FlatGraph(new List<GraphNode>(), new List<GraphEdge>());

        public int NodeCount => nodes.Count;
        public int EdgeCount => edges.Count;
        public bool IsEmpty => nodes.Count == 0;

        /// <summary>
        /// Convenience: wraps an authored template cycle as a flat graph *without* resolving rewrite sites.
        /// Authoring uses this for drawing the template as-authored.
        /// </summary>
        public static FlatGraph FromTemplateCycle(DungeonCycle templateCycle)
        {
            if (templateCycle == null)
                return Empty();

            return new FlatGraph(templateCycle.nodes, templateCycle.edges);
        }

        public GraphNode FindNode(string label)
        {
            if (string.IsNullOrEmpty(label))
                return null;

            foreach (var node in nodes)
            {
                if (node != null && node.label == label)
                    return node;
            }

            return null;
        }

        public List<GraphNode> FindNodesWithRole(NodeRoleType roleType)
        {
            var result = new List<GraphNode>();

            foreach (var node in nodes)
            {
                if (node != null && node.HasRole(roleType))
                    result.Add(node);
            }

            return result;
        }

        public List<GraphEdge> GetConnectedEdges(GraphNode node)
        {
            var result = new List<GraphEdge>();

            if (node == null)
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
