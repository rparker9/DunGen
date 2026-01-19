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
}
