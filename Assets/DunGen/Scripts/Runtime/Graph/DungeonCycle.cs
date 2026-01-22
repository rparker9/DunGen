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
        public CycleNode startNode;
        public CycleNode goalNode;

        public List<CycleNode> nodes = new List<CycleNode>();
        public List<CycleEdge> edges = new List<CycleEdge>();

        // Nodes that can be rewritten into a replacement pattern
        public List<RewriteSite> rewriteSites = new List<RewriteSite>();

        /// <summary>
        /// Create an empty cycle (for manual authoring)
        /// </summary>
        public DungeonCycle()
        {
            CreateEmptyStructure();
        }

        /// <summary>
        /// Create a basic cycle with just start and goal nodes
        /// </summary>
        private void CreateEmptyStructure()
        {
            nodes.Clear();
            edges.Clear();
            rewriteSites.Clear();

            // Create start node
            startNode = new CycleNode();
            startNode.label = "START";
            startNode.AddRole(NodeRoleType.Start);
            nodes.Add(startNode);

            // Create goal node
            goalNode = new CycleNode();
            goalNode.label = "GOAL";
            goalNode.AddRole(NodeRoleType.Goal);
            nodes.Add(goalNode);
        }
    }
}