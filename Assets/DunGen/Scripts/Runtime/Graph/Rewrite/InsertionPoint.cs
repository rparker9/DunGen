using DunGen.Graph.Core;

namespace DunGen.Graph.Rewrite
{
    /// <summary>
    /// Concrete insertion seam in the *runtime graph*.
    /// The seam is an existing edge that we will replace with an inserted subgraph.
    /// </summary>
    public sealed class InsertionPoint
    {
        public InsertionId Id { get; }
        public EdgeId SeamEdge { get; }
        public int Depth { get; }

        public InsertionPoint(InsertionId id, EdgeId seamEdge, int depth)
        {
            Id = id;
            SeamEdge = seamEdge;
            Depth = depth;
        }
    }
}
