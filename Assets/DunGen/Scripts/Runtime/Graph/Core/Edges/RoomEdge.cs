#nullable enable
namespace DunGen.Graph.Core
{
    /// <summary>
    /// How the player is allowed to traverse an edge.
    /// </summary>
    public enum EdgeTraversal
    {
        // Standard corridor / connection between rooms.
        Normal,

        // Only traversable in one direction.
        // Useful for: drop-downs, slides, one-way doors, teleports, etc.
        OneWay,

        // This connection exists visually / conceptually (e.g. you can see the goal),
        // but you cannot walk through it.
        // Useful for: foreshadowing, locked gates you can see, windows, etc.
        SightlineBlocked
    }

    /// <summary>
    /// A directed connection between two nodes in the flowchart graph.
    ///
    /// The graph is directed even if you *usually* treat connections as two-way,
    /// because it makes it easy to represent special cases:
    /// - one-way paths
    /// - shortcuts / warps
    /// - gated movement
    /// </summary>
    public sealed class RoomEdge
    {
        public EdgeId Id { get; }

        // "From" and "To" are node IDs, not references, so the graph stays lightweight
        // and easy to serialize/debug.
        public NodeId From { get; }
        public NodeId To { get; }

        // Traversal rules (normal / one-way / sightline-only).
        public EdgeTraversal Traversal { get; set; }

        // Optional lock/barrier info.
        public EdgeGate? Gate { get; set; }

        public RoomEdge(EdgeId id, NodeId from, NodeId to, EdgeTraversal traversal = EdgeTraversal.Normal)
        {
            Id = id;
            From = from;
            To = to;
            Traversal = traversal;
        }
    }
}
#nullable disable
