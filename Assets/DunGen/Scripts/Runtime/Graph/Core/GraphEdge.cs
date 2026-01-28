using System;
using System.Collections.Generic;
using System.Linq;

namespace DunGen
{
    /// <summary>
    /// Represents an edge/connection between two graph nodes.
    /// </summary>
    [Serializable]
    public sealed class GraphEdge
    {
        public GraphNode from;
        public GraphNode to;
        public bool bidirectional;
        public bool isBlocked;
        public bool hasSightline;

        // LOCKS: Keys required to traverse this edge
        public List<LockRequirement> requiredKeys;

        public GraphEdge(GraphNode from, GraphNode to, bool bidirectional = true, bool isBlocked = false, bool hasSightline = false)
        {
            this.from = from;
            this.to = to;
            this.bidirectional = bidirectional;
            this.isBlocked = isBlocked;
            this.hasSightline = hasSightline;
            this.requiredKeys = new List<LockRequirement>();
        }

        // --------------- Key Helpers ---------------

        public bool RequiresKey(string keyId)
        {
            return requiredKeys != null && requiredKeys.Any(r => r != null && r.requiredKeyId == keyId);
        }

        public bool RequiresAnyKey()
        {
            return requiredKeys != null && requiredKeys.Count > 0;
        }

        public void AddRequiredKey(LockRequirement requirement)
        {
            if (requiredKeys == null)
                requiredKeys = new List<LockRequirement>();

            if (requirement != null && !requiredKeys.Any(r => r != null && r.requiredKeyId == requirement.requiredKeyId))
                requiredKeys.Add(requirement);
        }

        public void RemoveRequiredKey(string keyId)
        {
            if (requiredKeys != null)
                requiredKeys.RemoveAll(r => r != null && r.requiredKeyId == keyId);
        }

        public void ClearRequiredKeys()
        {
            if (requiredKeys != null)
                requiredKeys.Clear();
        }

        // --------------- Backward Compatibility ---------------

        /// <summary>
        /// Add lock from legacy int key ID (for migration from old templates)
        /// </summary>
        public void AddRequiredKey(int legacyKeyId)
        {
            var requirement = new LockRequirement
            {
                requiredKeyId = $"legacy_key_{legacyKeyId}",
                type = LockType.Standard
            };
            AddRequiredKey(requirement);
        }
    }
}