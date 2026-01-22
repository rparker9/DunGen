using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Editor
{
    /// <summary>
    /// Provides visual styling for nodes: colors, labels, and depth tracking.
    /// UPDATED: Slot markers for subcycle Start/Goal.
    /// </summary>
    public sealed class NodeStyleProvider
    {
        // Node depth tracking (0 = root cycle, 1+ = subcycle)
        private readonly Dictionary<CycleNode, int> _nodeDepth = new Dictionary<CycleNode, int>();

        // Base colors
        private static readonly Color StartColor = new Color(0.25f, 0.85f, 0.30f);
        private static readonly Color GoalColor = new Color(0.85f, 0.28f, 0.28f);
        private static readonly Color EntranceColor = Color.Lerp(StartColor, Color.white, 0.25f);
        private static readonly Color ExitColor = Color.Lerp(GoalColor, Color.white, 0.25f);
        private static readonly Color RewriteSiteColor = new Color(0.90f, 0.85f, 0.30f);
        private static readonly Color DefaultColor = Color.white;

        // Slot marker alpha (subcycle start/goal)
        private const float SlotAlpha = 0.35f;

        // =========================================================
        // DEPTH TRACKING
        // =========================================================

        public void BuildDepthMap(DungeonCycle rootCycle)
        {
            _nodeDepth.Clear();
            if (rootCycle == null) return;
            MarkCycleNodesRecursive(rootCycle, depth: 0);
        }

        private void MarkCycleNodesRecursive(DungeonCycle cycle, int depth)
        {
            if (cycle == null || cycle.nodes == null) return;

            foreach (var node in cycle.nodes)
            {
                if (node == null) continue;

                if (_nodeDepth.TryGetValue(node, out int existing))
                    _nodeDepth[node] = Mathf.Min(existing, depth);
                else
                    _nodeDepth[node] = depth;
            }

            if (cycle.rewriteSites == null) return;

            foreach (var site in cycle.rewriteSites)
            {
                if (site == null || !site.HasReplacementPattern()) continue;
                MarkCycleNodesRecursive(site.replacementPattern, depth + 1);
            }
        }

        public bool IsSubcycleNode(CycleNode node)
        {
            if (node == null) return false;
            return _nodeDepth.TryGetValue(node, out int d) && d > 0;
        }

        public bool IsSlotMarkerNode(CycleNode node)
        {
            if (node == null) return false;
            if (!_nodeDepth.TryGetValue(node, out int d)) return false;
            if (d <= 0) return false;

            // Subcycle Start/Goal are “slot markers”
            return node.HasRole(NodeRoleType.Start) || node.HasRole(NodeRoleType.Goal);
        }

        public void Clear()
        {
            _nodeDepth.Clear();
        }

        // =========================================================
        // NODE COLORING
        // =========================================================

        public Color GetNodeColor(CycleNode node, DungeonCycle rootCycle)
        {
            if (node == null) return DefaultColor;

            bool isSub = IsSubcycleNode(node);

            Color nodeColor = DefaultColor;

            // Start/Goal, with subcycle entrance/exit tints
            if (node.HasRole(NodeRoleType.Start))
                nodeColor = isSub ? EntranceColor : StartColor;
            else if (node.HasRole(NodeRoleType.Goal))
                nodeColor = isSub ? ExitColor : GoalColor;

            // Rewrite sites (authoring): highlight if this node is actually a rewrite site
            bool isRewriteSite = FindRewriteSiteRecursive(rootCycle, node) != null;
            if (!node.HasRole(NodeRoleType.Start) && !node.HasRole(NodeRoleType.Goal) && isRewriteSite)
                nodeColor = RewriteSiteColor;

            // Role-based tints (kept subtle so Start/Goal remain obvious)
            if (!node.HasRole(NodeRoleType.Start) && !node.HasRole(NodeRoleType.Goal))
            {
                if (node.HasRole(NodeRoleType.Barrier))
                    nodeColor = Color.Lerp(nodeColor, new Color(0.55f, 0.75f, 0.95f), 0.35f);
                else if (node.HasRole(NodeRoleType.Secret))
                    nodeColor = Color.Lerp(nodeColor, new Color(0.75f, 0.60f, 0.95f), 0.35f);
                else if (node.HasRole(NodeRoleType.Danger))
                    nodeColor = Color.Lerp(nodeColor, new Color(0.95f, 0.55f, 0.25f), 0.35f);
                else if (node.HasRole(NodeRoleType.FalseGoal))
                    nodeColor = Color.Lerp(nodeColor, new Color(0.95f, 0.75f, 0.25f), 0.35f);
                else if (node.HasRole(NodeRoleType.Patrol))
                    nodeColor = Color.Lerp(nodeColor, new Color(0.55f, 0.95f, 0.80f), 0.25f);
            }

            // Slot markers: fade them out (faint “slot”)
            if (IsSlotMarkerNode(node))
                nodeColor.a = SlotAlpha;

            return nodeColor;
        }

        // =========================================================
        // NODE LABELING
        // =========================================================

        public string GetNodeLabel(CycleNode node)
        {
            if (node == null) return "";

            bool isSubcycle = IsSubcycleNode(node);

            // Start/Goal become Entrance/Exit when nested as a subcycle.
            if (node.HasRole(NodeRoleType.Start))
                return isSubcycle ? "ENTRANCE" : "START";

            if (node.HasRole(NodeRoleType.Goal))
                return isSubcycle ? "EXIT" : "GOAL";

            // Common role tags
            if (node.HasRole(NodeRoleType.Barrier)) return "BARRIER";
            if (node.HasRole(NodeRoleType.Secret) && node.HasRole(NodeRoleType.Danger)) return "SECRET\nDANGER";
            if (node.HasRole(NodeRoleType.Secret)) return "SECRET";
            if (node.HasRole(NodeRoleType.Danger)) return "DANGER";
            if (node.HasRole(NodeRoleType.FalseGoal)) return "FALSE GOAL";
            if (node.HasRole(NodeRoleType.Patrol)) return "PATROL";
            if (node.HasRole(NodeRoleType.Reward)) return "REWARD";

            return "NODE";
        }

        // =========================================================
        // HELPER: Find rewrite site
        // =========================================================

        private static RewriteSite FindRewriteSiteRecursive(DungeonCycle pattern, CycleNode node)
        {
            if (pattern == null || node == null || pattern.rewriteSites == null)
                return null;

            // Check direct sites
            foreach (var site in pattern.rewriteSites)
            {
                if (site != null && site.placeholder == node)
                    return site;
            }

            // Check nested sites
            foreach (var site in pattern.rewriteSites)
            {
                if (site != null && site.HasReplacementPattern())
                {
                    var found = FindRewriteSiteRecursive(site.replacementPattern, node);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }
    }
}
