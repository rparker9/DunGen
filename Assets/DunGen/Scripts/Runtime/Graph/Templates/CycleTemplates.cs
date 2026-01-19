using DunGen.Graph.Core;
using System;
using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    /*
        =============================
        WHY THIS FILE EXISTS
        =============================

        You already have "Graph.Core" which defines:

            - RoomNode / RoomEdge
            - NodeId / EdgeId
            - DungeonGraph (the final output graph)

        That Core graph is the REAL dungeon flowchart your generator produces.

        This "Graph.Templates" namespace is different:
            It does NOT represent the final graph.

        Instead, it represents BLUEPRINTS (patterns) that we can reuse to build the final graph.

        Think of it like:

            Graph.Core      = A finished LEGO build (the dungeon graph you will draw & play).
            Graph.Templates = LEGO instructions pages (reusable patterns you can build and combine).

        We use templates because your PDF is all about:
            - building a big cycle (overall cycle)
            - then replacing  insertion points with smaller cycles
            - repeating to get cycles-within-cycles (nested structure)

        Templates are "small graphs" that we can instantiate many times.
    */

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

    /*
        =============================
        TEMPLATE-LEVEL IDs
        =============================

        In Graph.Core you have NodeId / EdgeId.
        Those IDs identify nodes/edges in the FINAL dungeon graph.

        Here we use TNodeId / TEdgeId / TInsertionId instead.

        Why?
        - A template is like a "mold" or "pattern".
        - You might instantiate the same template 50 times.
        - Each instance needs fresh NodeId/EdgeId values in the real graph.
        - So the template must NOT reuse the runtime IDs.

        Template IDs are local to the template and only used internally
        while instantiating.
    */

    public readonly struct TNodeId : IEquatable<TNodeId>
    {
        public readonly int Value;
        public TNodeId(int value) => Value = value;

        public bool Equals(TNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TNodeId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(TNodeId a, TNodeId b) => a.Equals(b);
        public static bool operator !=(TNodeId a, TNodeId b) => !a.Equals(b);
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
    }

    /// <summary>
    /// A node inside a template blueprint.
    ///
    /// IMPORTANT DIFFERENCE vs RoomNode (Graph.Core):
    /// - TemplateNode is NOT part of the final graph.
    /// - It’s a "prototype" node that will be copied into the final graph later.
    ///
    /// During instantiation we will:
    /// - allocate a fresh NodeId for each TemplateNode
    /// - create a real RoomNode in the output graph
    /// </summary>
    public sealed class TemplateNode
    {
        public TNodeId Id { get; }
        public NodeKind Kind { get; set; }
        public string DebugLabel { get; set; }

        // We reuse the same tag structs as Core (NodeTag) because the meaning is identical.
        public List<NodeTag> Tags { get; } = new List<NodeTag>();

        public TemplateNode(TNodeId id, NodeKind kind = NodeKind.Normal, string debugLabel = null)
        {
            Id = id;
            Kind = kind;
            DebugLabel = debugLabel;
        }
    }

    /// <summary>
    /// An edge inside a template blueprint.
    ///
    /// IMPORTANT DIFFERENCE vs RoomEdge (Graph.Core):
    /// - TemplateEdge uses template IDs (TNodeId) not runtime NodeId.
    /// - TemplateEdge will be copied into the final graph during instantiation.
    /// </summary>
    public sealed class TemplateEdge
    {
        public TEdgeId Id { get; }
        public TNodeId From { get; }
        public TNodeId To { get; }

        // Traversal rules are the same conceptually as Core, so we reuse the enum.
        public EdgeTraversal Traversal { get; set; }

        public TemplateEdge(TEdgeId id, TNodeId from, TNodeId to, EdgeTraversal traversal = EdgeTraversal.Normal)
        {
            Id = id;
            From = from;
            To = to;
            Traversal = traversal;
        }
    }

    /// <summary>
    /// One of the two arcs in a cycle.
    /// An arc is basically one "branch" from Start to Goal.
    ///
    /// MVP representation:
    /// - An arc is just the ordered list of edges you follow.
    /// Later we may store a richer structure (steps, room counts, etc).
    /// </summary>
    public sealed class TemplateArc
    {
        public string Name { get; }
        public ArcLengthHint LengthHint { get; }

        // EdgeChain is the path that forms this arc.
        // (Example: Start -> A -> B -> Goal)
        public List<TEdgeId> EdgeChain { get; } = new List<TEdgeId>();

        public TemplateArc(string name, ArcLengthHint lengthHint)
        {
            Name = name;
            LengthHint = lengthHint;
        }
    }

    /// <summary>
    /// A diamond insertion point from the PDF.
    ///
    /// We represent the diamond as an EDGE SEAM.
    /// That means:
    /// - The seam edge will be REMOVED later
    /// - A new sub-cycle graph will be inserted in its place
    ///
    /// This is the key trick that enables "cycles within cycles" without
    /// having nested object hierarchies.
    /// </summary>
    public sealed class TemplateInsertionPoint
    {
        public TInsertionId Id { get; }

        // The edge in this template that acts as the seam.
        public TEdgeId SeamEdge { get; }

        public TemplateInsertionPoint(TInsertionId id, TEdgeId seamEdge)
        {
            Id = id;
            SeamEdge = seamEdge;
        }
    }

    /// <summary>
    /// A reusable "cycle blueprint" describing:
    /// - Start node
    /// - Goal node
    /// - exactly two arcs from start to goal
    /// -  insertion seams that allow nesting
    ///
    /// IMPORTANT DIFFERENCE vs DungeonGraph (Graph.Core):
    /// - CycleTemplate is not a final dungeon.
    /// - It's a reusable pattern that can be instantiated many times.
    /// - Instantiation converts TemplateNodes/Edges into real RoomNodes/Edges.
    /// </summary>
    public sealed class CycleTemplate
    {
        public CycleType Type { get; }

        // Template graphs are stored separately from the runtime graph.
        public Dictionary<TNodeId, TemplateNode> Nodes { get; } = new Dictionary<TNodeId, TemplateNode>();
        public Dictionary<TEdgeId, TemplateEdge> Edges { get; } = new Dictionary<TEdgeId, TemplateEdge>();

        // Template-level "special nodes"
        public TNodeId Start { get; private set; }
        public TNodeId Goal { get; private set; }

        // A cycle always has two arcs (PDF definition).
        public TemplateArc ArcA { get; }
        public TemplateArc ArcB { get; }

        // Diamonds where we can insert sub-cycles.
        public List<TemplateInsertionPoint> Insertions { get; } = new List<TemplateInsertionPoint>();

        public CycleTemplate(CycleType type, TemplateArc arcA, TemplateArc arcB)
        {
            Type = type;
            ArcA = arcA;
            ArcB = arcB;
        }

        public CycleTemplate SetStart(TNodeId id) { Start = id; return this; }
        public CycleTemplate SetGoal(TNodeId id) { Goal = id; return this; }

        // Builder helpers (makes defining templates less annoying)
        public TemplateNode AddNode(int id, NodeKind kind = NodeKind.Normal, string label = null)
        {
            var tn = new TemplateNode(new TNodeId(id), kind, label);
            Nodes.Add(tn.Id, tn);
            return tn;
        }

        public TemplateEdge AddEdge(int id, int from, int to, EdgeTraversal traversal = EdgeTraversal.Normal)
        {
            var te = new TemplateEdge(new TEdgeId(id), new TNodeId(from), new TNodeId(to), traversal);
            Edges.Add(te.Id, te);
            return te;
        }

        public void AddInsertion(int insertionId, int seamEdgeId)
        {
            Insertions.Add(new TemplateInsertionPoint(new TInsertionId(insertionId), new TEdgeId(seamEdgeId)));
        }
    }
}
