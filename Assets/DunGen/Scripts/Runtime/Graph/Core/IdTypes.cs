using System;

namespace DunGen.Graph.Core
{
    // These small "ID wrapper" structs exist so you don't accidentally mix up
    // different kinds of IDs (ex: pass a NodeId where an EdgeId was expected).
    //
    // They also work great as Dictionary keys (hashable, equatable),
    // and they make debugging easier (ToString prints N12 instead of just 12).

    public readonly struct NodeId : IEquatable<NodeId>
    {
        // The actual integer value we store.
        public readonly int Value;

        public NodeId(int value) => Value = value;

        // Equality is value-based: NodeId(5) == NodeId(5)
        public bool Equals(NodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is NodeId other && Equals(other);

        // Used by Dictionary/HashSet.
        public override int GetHashCode() => Value;

        public static bool operator ==(NodeId a, NodeId b) => a.Equals(b);
        public static bool operator !=(NodeId a, NodeId b) => !a.Equals(b);

        // Helpful for logs/debugging.
        public override string ToString() => $"N{Value}";
    }

    public readonly struct EdgeId : IEquatable<EdgeId>
    {
        public readonly int Value;
        public EdgeId(int value) => Value = value;

        public bool Equals(EdgeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EdgeId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(EdgeId a, EdgeId b) => a.Equals(b);
        public static bool operator !=(EdgeId a, EdgeId b) => !a.Equals(b);
        public override string ToString() => $"E{Value}";
    }

    // InsertionId is used later by your graph-rewrite system (diamond-shaped insertion points)
    // to identify "seams" where a sub-cycle can be spliced into the parent graph.
    public readonly struct InsertionId : IEquatable<InsertionId>
    {
        public readonly int Value;
        public InsertionId(int value) => Value = value;

        public bool Equals(InsertionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is InsertionId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(InsertionId a, InsertionId b) => a.Equals(b);
        public static bool operator !=(InsertionId a, InsertionId b) => !a.Equals(b);
        public override string ToString() => $"I{Value}";
    }

    // KeyId identifies a specific key (K1, K2...) in lock-and-key generation.
    public readonly struct KeyId : IEquatable<KeyId>
    {
        public readonly int Value;
        public KeyId(int value) => Value = value;

        public bool Equals(KeyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is KeyId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(KeyId a, KeyId b) => a.Equals(b);
        public static bool operator !=(KeyId a, KeyId b) => !a.Equals(b);
        public override string ToString() => $"K{Value}";
    }

    // GateId identifies a specific gate/lock (G1, G2...).
    public readonly struct GateId : IEquatable<GateId>
    {
        public readonly int Value;
        public GateId(int value) => Value = value;

        public bool Equals(GateId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GateId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(GateId a, GateId b) => a.Equals(b);
        public static bool operator !=(GateId a, GateId b) => !a.Equals(b);
        public override string ToString() => $"G{Value}";
    }

    /// <summary>
    /// Identifies a specific cycle instance in the generated dungeon.
    /// </summary>
    public readonly struct CycleId : IEquatable<CycleId>
    {
        public readonly int Value;
        public CycleId(int value) => Value = value;

        public bool Equals(CycleId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CycleId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(CycleId a, CycleId b) => a.Equals(b);
        public static bool operator !=(CycleId a, CycleId b) => !a.Equals(b);
        public override string ToString() => $"C{Value}";
    }
}
