using DunGen.Graph.Core;

namespace DunGen.Graph.Templates.Core
{
    /// <summary>
    /// An edge inside a template blueprint.
    ///
    /// IMPORTANT DIFFERENCE vs RoomEdge (Graph.Core):
    /// - TemplateEdge uses template IDs (TNodeId) not runtime NodeId.
    /// - TemplateEdge will be copied into the final graph during instantiation.
    /// </summary>
    public sealed class TEdge
    {
        public TEdgeId Id { get; }
        public TNodeId From { get; }
        public TNodeId To { get; }
        public EdgeTraversal Traversal { get; set; }

        public TEdge(TEdgeId id, TNodeId from, TNodeId to, EdgeTraversal traversal)
        {
            Id = id;
            From = from;
            To = to;
            Traversal = traversal;
        }
    }

}
