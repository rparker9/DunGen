using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Constraint-based hierarchical layout engine.
    /// Uses declarative positioning rules and automatic overlap resolution.
    /// </summary>
    public static class GraphLayoutEngine
    {
        // Layout configuration
        private const float BaseRadius = 250f;
        private const float RadiusScale = 0.65f;
        private const float MinRadius = 60f;
        private const float MinNodeSpacing = 100f;

        // Overlap resolution
        private const int MaxOverlapIterations = 50;
        private const float OverlapPushForce = 0.5f;

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
            public CycleNode node;
            public ConstraintType type;
            public Vector2 position;         // For Fixed
            public CycleNode anchor;         // For RelativeTo
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
            public List<CycleNode> ownedNodes = new List<CycleNode>();
            public List<CycleLayoutNode> subcycles = new List<CycleLayoutNode>();
            public Dictionary<CycleNode, CycleLayoutNode> placeholderToSubcycle = new Dictionary<CycleNode, CycleLayoutNode>();
            public int depth;
            public Vector2 center;
            public float radius;
        }

        // =========================================================
        // PUBLIC API
        // =========================================================

        public static Dictionary<CycleNode, Vector2> ComputeLayout(
            DungeonCycle rootCycle,
            float nodeRadius)
        {
            if (rootCycle == null)
                return new Dictionary<CycleNode, Vector2>();

            // Build hierarchy tree
            var root = BuildCycleHierarchy(rootCycle, depth: 0);

            // Generate constraints recursively
            var constraints = new List<LayoutConstraint>();
            GenerateConstraints(root, Vector2.zero, BaseRadius, constraints);

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

            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site != null && site.HasReplacement() && site.placeholder != null)
                    {
                        var subcycle = BuildCycleHierarchy(site.replacementPattern, depth + 1);
                        node.subcycles.Add(subcycle);
                        node.placeholderToSubcycle[site.placeholder] = subcycle;
                    }
                }
            }

            return node;
        }

        // =========================================================
        // CONSTRAINT GENERATION
        // =========================================================

        private static void GenerateConstraints(
            CycleLayoutNode cycleNode,
            Vector2 center,
            float availableRadius,
            List<LayoutConstraint> constraints)
        {
            cycleNode.center = center;
            cycleNode.radius = Mathf.Max(MinRadius, availableRadius);

            // Generate constraints for this cycle
            GenerateConstraintsForCycle(cycleNode, constraints);

            // Generate constraints for subcycles
            foreach (var kvp in cycleNode.placeholderToSubcycle)
            {
                var placeholder = kvp.Key;
                var subcycle = kvp.Value;

                // Find placeholder's constraint to get its position
                var placeholderConstraint = constraints.Find(c => c.node == placeholder);
                if (placeholderConstraint != null)
                {
                    Vector2 subcycleCenter = ResolveConstraintPosition(placeholderConstraint);
                    float subcycleRadius = cycleNode.radius * RadiusScale;
                    GenerateConstraints(subcycle, subcycleCenter, subcycleRadius, constraints);
                }
            }
        }

        private static void GenerateConstraintsForCycle(
            CycleLayoutNode cycleNode,
            List<LayoutConstraint> constraints)
        {
            if (cycleNode.cycle == null)
                return;

            switch (cycleNode.cycle.type)
            {
                case CycleType.TwoAlternativePaths:
                    GenerateTwoAlternativePathsConstraints(cycleNode, constraints);
                    break;

                case CycleType.TwoKeys:
                    GenerateTwoKeysConstraints(cycleNode, constraints);
                    break;

                case CycleType.HiddenShortcut:
                    GenerateHiddenShortcutConstraints(cycleNode, constraints);
                    break;

                case CycleType.ForeshadowingLoop:
                    GenerateForeshadowingLoopConstraints(cycleNode, constraints);
                    break;

                case CycleType.SimpleLockAndKey:
                    GenerateSimpleLockAndKeyConstraints(cycleNode, constraints);
                    break;

                case CycleType.DangerousRoute:
                    GenerateDangerousRouteConstraints(cycleNode, constraints);
                    break;

                case CycleType.LockAndKeyCycle:
                    GenerateLockAndKeyCycleConstraints(cycleNode, constraints);
                    break;

                case CycleType.BlockedRetreat:
                    GenerateBlockedRetreatConstraints(cycleNode, constraints);
                    break;

                case CycleType.MonsterPatrol:
                    GenerateMonsterPatrolConstraints(cycleNode, constraints);
                    break;

                case CycleType.AlteredReturn:
                    GenerateAlteredReturnConstraints(cycleNode, constraints);
                    break;

                case CycleType.FalseGoal:
                    GenerateFalseGoalConstraints(cycleNode, constraints);
                    break;

                case CycleType.Gambit:
                    GenerateGambitConstraints(cycleNode, constraints);
                    break;

                default:
                    GenerateDefaultConstraints(cycleNode, constraints);
                    break;
            }
        }

        // =========================================================
        // CONSTRAINT GENERATORS (Declarative)
        // =========================================================

        private static void GenerateTwoAlternativePathsConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            // Start and Goal on horizontal axis
            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r, priority: 100);

            // Two paths above and below
            var sites = GetRewriteSites(cycle);
            if (sites.Count >= 1)
                AddFixedConstraint(constraints, sites[0], cycleNode.center + new Vector2(-r * 0.2f, r * 0.8f), priority: 90);
            if (sites.Count >= 2)
                AddFixedConstraint(constraints, sites[1], cycleNode.center + new Vector2(-r * 0.2f, -r * 0.8f), priority: 90);
        }

        private static void GenerateTwoKeysConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.5f;

            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r, priority: 100);

            // FIXED: Layout the path rewrite sites instead of key nodes (keys are now on edges)
            var sites = GetRewriteSites(cycle);
            if (sites.Count >= 1)
                AddFixedConstraint(constraints, sites[0], cycleNode.center + Vector2.up * r * 0.85f, priority: 90);
            if (sites.Count >= 2)
                AddFixedConstraint(constraints, sites[1], cycleNode.center + Vector2.down * r * 0.85f, priority: 90);
        }

        private static void GenerateHiddenShortcutConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r, priority: 100);

            // Secret path below
            var secretNode = FindNodeWithRole(cycle, NodeRoleType.Secret);
            if (secretNode != null)
                AddFixedConstraint(constraints, secretNode, cycleNode.center + Vector2.down * r * 0.7f, priority: 90);

            // Remaining nodes on upper path
            var remaining = GetRemainingNodes(cycle, constraints);
            AddLineConstraints(constraints, remaining,
                cycleNode.center + new Vector2(-r * 0.5f, r * 0.6f),
                cycleNode.center + new Vector2(r * 0.5f, r * 0.6f),
                priority: 80);
        }

        private static void GenerateForeshadowingLoopConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.5f;

            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r, priority: 100);

            // Sites in bottom arc
            var sites = GetRewriteSites(cycle);
            for (int i = 0; i < sites.Count; i++)
            {
                float angle = Mathf.PI + (i + 1) * Mathf.PI / (sites.Count + 1);
                AddCircleConstraint(constraints, sites[i], cycleNode.center, r * 0.9f, angle, priority: 80);
            }
        }

        private static void GenerateSimpleLockAndKeyConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            var orderedNodes = new List<CycleNode>();
            orderedNodes.Add(cycle.startNode);
            orderedNodes.AddRange(GetRewriteSites(cycle));
            orderedNodes.Add(cycle.goalNode);

            // FIXED: Key room is already in the rewrite sites, no need to find it separately
            // The layout now includes: start -> site(s) -> goal -> keyRoom (all from rewrite sites)

            AddLineConstraints(constraints, orderedNodes,
                cycleNode.center + Vector2.left * r,
                cycleNode.center + Vector2.right * r,
                priority: 90);
        }

        private static void GenerateDangerousRouteConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r, priority: 100);

            var dangerNode = FindNodeWithRole(cycle, NodeRoleType.Danger);
            if (dangerNode != null)
                AddFixedConstraint(constraints, dangerNode, cycleNode.center + Vector2.down * r * 0.65f, priority: 90);

            // Safe path on top
            var sites = GetRewriteSites(cycle);
            AddLineConstraints(constraints, sites,
                cycleNode.center + new Vector2(-r * 0.3f, r * 0.75f),
                cycleNode.center + new Vector2(r * 0.3f, r * 0.75f),
                priority: 80);
        }

        private static void GenerateLockAndKeyCycleConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            var orderedNodes = new List<CycleNode>();

            orderedNodes.Add(cycle.startNode);
            orderedNodes.Add(cycle.goalNode);
            orderedNodes.AddRange(GetRewriteSites(cycle));

            // FIXED: Key room is already in the rewrite sites, no need to find it separately
            // The circular layout will include: start -> goal -> site1 -> site2 -> keyRoom (all sites)

            AddCircularConstraints(constraints, orderedNodes, cycleNode.center, cycleNode.radius * 0.5f, priority: 90);
        }

        private static void GenerateBlockedRetreatConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            var orderedNodes = new List<CycleNode>();

            orderedNodes.Add(cycle.startNode);
            orderedNodes.Add(cycle.goalNode);

            var barrierNode = FindNodeWithRole(cycle, NodeRoleType.Barrier);
            if (barrierNode != null)
                orderedNodes.Add(barrierNode);

            orderedNodes.AddRange(GetRewriteSites(cycle));

            AddCircularConstraints(constraints, orderedNodes, cycleNode.center, cycleNode.radius * 0.5f, priority: 90);
        }

        private static void GenerateMonsterPatrolConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            AddCircularConstraints(constraints, cycle.nodes, cycleNode.center, cycleNode.radius * 0.5f, priority: 90);
        }

        private static void GenerateAlteredReturnConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            AddCircularConstraints(constraints, cycle.nodes, cycleNode.center, cycleNode.radius * 0.4f, priority: 90);
        }

        private static void GenerateFalseGoalConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            var orderedNodes = new List<CycleNode>();
            orderedNodes.Add(cycle.startNode);

            var falseGoal = FindNodeWithRole(cycle, NodeRoleType.FalseGoal);
            if (falseGoal != null)
                orderedNodes.Add(falseGoal);

            orderedNodes.AddRange(GetRewriteSites(cycle));
            orderedNodes.Add(cycle.goalNode);

            AddLineConstraints(constraints, orderedNodes,
                cycleNode.center + Vector2.left * r,
                cycleNode.center + Vector2.right * r,
                priority: 90);
        }

        private static void GenerateGambitConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            float r = cycleNode.radius * 0.45f;

            AddFixedConstraint(constraints, cycle.startNode, cycleNode.center + Vector2.left * r, priority: 100);
            AddFixedConstraint(constraints, cycle.goalNode, cycleNode.center + Vector2.right * r * 0.6f, priority: 100);

            var sites = GetRewriteSites(cycle);
            if (sites.Count > 0)
                AddFixedConstraint(constraints, sites[0], cycleNode.center + Vector2.left * r * 0.3f, priority: 90);

            var dangerNode = FindNodeWithRole(cycle, NodeRoleType.Danger);
            var rewardNode = FindNodeWithRole(cycle, NodeRoleType.Reward);

            if (dangerNode != null)
                AddFixedConstraint(constraints, dangerNode, cycleNode.center + new Vector2(r * 0.6f, -r * 0.8f), priority: 80);
            if (rewardNode != null)
                AddFixedConstraint(constraints, rewardNode, cycleNode.center + new Vector2(r * 1.1f, -r * 0.8f), priority: 80);
        }

        private static void GenerateDefaultConstraints(CycleLayoutNode cycleNode, List<LayoutConstraint> constraints)
        {
            var cycle = cycleNode.cycle;
            if (cycle != null)
                AddCircularConstraints(constraints, cycle.nodes, cycleNode.center, cycleNode.radius * 0.5f, priority: 90);
        }

        // =========================================================
        // CONSTRAINT HELPERS
        // =========================================================

        private static void AddFixedConstraint(List<LayoutConstraint> constraints, CycleNode node, Vector2 position, int priority)
        {
            if (node == null) return;
            constraints.Add(new LayoutConstraint
            {
                node = node,
                type = ConstraintType.Fixed,
                position = position,
                priority = priority
            });
        }

        private static void AddCircleConstraint(List<LayoutConstraint> constraints, CycleNode node, Vector2 center, float radius, float angle, int priority)
        {
            if (node == null) return;
            constraints.Add(new LayoutConstraint
            {
                node = node,
                type = ConstraintType.OnCircle,
                center = center,
                radius = radius,
                angle = angle,
                priority = priority
            });
        }

        private static void AddCircularConstraints(List<LayoutConstraint> constraints, List<CycleNode> nodes, Vector2 center, float radius, int priority)
        {
            if (nodes == null) return;

            int validCount = 0;
            foreach (var node in nodes)
            {
                if (node != null && !HasConstraint(constraints, node))
                    validCount++;
            }

            if (validCount == 0) return;

            float angleStep = (Mathf.PI * 2f) / validCount;
            int index = 0;

            foreach (var node in nodes)
            {
                if (node == null || HasConstraint(constraints, node)) continue;

                float angle = index * angleStep - Mathf.PI * 0.5f;
                AddCircleConstraint(constraints, node, center, radius, angle, priority);
                index++;
            }
        }

        private static void AddLineConstraints(List<LayoutConstraint> constraints, List<CycleNode> nodes, Vector2 start, Vector2 end, int priority)
        {
            if (nodes == null || nodes.Count == 0) return;

            // Filter out nodes that already have constraints
            var validNodes = new List<CycleNode>();
            foreach (var node in nodes)
            {
                if (node != null && !HasConstraint(constraints, node))
                    validNodes.Add(node);
            }

            if (validNodes.Count == 0) return;

            for (int i = 0; i < validNodes.Count; i++)
            {
                float t = validNodes.Count == 1 ? 0.5f : i / (float)(validNodes.Count - 1);
                constraints.Add(new LayoutConstraint
                {
                    node = validNodes[i],
                    type = ConstraintType.OnLine,
                    lineStart = start,
                    lineEnd = end,
                    lineT = t,
                    priority = priority
                });
            }
        }

        private static bool HasConstraint(List<LayoutConstraint> constraints, CycleNode node)
        {
            foreach (var c in constraints)
            {
                if (c.node == node)
                    return true;
            }
            return false;
        }

        // =========================================================
        // CONSTRAINT RESOLUTION
        // =========================================================

        private static Dictionary<CycleNode, Vector2> ResolveConstraints(List<LayoutConstraint> constraints, float nodeRadius)
        {
            var positions = new Dictionary<CycleNode, Vector2>();

            foreach (var constraint in constraints)
            {
                if (constraint.node == null) continue;

                Vector2 pos = ResolveConstraintPosition(constraint);
                positions[constraint.node] = pos;
            }

            return positions;
        }

        private static Vector2 ResolveConstraintPosition(LayoutConstraint constraint)
        {
            switch (constraint.type)
            {
                case ConstraintType.Fixed:
                    return constraint.position;

                case ConstraintType.OnCircle:
                    return constraint.center + new Vector2(
                        Mathf.Cos(constraint.angle) * constraint.radius,
                        Mathf.Sin(constraint.angle) * constraint.radius
                    );

                case ConstraintType.OnLine:
                    return Vector2.Lerp(constraint.lineStart, constraint.lineEnd, constraint.lineT);

                default:
                    return Vector2.zero;
            }
        }

        // =========================================================
        // OVERLAP RESOLUTION
        // =========================================================

        private static void ResolveOverlaps(Dictionary<CycleNode, Vector2> positions, float nodeRadius)
        {
            float minDist = MinNodeSpacing;
            var nodes = new List<CycleNode>(positions.Keys);

            for (int iter = 0; iter < MaxOverlapIterations; iter++)
            {
                bool hadOverlap = false;

                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var nodeA = nodes[i];
                        var nodeB = nodes[j];

                        Vector2 posA = positions[nodeA];
                        Vector2 posB = positions[nodeB];

                        Vector2 delta = posB - posA;
                        float dist = delta.magnitude;

                        if (dist < minDist && dist > 0.001f)
                        {
                            hadOverlap = true;

                            // Push apart
                            Vector2 push = delta.normalized * (minDist - dist) * 0.5f * OverlapPushForce;
                            positions[nodeA] -= push;
                            positions[nodeB] += push;
                        }
                    }
                }

                if (!hadOverlap)
                    break;
            }
        }

        // =========================================================
        // NODE FINDING HELPERS
        // =========================================================

        private static CycleNode FindNodeWithRole(DungeonCycle cycle, NodeRoleType roleType)
        {
            if (cycle == null || cycle.nodes == null)
                return null;

            foreach (var node in cycle.nodes)
            {
                if (node != null && node.HasRole(roleType))
                    return node;
            }

            return null;
        }

        private static List<CycleNode> FindNodesWithRole(DungeonCycle cycle, NodeRoleType roleType)
        {
            var result = new List<CycleNode>();
            if (cycle == null || cycle.nodes == null)
                return result;

            foreach (var node in cycle.nodes)
            {
                if (node != null && node.HasRole(roleType))
                    result.Add(node);
            }

            return result;
        }

        private static List<CycleNode> GetRewriteSites(DungeonCycle cycle)
        {
            var result = new List<CycleNode>();
            if (cycle == null || cycle.rewriteSites == null)
                return result;

            foreach (var site in cycle.rewriteSites)
            {
                if (site != null && site.placeholder != null)
                    result.Add(site.placeholder);
            }

            return result;
        }

        private static List<CycleNode> GetRemainingNodes(DungeonCycle cycle, List<LayoutConstraint> existingConstraints)
        {
            var result = new List<CycleNode>();
            if (cycle == null || cycle.nodes == null)
                return result;

            foreach (var node in cycle.nodes)
            {
                if (node != null && !HasConstraint(existingConstraints, node))
                    result.Add(node);
            }

            return result;
        }
    }
}