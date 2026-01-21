using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Represents a complete dungeon cycle (overall or sub-cycle)
    /// </summary>
    [System.Serializable]
    public class DungeonCycle
    {
        public CycleType type;
        public CycleNode startNode;
        public CycleNode goalNode;

        public List<CycleNode> nodes = new List<CycleNode>();
        public readonly List<CycleEdge> edges = new List<CycleEdge>();

        // Nodes that can be rewritten into a replacement pattern.
        public List<RewriteSite> rewriteSites = new List<RewriteSite>();

        public DungeonCycle(CycleType cycleType)
        {
            type = cycleType;
            GenerateCycleStructure();
        }

        /// <summary>
        /// Generate the base structure for this cycle type
        /// </summary>
        private void GenerateCycleStructure()
        {
            // Clear existing data
            nodes.Clear();
            edges.Clear();
            rewriteSites.Clear();

            // Create start node
            startNode = new CycleNode(type);
            startNode.AddRole(NodeRoleType.Start);
            nodes.Add(startNode);

            // Create goal node
            goalNode = new CycleNode(type);
            goalNode.AddRole(NodeRoleType.Goal);
            nodes.Add(goalNode);

            // Add cycle-specific nodes based on type
            switch (type)
            {
                case CycleType.TwoAlternativePaths:
                    CreateTwoAlternativePaths();
                    break;
                case CycleType.TwoKeys:
                    CreateTwoKeys();
                    break;
                case CycleType.HiddenShortcut:
                    CreateHiddenShortcut();
                    break;
                case CycleType.ForeshadowingLoop:
                    CreateForeshadowingLoop();
                    break;
                case CycleType.SimpleLockAndKey:
                    CreateSimpleLockAndKey();
                    break;
                case CycleType.DangerousRoute:
                    CreateDangerousRoute();
                    break;
                case CycleType.LockAndKeyCycle:
                    CreateLockAndKeyCycle();
                    break;
                case CycleType.BlockedRetreat:
                    CreateBlockedRetreat();
                    break;
                case CycleType.MonsterPatrol:
                    CreateMonsterPatrol();
                    break;
                case CycleType.AlteredReturn:
                    CreateAlteredReturn();
                    break;
                case CycleType.FalseGoal:
                    CreateFalseGoal();
                    break;
                case CycleType.Gambit:
                    CreateGambit();
                    break;
                default:
                    CreateDefaultCycle();
                    break;
            }
        }

        private CycleEdge AddEdge(CycleNode from, CycleNode to, bool bidirectional = true, bool isBlocked = false, bool hasSightline = false)
        {
            var edge = new CycleEdge(from, to, bidirectional, isBlocked, hasSightline);
            edges.Add(edge);
            return edge;
        }

        private void CreateDefaultCycle()
        {
            // Simple connection with one rewrite site
            var site = new CycleNode(type);
            nodes.Add(site);
            rewriteSites.Add(new RewriteSite(site));

            // Connect: start <-> site <-> goal
            AddEdge(startNode, site);
            AddEdge(site, goalNode);
        }

        private void CreateTwoAlternativePaths()
        {
            // Rewrite site for path #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Rewrite site for path #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> site1 <-> goal
            AddEdge(startNode, site1);
            AddEdge(site1, goalNode);
            AddEdge(goalNode, site2);
        }

        private void CreateTwoKeys()
        {
            // Path node #1 - grants key 1
            var path1 = new CycleNode(type);
            path1.label = "Path 1";
            path1.AddGrantedKey(1);
            nodes.Add(path1);
            rewriteSites.Add(new RewriteSite(path1));

            // Path node #2 - grants key 2
            var path2 = new CycleNode(type);
            path2.label = "Path 2";
            path2.AddGrantedKey(2);
            nodes.Add(path2);
            rewriteSites.Add(new RewriteSite(path2));

            // Connect: start <-> path1 <-> goal
            AddEdge(startNode, path1);
            var edge1 = AddEdge(path1, goalNode);
            edge1.AddRequiredKey(2); // Need key 2 to enter goal from path1

            // Connect: goal <-> path2 <-> start
            AddEdge(goalNode, path2);
            var edge2 = AddEdge(path2, startNode);

            // Edge from path2 to goal requires key 1
            var edgeFromPath2 = FindEdge(path2, goalNode);
            if (edgeFromPath2 != null)
                edgeFromPath2.AddRequiredKey(1);
        }

        private void CreateHiddenShortcut()
        {
            // Rewrite site #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Rewrite site #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Secret shortcut node
            var secret = new CycleNode(type);
            secret.AddRole(NodeRoleType.Secret);
            nodes.Add(secret);

            // Connect: start <-> site1 <-> site2 <-> goal
            AddEdge(startNode, site1);
            AddEdge(site1, site2);
            AddEdge(site2, goalNode);

            // Connect: start <-> secret <-> goal (hidden shortcut)
            AddEdge(startNode, secret);
            AddEdge(secret, goalNode);
        }

        private void CreateForeshadowingLoop()
        {
            // Insertion point #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Insertion point #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> site1 <-> site2 <-> goal
            AddEdge(startNode, site1);
            AddEdge(site1, site2);
            AddEdge(site2, goalNode);

            // Connect: start <-> goal (blocked w/ sightline)
            AddEdge(startNode, goalNode, true, true, true);
        }

        private void CreateSimpleLockAndKey()
        {
            // Rewrite site #1 (path to goal)
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Key room - grants key 1
            var keyRoom = new CycleNode(type);
            keyRoom.label = "Key Room";
            keyRoom.AddGrantedKey(1);
            nodes.Add(keyRoom);
            rewriteSites.Add(new RewriteSite(keyRoom));

            // Connect: start <-> site1 <-> goal <-> keyRoom
            AddEdge(startNode, site1);

            // Goal is locked - requires key 1
            var edgeToGoal = AddEdge(site1, goalNode);
            edgeToGoal.AddRequiredKey(1);

            // Key room is past the goal
            AddEdge(goalNode, keyRoom);
        }

        private void CreateDangerousRoute()
        {
            // Safe long path #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Safe long path #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Dangerous short path (bottom)
            var danger = new CycleNode(type);
            danger.AddRole(NodeRoleType.Danger);
            nodes.Add(danger);

            // Connect: start <-> site1 <-> site2 <-> goal
            AddEdge(startNode, site1);
            AddEdge(site1, site2);
            AddEdge(site2, goalNode);

            // Connect: start <-> danger <-> goal
            AddEdge(startNode, danger);
            AddEdge(danger, goalNode);
        }

        private void CreateLockAndKeyCycle()
        {
            // Rewrite site #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Rewrite site #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Key room - grants key 1
            var keyRoom = new CycleNode(type);
            keyRoom.label = "Key Room";
            keyRoom.AddGrantedKey(1);
            nodes.Add(keyRoom);
            rewriteSites.Add(new RewriteSite(keyRoom));

            // Connect: start <-> goal (locked)
            var edgeToGoal = AddEdge(startNode, goalNode);
            edgeToGoal.AddRequiredKey(1);

            // Connect: goal <-> site1 <-> site2 <-> keyRoom -> start
            AddEdge(goalNode, site1);
            AddEdge(site1, site2);
            AddEdge(site2, keyRoom);
            AddEdge(keyRoom, startNode, false); // One-way back to start
        }

        private void CreateBlockedRetreat()
        {
            // Barrier just past goal
            var barrier = new CycleNode(type);
            barrier.AddRole(NodeRoleType.Barrier);
            nodes.Add(barrier);

            // Rewrite site #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            // Rewrite site #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> goal <-> barrier -> site1 <-> site2 <-> start
            AddEdge(startNode, goalNode);
            AddEdge(goalNode, barrier);
            AddEdge(barrier, site1, false); // One-way past barrier
            AddEdge(site1, site2);
            AddEdge(site2, startNode);
        }

        private void CreateMonsterPatrol()
        {
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            site1.AddRole(NodeRoleType.Patrol);

            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> site1 <-> goal <-> site2 <-> start
            AddEdge(startNode, site1);
            AddEdge(site1, goalNode, false);
            AddEdge(goalNode, site2);
            AddEdge(site2, startNode);
        }

        private void CreateAlteredReturn()
        {
            // Rewrite site #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            site1.AddRole(NodeRoleType.Patrol);

            // Rewrite site #2
            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> site1 -> goal <-> site2 -> start
            AddEdge(startNode, site1);
            AddEdge(site1, goalNode, false); // One-way to goal
            AddEdge(goalNode, site2);
            AddEdge(site2, startNode, false); // One-way back to start
        }

        private void CreateFalseGoal()
        {
            // Short path to false goal
            var falseGoal = new CycleNode(type);
            falseGoal.AddRole(NodeRoleType.FalseGoal);
            nodes.Add(falseGoal);

            // Hidden long path to true goal
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            var site2 = new CycleNode(type);
            nodes.Add(site2);
            rewriteSites.Add(new RewriteSite(site2));

            // Connect: start <-> falseGoal <-> site1 <-> site2 <-> goal
            AddEdge(startNode, falseGoal);
            AddEdge(falseGoal, site1);
            AddEdge(site1, site2);
            AddEdge(site2, goalNode);
        }

        private void CreateGambit()
        {
            // Rewrite site #1
            var site1 = new CycleNode(type);
            nodes.Add(site1);
            rewriteSites.Add(new RewriteSite(site1));

            var danger = new CycleNode(type);
            danger.AddRole(NodeRoleType.Danger);
            nodes.Add(danger);

            var reward = new CycleNode(type);
            reward.AddRole(NodeRoleType.Reward);
            nodes.Add(reward);

            // Connect: start <-> site1 <-> goal <-> danger <-> reward
            AddEdge(startNode, site1);
            AddEdge(site1, goalNode);
            AddEdge(goalNode, danger);
            AddEdge(danger, reward);
        }

        // Helper to find an edge between two nodes
        private CycleEdge FindEdge(CycleNode from, CycleNode to)
        {
            foreach (var edge in edges)
            {
                if (edge.from == from && edge.to == to)
                    return edge;
            }
            return null;
        }
    }
}