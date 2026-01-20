using System.Collections.Generic;

namespace DunGen.Graph.Core
{
    /// <summary>
    /// What kind of gating mechanic blocks an edge.
    /// </summary>
    public enum GateKind
    {
        // Typically unlocked by a key.
        Lock,

        // Blocked by something else (rocks, debris, boss, puzzle, etc.)
        Barrier
    }

    /// <summary>
    /// How "strict" the gate is.
    /// Soft vs Hard matters later when you want
    /// more flexible pacing / optional bypasses.
    /// </summary>
    public enum GateStrength
    {
        Soft,
        Hard
    }

    /// <summary>
    /// Optional gating data attached to an edge.
    ///
    /// Think: "This door is locked and needs Key 1"
    /// or "This corridor is blocked by rubble".
    /// </summary>
    public sealed class EdgeGate
    {
        // Unique ID for the gate (useful for debugging and future editor tools).
        public GateId Id { get; }

        // Lock vs Barrier.
        public GateKind Kind { get; }

        // Soft vs Hard gate (later can affect generation rules).
        public GateStrength Strength { get; }

        // Single-key gate (works for SimpleLockAndKey later).
        public KeyId? RequiredKey { get; }

        // Multi-key gate (used for TwoKeys).
        // If this list is non-empty, it takes priority over RequiredKey.
        public IReadOnlyList<KeyId> RequiredKeys { get; }

        /// <summary>
        /// Constructor for single-key gate.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="kind"></param>
        /// <param name="strength"></param>
        /// <param name="requiredKey"></param>
        public EdgeGate(GateId id, GateKind kind, GateStrength strength, KeyId? requiredKey)
        {
            Id = id;
            Kind = kind;
            Strength = strength;
            RequiredKey = requiredKey;
            RequiredKeys = System.Array.Empty<KeyId>();
        }

        /// <summary>
        /// Constructor for multi-key gate.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="kind"></param>
        /// <param name="strength"></param>
        /// <param name="requiredKeys"></param>
        public EdgeGate(GateId id, GateKind kind, GateStrength strength, IReadOnlyList<KeyId> requiredKeys)
        {
            Id = id;
            Kind = kind;
            Strength = strength;
            RequiredKey = null;
            RequiredKeys = requiredKeys ?? System.Array.Empty<KeyId>();
        }

        public bool IsMultiKey => RequiredKeys != null && RequiredKeys.Count > 0;
    }
}
