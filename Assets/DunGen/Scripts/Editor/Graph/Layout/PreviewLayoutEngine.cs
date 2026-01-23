using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// NEW APPROACH: Hierarchical layout that preserves cycle structure.
    /// Instead of flattening, we maintain the nested structure throughout layout and rendering.
    /// This eliminates edge crossings by treating each cycle as a self-contained unit.
    /// </summary>
    public sealed class PreviewLayoutEngine
    {
        // =========================================================
        // DATA STRUCTURES
        // =========================================================

        /// <summary>
        /// A cycle with its computed layout information
        /// </summary>
        public class LayoutCycle
        {
            // Source data
            public DungeonCycle source;
            public int depth;

            // Computed layout
            public Vector2 center;
            public float radius;
            public Dictionary<GraphNode, Vector2> nodePositions = new Dictionary<GraphNode, Vector2>();

            // Hierarchy
            public LayoutCycle parent;
            public List<LayoutCycle> children = new List<LayoutCycle>();

            // Connection points for parent
            public GraphNode entranceNode;  // Where parent connects in
            public GraphNode exitNode;      // Where we connect back to parent
            public Vector2 entrancePos;
            public Vector2 exitPos;
        }

        /// <summary>
        /// Complete layout result with hierarchy preserved
        /// </summary>
        public class LayoutResult
        {
            public LayoutCycle root;
            public List<LayoutCycle> allCycles = new List<LayoutCycle>();
            public Dictionary<GraphNode, Vector2> allPositions = new Dictionary<GraphNode, Vector2>();
        }

        // =========================================================
        // CONFIGURATION
        // =========================================================

        private const float BaseRadius = 300f;
        private const float MinRadius = 80f;
        private const float NodeSpacing = 100f;
        private const float CycleSpacing = 120f; // Space between nested cycles
        private const float DepthRadiusScale = 0.7f; // How much smaller each level gets

        // =========================================================
        // PUBLIC API
        // =========================================================

        /// <summary>
        /// Compute hierarchical layout for a dungeon cycle tree
        /// </summary>
        public static LayoutResult ComputeLayout(DungeonCycle rootCycle)
        {
            if (rootCycle == null)
                return new LayoutResult();

            var result = new LayoutResult();

            // Build layout tree
            result.root = BuildLayoutTree(rootCycle, depth: 0, parent: null);
            CollectAllCycles(result.root, result.allCycles);

            // Compute positions recursively
            LayoutCycleRecursive(result.root, Vector2.zero, BaseRadius);

            // Collect all positions
            CollectAllPositions(result.root, result.allPositions);

            return result;
        }

        // =========================================================
        // TREE BUILDING
        // =========================================================

        private static LayoutCycle BuildLayoutTree(DungeonCycle cycle, int depth, LayoutCycle parent)
        {
            var layout = new LayoutCycle
            {
                source = cycle,
                depth = depth,
                parent = parent,
                entranceNode = cycle.startNode,
                exitNode = cycle.goalNode
            };

            // Process rewrite sites to find children
            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site?.replacementPattern != null)
                    {
                        var child = BuildLayoutTree(site.replacementPattern, depth + 1, layout);
                        layout.children.Add(child);
                    }
                }
            }

            return layout;
        }

        private static void CollectAllCycles(LayoutCycle cycle, List<LayoutCycle> output)
        {
            if (cycle == null) return;

            output.Add(cycle);

            foreach (var child in cycle.children)
                CollectAllCycles(child, output);
        }

        private static void CollectAllPositions(LayoutCycle cycle, Dictionary<GraphNode, Vector2> output)
        {
            if (cycle == null) return;

            foreach (var kvp in cycle.nodePositions)
                output[kvp.Key] = kvp.Value;

            foreach (var child in cycle.children)
                CollectAllPositions(child, output);
        }

        // =========================================================
        // LAYOUT COMPUTATION
        // =========================================================

        private static void LayoutCycleRecursive(LayoutCycle layout, Vector2 center, float radius)
        {
            if (layout?.source == null)
                return;

            layout.center = center;
            layout.radius = Mathf.Max(radius, MinRadius);

            // Calculate actual radius needed for node spacing
            int nodeCount = layout.source.nodes?.Count ?? 0;
            if (nodeCount > 0)
            {
                float circumference = nodeCount * NodeSpacing;
                float minRadiusForSpacing = circumference / (2f * Mathf.PI);
                layout.radius = Mathf.Max(layout.radius, minRadiusForSpacing);
            }

            // Layout nodes in a circle
            LayoutNodesInCircle(layout);

            // Layout children
            if (layout.children.Count > 0)
            {
                LayoutChildren(layout);
            }
        }

        /// <summary>
        /// Layout all nodes of this cycle in a circle
        /// </summary>
        private static void LayoutNodesInCircle(LayoutCycle layout)
        {
            if (layout.source.nodes == null || layout.source.nodes.Count == 0)
                return;

            var nodes = layout.source.nodes;

            // Filter out null nodes
            var validNodes = new List<GraphNode>();
            foreach (var node in nodes)
            {
                if (node != null)
                    validNodes.Add(node);
            }

            int count = validNodes.Count;
            if (count == 0) return;

            // Find entrance index to rotate circle so entrance is at top
            int entranceIndex = -1;
            if (layout.entranceNode != null)
            {
                entranceIndex = validNodes.IndexOf(layout.entranceNode);
            }

            // If we have a parent, rotate entrance to face parent
            float baseAngle = -Mathf.PI * 0.5f; // Start at top (12 o'clock)

            if (layout.parent != null)
            {
                // Calculate angle from this cycle's center to parent's center
                Vector2 toParent = layout.parent.center - layout.center;
                if (toParent.magnitude > 0.001f)
                {
                    baseAngle = Mathf.Atan2(toParent.y, toParent.x);
                }
            }

            for (int i = 0; i < count; i++)
            {
                var node = validNodes[i];

                // Base angle for this position (evenly distributed)
                float angle = (i / (float)count) * Mathf.PI * 2f;

                // Rotate so entrance is at baseAngle
                if (entranceIndex >= 0)
                {
                    float entranceBaseAngle = (entranceIndex / (float)count) * Mathf.PI * 2f;
                    angle = angle - entranceBaseAngle + baseAngle;
                }

                Vector2 pos = layout.center + new Vector2(
                    Mathf.Cos(angle) * layout.radius,
                    Mathf.Sin(angle) * layout.radius
                );

                layout.nodePositions[node] = pos;

                // Store entrance/exit positions
                if (node == layout.entranceNode)
                    layout.entrancePos = pos;
                if (node == layout.exitNode)
                    layout.exitPos = pos;
            }
        }

        /// <summary>
        /// Layout child cycles around this cycle
        /// </summary>
        private static void LayoutChildren(LayoutCycle parent)
        {
            if (parent.children.Count == 0)
                return;

            // Distribute children evenly around parent circle
            int childCount = parent.children.Count;

            for (int i = 0; i < childCount; i++)
            {
                var child = parent.children[i];

                // Calculate angle for this child
                float angle = (i / (float)childCount) * Mathf.PI * 2f;

                // Calculate child radius (smaller at deeper levels)
                float childBaseRadius = parent.radius * DepthRadiusScale;

                // Position child outside parent circle
                float distance = parent.radius + childBaseRadius + CycleSpacing;
                Vector2 childCenter = parent.center + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance
                );

                // Recursively layout this child
                LayoutCycleRecursive(child, childCenter, childBaseRadius);
            }
        }
    }
}