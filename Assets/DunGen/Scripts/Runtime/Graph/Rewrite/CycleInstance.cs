using DunGen.Graph.Core;

using System.Collections.Generic;

namespace DunGen.Graph.Rewrite
{
    /// <summary>
    /// Temporary chunk of graph produced from a CycleTemplate.
    /// </summary>
    public sealed class CycleInstance
    {
        public NodeId Entry { get; }
        public NodeId Exit { get; }

        public List<RoomNode> NewNodes { get; } = new List<RoomNode>();
        public List<RoomEdge> NewEdges { get; } = new List<RoomEdge>();
        public List<InsertionPoint> NewInsertions { get; } = new List<InsertionPoint>();

        // NEW: Cycle identity and structure
        public CycleInstanceInfo CycleInfo { get; set; }

        public CycleInstance(NodeId entry, NodeId exit)
        {
            Entry = entry;
            Exit = exit;
        }
    }
}
