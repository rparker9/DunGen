using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    public enum NodeRoleType
    {
        Start,
        Goal,

        // Rewriting / authoring
        RewriteSite,

        // Rewards (moved from nodes, kept for backward compatibility or future use)
        Reward,               // parameter: rewardId

        // World / traversal semantics preserved through rewrites
        Barrier,              // "this room acts as barrier"
        Secret,               // "this room is secret/hidden"
        Danger,               // "this room is dangerous"
        FalseGoal,            // "this room looks like goal but isn't"
        Patrol,               // monster patrol loop room, etc.

        // Optional / future
        SightlineSource,      // foreshadowing "start sees goal"
        SightlineTarget       // foreshadowing "goal seen from start"
    }

    /// <summary>
    /// Serializable role with optional parameters.
    /// Simplified now that keys are on edges.
    /// </summary>
    [Serializable]
    public sealed class NodeRole
    {
        public NodeRoleType type;

        // For future reward system
        public int rewardId;

        public NodeRole(NodeRoleType type)
        {
            this.type = type;
        }

        public static NodeRole WithReward(int id) => new NodeRole(NodeRoleType.Reward) { rewardId = id };
    }
}