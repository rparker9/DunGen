using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Represents a complete dungeon cycle (overall or sub-cycle).
    /// Cycles are now manually authored in the editor, not generated from types.
    /// </summary>
    [System.Serializable]
    public class DungeonCycle
    {
        public GraphNode startNode;
        public GraphNode goalNode;

        public List<GraphNode> nodes = new List<GraphNode>();
        public List<GraphEdge> edges = new List<GraphEdge>();

        // Nodes that can be rewritten into a replacement pattern
        public List<RewriteSite> rewriteSites = new List<RewriteSite>();

        /// <summary>
        /// Create an empty cycle (for manual authoring)
        /// </summary>
        public DungeonCycle() { }
    }
}