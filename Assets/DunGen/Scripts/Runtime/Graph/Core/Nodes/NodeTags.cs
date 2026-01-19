using System;

namespace DunGen.Graph.Core
{
    // "Kind" is the structural role of a node in the flowchart.
    // You can have a Start node, a Goal node, and normal nodes.
    public enum NodeKind
    {
        Normal,
        Start,
        Goal
    }

    // "TagKind" describes *semantics* of a room:
    // what the player can find here, or what the room represents.
    public enum NodeTagKind
    {
        None,

        // Core semantic tags you’ll likely want soon:
        Key,         // a key pickup exists here (Data might store which key)
        Reward,      // treasure / loot / reward
        Danger,      // combat / hazard / risk
        Secret,      // hidden room / optional content
        LockHint,    // hints the player about upcoming locks
        BarrierHint  // hints about upcoming barriers
    }

    /// <summary>
    /// A compact piece of metadata attached to a node.
    ///
    /// Example uses:
    /// - new NodeTag(NodeTagKind.Key, data: 1)   // "Key 1 is here"
    /// - new NodeTag(NodeTagKind.Danger)         // "this room is dangerous"
    ///
    /// It's a struct so it stays lightweight and can be stored in lists efficiently.
    /// </summary>
    public readonly struct NodeTag : IEquatable<NodeTag>
    {
        public readonly NodeTagKind Kind;

        // Optional extra data. (Ex: key index, difficulty tier, etc.)
        public readonly int Data;

        public NodeTag(NodeTagKind kind, int data = 0)
        {
            Kind = kind;
            Data = data;
        }

        public bool Equals(NodeTag other)
            => Kind == other.Kind && Data == other.Data;

        public override bool Equals(object obj)
            => obj is NodeTag other && Equals(other);

        public override int GetHashCode()
            => ((int)Kind * 397) ^ Data;

        public static bool operator ==(NodeTag a, NodeTag b) => a.Equals(b);
        public static bool operator !=(NodeTag a, NodeTag b) => !a.Equals(b);

        public override string ToString()
            => Data == 0 ? Kind.ToString() : $"{Kind}({Data})";
    }
}
