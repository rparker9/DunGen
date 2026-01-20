using DunGen.Graph.Core;

using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    /// <summary>
    /// Node kind in TEMPLATE space (before instantiation).
    /// These map to runtime NodeKind + NodeTagKind during instantiation.
    /// 
    /// Mapping rules (from GraphRewriteEngine.Instantiate):
    /// - TNodeKind.Start -> NodeKind.Entrance (depth 0) or NodeKind.Normal + NodeTagKind.CycleStart (depth > 0)
    /// - TNodeKind.Goal -> NodeKind.Exit (depth 0) or NodeKind.Normal + NodeTagKind.CycleGoal (depth > 0)
    /// - TNodeKind.Normal -> NodeKind.Normal
    /// </summary>
    public enum TNodeKind
    {
        Normal,
        Start,  // Template-level "start" marker
        Goal    // Template-level "goal" marker
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
    public sealed class TNode
    {
        public TNodeId Id { get; }
        public TNodeKind Kind { get; set; }
        public string DebugLabel { get; set; }
        public List<NodeTag> Tags { get; } = new List<NodeTag>();

        public TNode(TNodeId id, TNodeKind kind, string debugLabel = null)
        {
            Id = id;
            Kind = kind;
            DebugLabel = debugLabel;
        }
    }
}
