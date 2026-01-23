using System;
using System.Collections.Generic;

namespace DunGen
{
    [Serializable]
    public sealed class GraphEdge
    {
        public GraphNode from;
        public GraphNode to;
        public bool bidirectional;
        public bool isBlocked;
        public bool hasSightline;

        // LOCKS: Keys required to traverse this edge
        public List<int> requiredKeys;

        public GraphEdge(GraphNode from, GraphNode to, bool bidirectional = true, bool isBlocked = false, bool hasSightline = false)
        {
            this.from = from;
            this.to = to;
            this.bidirectional = bidirectional;
            this.isBlocked = isBlocked;
            this.hasSightline = hasSightline;
            this.requiredKeys = new List<int>();
        }

        // Convenience methods for locks
        public bool RequiresKey(int keyId)
        {
            return requiredKeys != null && requiredKeys.Contains(keyId);
        }

        public bool RequiresAnyKey()
        {
            return requiredKeys != null && requiredKeys.Count > 0;
        }

        public void AddRequiredKey(int keyId)
        {
            if (requiredKeys == null)
                requiredKeys = new List<int>();

            if (!requiredKeys.Contains(keyId))
                requiredKeys.Add(keyId);
        }

        public void RemoveRequiredKey(int keyId)
        {
            if (requiredKeys != null)
                requiredKeys.Remove(keyId);
        }

        public void ClearRequiredKeys()
        {
            if (requiredKeys != null)
                requiredKeys.Clear();
        }
    }
}