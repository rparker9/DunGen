using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;

using System.Collections.Generic;

namespace DunGen.Graph.Generation
{
    public sealed class GenerationResult
    {
        /// <summary>
        /// The flat graph (nodes and edges).
        /// </summary>
        public DungeonGraph Graph { get; }

        /// <summary>
        /// All cycles in the dungeon, indexed by CycleId.
        /// This is the "cycle layer" that preserves the PDF's flowchart structure.
        /// </summary>
        public IReadOnlyDictionary<CycleId, CycleInstanceInfo> Cycles { get; }

        /// <summary>
        /// Lookup: which cycle owns each node?
        /// </summary>
        public IReadOnlyDictionary<NodeId, CycleId> NodeToCycle { get; }

        /// <summary>
        /// Lookup: which cycle + arc index does each edge belong to?
        /// Returns (CycleId, ArcIndex) where ArcIndex is 0 for Arc A, 1 for Arc B.
        /// </summary>
        public IReadOnlyDictionary<EdgeId, (CycleId Cycle, int ArcIndex)> EdgeToArc { get; }

        /// <summary>
        /// History of all insertion events (for debugging/visualization).
        /// </summary>
        public IReadOnlyList<InsertionEvent> InsertionHistory { get; }

        /// <summary>
        /// What cycle type was used for the overall dungeon.
        /// </summary>
        public CycleType OverallType { get; }

        /// <summary>
        /// The overall cycle instance (depth 0).
        /// </summary>
        public CycleInstance OverallCycle { get; }

        public GenerationResult(
            DungeonGraph graph,
            IReadOnlyDictionary<CycleId, CycleInstanceInfo> cycles,
            IReadOnlyDictionary<NodeId, CycleId> nodeToCycle,
            IReadOnlyDictionary<EdgeId, (CycleId, int)> edgeToArc,
            IReadOnlyList<InsertionEvent> insertionHistory,
            CycleType overallType,
            CycleInstance overallCycle)
        {
            Graph = graph;
            Cycles = cycles;
            NodeToCycle = nodeToCycle;
            EdgeToArc = edgeToArc;
            InsertionHistory = insertionHistory;
            OverallType = overallType;
            OverallCycle = overallCycle;
        }

        // Helper methods

        /// <summary>
        /// Get the cycle that owns a specific node.
        /// </summary>
        public CycleInstanceInfo GetCycleForNode(NodeId nodeId)
        {
            if (NodeToCycle.TryGetValue(nodeId, out var cycleId))
                return Cycles[cycleId];
            throw new System.ArgumentException($"Node {nodeId} has no owning cycle");
        }

        /// <summary>
        /// Get all sub-cycles (depth > 0).
        /// </summary>
        public IEnumerable<CycleInstanceInfo> GetSubCycles()
        {
            foreach (var cycle in Cycles.Values)
            {
                if (cycle.Depth > 0)
                    yield return cycle;
            }
        }

        /// <summary>
        /// Get direct children of a cycle (cycles inserted at this cycle's diamonds).
        /// </summary>
        public IEnumerable<CycleInstanceInfo> GetChildCycles(CycleId parentId)
        {
            foreach (var cycle in Cycles.Values)
            {
                if (cycle.ParentCycle == parentId)
                    yield return cycle;
            }
        }
    }
}
