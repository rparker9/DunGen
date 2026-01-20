#nullable enable
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;
using System.Collections.Generic;

namespace DunGen.Graph.Core
{
    /// <summary>
    /// Runtime metadata for a cycle instance in the generated dungeon.
    /// Allows the editor to draw PDF-style flowchart diagrams showing
    /// the cycle's Start->Goal structure with two arcs and insertion points.
    /// 
    /// From PDF page 2: "Each cycle has four elements: a start, a goal, arcs, 
    /// and insertion points."
    /// </summary>
    public sealed class CycleInstanceInfo
    {
        /// <summary>
        /// Unique identifier for this cycle instance.
        /// </summary>
        public CycleId Id { get; }

        /// <summary>
        /// What template was used (e.g., "Lock-and-key", "Two Keys").
        /// </summary>
        public CycleType Type { get; }

        /// <summary>
        /// Nesting depth (0 = overall cycle, 1+ = sub-cycles).
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// The cycle's start node (PDF: "Start").
        /// </summary>
        public NodeId StartNode { get; }

        /// <summary>
        /// The cycle's goal node (PDF: "Goal").
        /// </summary>
        public NodeId GoalNode { get; }

        /// <summary>
        /// Edges belonging to Arc A (one of the two paths from start to goal).
        /// From PDF page 2: "A cycle has two arcs—lines that connect the start to the goal."
        /// </summary>
        public IReadOnlyList<EdgeId> ArcA { get; }

        /// <summary>
        /// Edges belonging to Arc B (the other path from start to goal).
        /// </summary>
        public IReadOnlyList<EdgeId> ArcB { get; }

        /// <summary>
        /// Insertion points that belong to this cycle.
        /// From PDF page 2: "A point in the cycle in which a sub-cycle might be inserted."
        /// </summary>
        public IReadOnlyList<InsertionPoint> InsertionPoints { get; }

        /// <summary>
        /// Parent cycle ID (null for overall cycle).
        /// </summary>
        public CycleId? ParentCycle { get; }

        /// <summary>
        /// Which insertion point in the parent was replaced to create this cycle.
        /// </summary>
        public InsertionId? ParentInsertion { get; }

        public CycleInstanceInfo(
            CycleId id,
            CycleType type,
            int depth,
            NodeId startNode,
            NodeId goalNode,
            IReadOnlyList<EdgeId> arcA,
            IReadOnlyList<EdgeId> arcB,
            IReadOnlyList<InsertionPoint> insertionPoints,
            CycleId? parentCycle = null,
            InsertionId? parentInsertion = null)
        {
            Id = id;
            Type = type;
            Depth = depth;
            StartNode = startNode;
            GoalNode = goalNode;
            ArcA = arcA;
            ArcB = arcB;
            InsertionPoints = insertionPoints;
            ParentCycle = parentCycle;
            ParentInsertion = parentInsertion;
        }
    }
}
#nullable disable