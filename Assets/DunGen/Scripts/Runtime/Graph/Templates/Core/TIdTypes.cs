using System;

namespace DunGen.Graph.Templates.Core
{
    public readonly struct TNodeId : IEquatable<TNodeId>
    {
        public readonly int Value;
        public TNodeId(int value) => Value = value;

        public bool Equals(TNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TNodeId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(TNodeId a, TNodeId b) => a.Equals(b);
        public static bool operator !=(TNodeId a, TNodeId b) => !a.Equals(b);
        public override string ToString() => $"TN{Value}";
    }

    public readonly struct TEdgeId : IEquatable<TEdgeId>
    {
        public readonly int Value;
        public TEdgeId(int value) => Value = value;

        public bool Equals(TEdgeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TEdgeId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(TEdgeId a, TEdgeId b) => a.Equals(b);
        public static bool operator !=(TEdgeId a, TEdgeId b) => !a.Equals(b);
        public override string ToString() => $"TE{Value}";
    }

    public readonly struct TInsertionId : IEquatable<TInsertionId>
    {
        public readonly int Value;
        public TInsertionId(int value) => Value = value;

        public bool Equals(TInsertionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TInsertionId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(TInsertionId a, TInsertionId b) => a.Equals(b);
        public static bool operator !=(TInsertionId a, TInsertionId b) => !a.Equals(b);
        public override string ToString() => $"TI{Value}";
    }
}
