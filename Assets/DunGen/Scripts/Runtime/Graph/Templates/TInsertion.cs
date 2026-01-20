namespace DunGen.Graph.Templates
{
    public sealed class TInsertion
    {
        public TInsertionId Id { get; }
        public TEdgeId SeamEdge { get; }

        public TInsertion(TInsertionId id, TEdgeId seamEdge)
        {
            Id = id;
            SeamEdge = seamEdge;
        }
    }
}
