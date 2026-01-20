using DunGen.Graph.Core;
using DunGen.Graph.Templates.Core;
using System;
using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    /// <summary>
    /// The 12 cycle types from the PDF’s table.
    /// Each one corresponds to a specific "gameplay loop pattern"
    /// like lock & key, hidden shortcut, false goal, etc.
    /// </summary>
    public enum CycleType
    {
        TwoAlternativePaths,
        TwoKeys,
        HiddenShortcut,
        DangerousRoute,
        ForeshadowingLoop,
        LockAndKeyCycle,
        BlockedRetreat,
        MonsterPatrol,
        AlteredReturn,
        FalseGoal,
        SimpleLockAndKey,
        Gambit
    }

    /// <summary>
    /// A cycle template from the PDF’s table of 12 cycle types.
    /// </summary>
    public sealed class CycleTemplate
    {
        public CycleType Type { get; }
        public IReadOnlyDictionary<TNodeId, TNode> Nodes { get; }
        public IReadOnlyDictionary<TEdgeId, TEdge> Edges { get; }
        public IReadOnlyList<TInsertion> Insertions { get; }
        public TNodeId Start { get; }
        public TNodeId Goal { get; }

        // NEW: Arc membership (from PDF page 2)
        public IReadOnlyList<TEdgeId> ArcA { get; }
        public IReadOnlyList<TEdgeId> ArcB { get; }

        public CycleTemplate(
            CycleType type,
            IReadOnlyDictionary<TNodeId, TNode> nodes,
            IReadOnlyDictionary<TEdgeId, TEdge> edges,
            IReadOnlyList<TInsertion> insertions,
            TNodeId start,
            TNodeId goal,
            IReadOnlyList<TEdgeId> arcA,    // NEW
            IReadOnlyList<TEdgeId> arcB)    // NEW
        {
            Type = type;
            Nodes = nodes;
            Edges = edges;
            Insertions = insertions;
            Start = start;
            Goal = goal;
            ArcA = arcA ?? System.Array.Empty<TEdgeId>();
            ArcB = arcB ?? System.Array.Empty<TEdgeId>();
        }
    }
}