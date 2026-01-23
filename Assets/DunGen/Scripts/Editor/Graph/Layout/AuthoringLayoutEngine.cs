using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Constraint-based hierarchical layout engine.
    /// Uses declarative positioning rules and automatic overlap resolution.
    /// UPDATED: Positions subcycles outside parent circles with aligned entrances to minimize edge crossings.
    /// </summary>
    public sealed class AuthoringLayoutEngine
    {
        // Layout configuration
        private const float BaseRadius = 250f;
        private const float RadiusScale = 0.65f;
        private const float MinRadius = 60f;
        private const float MinNodeSpacing = 100f;
        private const float SubcycleSpacing = 80f; // Spacing between parent and subcycle boundaries

        // Overlap resolution
        private const int MaxOverlapIterations = 50;
        private const float OverlapPushForce = 0.5f;

        /// <summary>
        /// Cycle boundary information for rendering "cycles within cycles".
        /// </summary>
        public readonly struct CycleVisualBounds
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly int Depth;

            public CycleVisualBounds(Vector2 center, float radius, int depth)
            {
                Center = center;
                Radius = radius;
                Depth = depth;
            }
        }

        /// <summary>
        /// Layout constraint types
        /// </summary>
        private enum ConstraintType
        {
            Fixed,           // Fixed position
            RelativeTo,      // Position relative to another node
            OnCircle,        // On a circle around center
            OnLine,          // On a line between two points
            InGrid,          // Grid position
        }

        /// <summary>
        /// A positioning constraint for a node
        /// </summary>
        private class LayoutConstraint
        {
            public GraphNode node;
            public ConstraintType type;
            public Vector2 position;         // For Fixed
            public GraphNode anchor;         // For RelativeTo
            public Vector2 offset;           // For RelativeTo
            public Vector2 center;           // For OnCircle
            public float radius;             // For OnCircle
            public float angle;              // For OnCircle
            public Vector2 lineStart;        // For OnLine
            public Vector2 lineEnd;          // For OnLine
            public float lineT;              // For OnLine (0-1)
            public int priority;             // Higher priority = less likely to move
        }

        /// <summary>
        /// Container for cycle layout information
        /// </summary>
        private class CycleLayoutNode
        {
            public DungeonCycle cycle;
            public List<GraphNode> ownedNodes = new List<GraphNode>();
            public List<CycleLayoutNode> subcycles = new List<CycleLayoutNode>();
            public Dictionary<GraphNode, CycleLayoutNode> placeholderToSubcycle = new Dictionary<GraphNode, CycleLayoutNode>();
            public int depth;
            public Vector2 center;
            public float radius;
        }

        // =========================================================
        // PUBLIC API
        // =========================================================

        public static Dictionary<GraphNode, Vector2> ComputeLayout(
            DungeonCycle rootCycle,
            float nodeRadius)
        {
            return ComputeLayout(rootCycle, nodeRadius, out _);
        }

        public static Dictionary<GraphNode, Vector2> ComputeLayout(
            DungeonCycle rootCycle,
            float nodeRadius,
            out List<CycleVisualBounds> cycleBounds)
        {
            cycleBounds = new List<CycleVisualBounds>();

            if (rootCycle == null)
                return new Dictionary<GraphNode, Vector2>();

            // Build hierarchy tree (requires real rewriteSites + replacementPattern)
            var root = BuildCycleHierarchy(rootCycle, depth: 0);

            // Generate constraints recursively
            var constraints = new List<LayoutConstraint>();
            GenerateConstraints(root, Vector2.zero, BaseRadius, constraints, cycleBounds);

            // Resolve constraints to positions
            var positions = ResolveConstraints(constraints, nodeRadius);

            // Resolve overlaps
            ResolveOverlaps(positions, nodeRadius);

            return positions;
        }

        // =========================================================
        // HIERARCHY BUILDING
        // =========================================================

        private static CycleLayoutNode BuildCycleHierarchy(DungeonCycle cycle, int depth)
        {
            var node = new CycleLayoutNode
            {
                cycle = cycle,
                depth = depth
            };

            if (cycle == null || cycle.nodes == null)
                return node;

            foreach (var cycleNode in cycle.nodes)
            {
                if (cycleNode != null)
                    node.ownedNodes.Add(cycleNode);
            }

            if (cycle.rewriteSites == null)
                return node;

            foreach (var site in cycle.rewriteSites)
            {
                if (site == null || site.placeholder == null || !site.HasReplacementPattern())
                    continue;

                var sub = BuildCycleHierarchy(site.replacementPattern, depth + 1);
                node.subcycles.Add(sub);
                node.placeholderToSubcycle[site.placeholder] = sub;
            }

            return node;
        }

        // =========================================================
        // CONSTRAINT GENERATION
        // =========================================================

        private static void GenerateConstraints(
            CycleLayoutNode cycleNode,
            Vector2 center,
            float radius,
            List<LayoutConstraint> constraints,
            List<CycleVisualBounds> boundsOut)
        {
            if (cycleNode == null || cycleNode.cycle == null)
                return;

            // Clamp radius by depth
            radius = Mathf.Max(radius, MinRadius);

            cycleNode.center = center;
            cycleNode.radius = radius;

            // Record this cycle boundary (for rendering rings)
            boundsOut?.Add(new CycleVisualBounds(center, radius, cycleNode.depth));

            // Place this cycle's owned nodes on a circle (no rotation for root)
            AddCircularConstraints(cycleNode.ownedNodes, center, radius, constraints);

            // Recurse into subcycles: position them OUTSIDE parent circle with aligned entrances
            foreach (var kvp in cycleNode.placeholderToSubcycle)
            {
                var placeholder = kvp.Key;
                var sub = kvp.Value;
                if (placeholder == null || sub == null) continue;

                // Find placeholder position on the parent circle
                Vector2 placeholderPos = GetConstraintPositionForNode(constraints, placeholder, center);

                // Calculate subcycle radius based on node count
                float subRadius = CalculateSubcycleRadius(sub, radius);

                // Direction from parent center toward placeholder
                Vector2 direction = (placeholderPos - center).normalized;

                // Position subcycle OUTSIDE parent circle
                float offsetDistance = radius + subRadius + SubcycleSpacing;
                Vector2 subcycleCenter = center + direction * offsetDistance;

                // Calculate angle from subcycle center back to parent center
                // This is the angle where we want the subcycle's entrance to face
                float entranceAngle = Mathf.Atan2(center.y - subcycleCenter.y, center.x - subcycleCenter.x);

                // Generate constraints for subcycle with aligned entrance
                GenerateConstraintsAligned(sub, subcycleCenter, subRadius, constraints, boundsOut, entranceAngle);
            }
        }

        /// <summary>
        /// Generate constraints for a subcycle with entrance aligned toward parent
        /// </summary>
        private static void GenerateConstraintsAligned(
            CycleLayoutNode cycleNode,
            Vector2 center,
            float radius,
            List<LayoutConstraint> constraints,
            List<CycleVisualBounds> boundsOut,
            float entranceAngle)
        {
            if (cycleNode == null || cycleNode.cycle == null)
                return;

            radius = Mathf.Max(radius, MinRadius);

            cycleNode.center = center;
            cycleNode.radius = radius;

            boundsOut?.Add(new CycleVisualBounds(center, radius, cycleNode.depth));

            // Find entrance node (startNode of the cycle)
            GraphNode entranceNode = cycleNode.cycle.startNode;

            // Place nodes on circle with entrance aligned
            AddCircularConstraintsAligned(
                cycleNode.ownedNodes,
                center,
                radius,
                constraints,
                entranceNode,
                entranceAngle
            );

            // Recurse into subcycles
            foreach (var kvp in cycleNode.placeholderToSubcycle)
            {
                var placeholder = kvp.Key;
                var sub = kvp.Value;
                if (placeholder == null || sub == null) continue;

                Vector2 placeholderPos = GetConstraintPositionForNode(constraints, placeholder, center);
                float subRadius = CalculateSubcycleRadius(sub, radius);

                Vector2 direction = (placeholderPos - center).normalized;
                float offsetDistance = radius + subRadius + SubcycleSpacing;
                Vector2 subcycleCenter = center + direction * offsetDistance;

                float subEntranceAngle = Mathf.Atan2(center.y - subcycleCenter.y, center.x - subcycleCenter.x);

                GenerateConstraintsAligned(sub, subcycleCenter, subRadius, constraints, boundsOut, subEntranceAngle);
            }
        }

        /// <summary>
        /// Calculate appropriate radius for a subcycle based on its node count
        /// </summary>
        private static float CalculateSubcycleRadius(CycleLayoutNode sub, float parentRadius)
        {
            if (sub == null) return MinRadius;

            // Base subcycle radius on number of nodes
            int nodeCount = sub.ownedNodes.Count;

            // Calculate minimum radius needed for node spacing
            float minRadiusForSpacing = (nodeCount * MinNodeSpacing) / (2f * Mathf.PI);

            // Use smaller of scaled parent or calculated minimum
            float scaledRadius = parentRadius * RadiusScale;

            // Ensure we have enough space
            return Mathf.Max(Mathf.Max(scaledRadius, minRadiusForSpacing), MinRadius);
        }

        /// <summary>
        /// Add circular constraints without rotation (for root cycle)
        /// </summary>
        private static void AddCircularConstraints(
            List<GraphNode> nodes,
            Vector2 center,
            float radius,
            List<LayoutConstraint> constraints)
        {
            AddCircularConstraintsAligned(nodes, center, radius, constraints, null, 0f);
        }

        /// <summary>
        /// Add circular constraints with entrance node aligned to specified angle
        /// </summary>
        private static void AddCircularConstraintsAligned(
            List<GraphNode> nodes,
            Vector2 center,
            float radius,
            List<LayoutConstraint> constraints,
            GraphNode entranceNode,
            float entranceAngle)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            // Filter nulls
            var valid = new List<GraphNode>();
            foreach (var n in nodes)
                if (n != null) valid.Add(n);

            int count = valid.Count;
            if (count == 0) return;

            // Enforce minimum spacing by increasing radius if needed
            float minRadiusForSpacing = (count * MinNodeSpacing) / (2f * Mathf.PI);
            float usedRadius = Mathf.Max(radius, minRadiusForSpacing, MinRadius);

            // Find entrance node index
            int entranceIndex = -1;
            if (entranceNode != null)
            {
                entranceIndex = valid.IndexOf(entranceNode);
            }

            for (int i = 0; i < count; i++)
            {
                // Calculate base angle (evenly distributed around circle)
                float angle = (i / (float)count) * Mathf.PI * 2f;

                // Rotate so entrance node is at specified angle
                if (entranceIndex >= 0)
                {
                    float entranceBaseAngle = (entranceIndex / (float)count) * Mathf.PI * 2f;
                    angle = angle - entranceBaseAngle + entranceAngle;
                }

                constraints.Add(new LayoutConstraint
                {
                    node = valid[i],
                    type = ConstraintType.OnCircle,
                    center = center,
                    radius = usedRadius,
                    angle = angle,
                    priority = 10
                });
            }
        }

        private static Vector2 GetConstraintPositionForNode(List<LayoutConstraint> constraints, GraphNode node, Vector2 fallback)
        {
            if (constraints == null || node == null)
                return fallback;

            for (int i = constraints.Count - 1; i >= 0; i--)
            {
                var c = constraints[i];
                if (c != null && c.node == node)
                {
                    // Approximate where this constraint will resolve
                    if (c.type == ConstraintType.Fixed) return c.position;
                    if (c.type == ConstraintType.OnCircle)
                        return c.center + new Vector2(Mathf.Cos(c.angle), Mathf.Sin(c.angle)) * c.radius;
                    if (c.type == ConstraintType.RelativeTo && c.anchor != null)
                        return fallback + c.offset;
                }
            }

            return fallback;
        }

        // =========================================================
        // CONSTRAINT RESOLUTION
        // =========================================================

        private static Dictionary<GraphNode, Vector2> ResolveConstraints(List<LayoutConstraint> constraints, float nodeRadius)
        {
            var positions = new Dictionary<GraphNode, Vector2>();
            if (constraints == null) return positions;

            // First pass: compute initial positions from constraint definitions
            foreach (var c in constraints)
            {
                if (c == null || c.node == null) continue;

                Vector2 p = Vector2.zero;

                switch (c.type)
                {
                    case ConstraintType.Fixed:
                        p = c.position;
                        break;

                    case ConstraintType.OnCircle:
                        p = c.center + new Vector2(Mathf.Cos(c.angle), Mathf.Sin(c.angle)) * c.radius;
                        break;

                    case ConstraintType.RelativeTo:
                        if (c.anchor != null && positions.TryGetValue(c.anchor, out var ap))
                            p = ap + c.offset;
                        else
                            p = c.offset;
                        break;

                    case ConstraintType.OnLine:
                        p = Vector2.Lerp(c.lineStart, c.lineEnd, Mathf.Clamp01(c.lineT));
                        break;

                    default:
                        p = Vector2.zero;
                        break;
                }

                positions[c.node] = p;
            }

            return positions;
        }

        // =========================================================
        // OVERLAP RESOLUTION
        // =========================================================

        private static void ResolveOverlaps(Dictionary<GraphNode, Vector2> positions, float nodeRadius)
        {
            if (positions == null || positions.Count < 2)
                return;

            float minDist = nodeRadius * 2.2f;

            var nodes = new List<GraphNode>(positions.Keys);

            for (int iter = 0; iter < MaxOverlapIterations; iter++)
            {
                bool anyMoved = false;

                for (int i = 0; i < nodes.Count; i++)
                {
                    var a = nodes[i];
                    if (a == null) continue;

                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var b = nodes[j];
                        if (b == null) continue;

                        Vector2 pa = positions[a];
                        Vector2 pb = positions[b];

                        Vector2 d = pb - pa;
                        float dist = d.magnitude;

                        if (dist < 0.0001f)
                        {
                            d = new Vector2(1f, 0f);
                            dist = 0.0001f;
                        }

                        if (dist < minDist)
                        {
                            float push = (minDist - dist) * OverlapPushForce;
                            Vector2 dir = d / dist;

                            positions[a] = pa - dir * push * 0.5f;
                            positions[b] = pb + dir * push * 0.5f;
                            anyMoved = true;
                        }
                    }
                }

                if (!anyMoved)
                    break;
            }
        }
    }
}