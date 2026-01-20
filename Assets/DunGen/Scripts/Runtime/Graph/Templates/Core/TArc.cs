using System.Collections.Generic;

namespace DunGen.Graph.Templates.Core
{
    /// <summary>
    /// In the PDF, arcs have a "short" vs "long" notion.
    /// This is NOT spatial distance yet.
    /// It's a generation hint:
    ///   - Short: usually 1-2 rooms
    ///   - Long:  usually 2-3+ rooms
    /// Later we will expand these arcs into real chains of RoomNodes.
    /// </summary>
    public enum ArcLengthHint
    {
        Short,
        Long
    }

    /// <summary>
    /// One of the two arcs in a cycle.
    /// An arc is basically one "branch" from Start to Goal.
    ///
    /// MVP representation:
    /// - An arc is just the ordered list of edges you follow.
    /// Later we may store a richer structure (steps, room counts, etc).
    /// </summary>
    public sealed class TArc
    {
        public string Name { get; }
        public ArcLengthHint LengthHint { get; }

        // EdgeChain is the path that forms this arc.
        // (Example: Start -> A -> B -> Goal)
        public List<TEdgeId> EdgeChain { get; } = new List<TEdgeId>();

        public TArc(string name, ArcLengthHint lengthHint)
        {
            Name = name;
            LengthHint = lengthHint;
        }
    }
}
