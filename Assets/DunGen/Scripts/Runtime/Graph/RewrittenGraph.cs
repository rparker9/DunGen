using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// Result of graph rewriting: a flat graph (no nesting).
    /// </summary>
    public sealed class RewrittenGraph
    {
        public readonly List<CycleNode> nodes;
        public readonly List<CycleEdge> edges;

        public RewrittenGraph(List<CycleNode> nodes, List<CycleEdge> edges)
        {
            this.nodes = nodes;
            this.edges = edges;
        }
    }
}
