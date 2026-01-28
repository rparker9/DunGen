using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Represents a node on a graph
    /// </summary>
    [System.Serializable]
    public class GraphNode
    {
        // Presentation only (editor)
        public string label = "";

        // Semantic roles (solver + rewrite)
        public List<NodeRole> roles = new List<NodeRole>();

        // KEYS: Keys granted when this node is visited/completed
        public List<KeyIdentity> grantedKeys = new List<KeyIdentity>();

        public GraphNode()
        {
            // Empty constructor for manual authoring
        }

        // -------------- Role Helpers ---------------
        public bool HasRole(NodeRoleType t)
        {
            for (int i = 0; i < roles.Count; i++)
                if (roles[i] != null && roles[i].type == t)
                    return true;
            return false;
        }

        public void AddRole(NodeRole role)
        {
            if (role == null) return;
            roles.Add(role);
        }

        public void AddRole(NodeRoleType t)
        {
            roles.Add(new NodeRole(t));
        }

        public void RemoveRole(NodeRoleType t)
        {
            for (int i = roles.Count - 1; i >= 0; i--)
                if (roles[i] != null && roles[i].type == t)
                    roles.RemoveAt(i);
        }

        // --------------- Key Helpers ---------------
        public bool GrantsAnyKey()
        {
            return grantedKeys != null && grantedKeys.Count > 0;
        }

        public bool GrantsKey(string keyId)
        {
            return grantedKeys != null && grantedKeys.Any(k => k != null && k.globalId == keyId);
        }

        public void AddGrantedKey(KeyIdentity key)
        {
            if (grantedKeys == null)
                grantedKeys = new List<KeyIdentity>();

            if (key != null && !grantedKeys.Any(k => k != null && k.globalId == key.globalId))
                grantedKeys.Add(key);
        }

        public void RemoveGrantedKey(string keyId)
        {
            if (grantedKeys != null)
                grantedKeys.RemoveAll(k => k != null && k.globalId == keyId);
        }

        public void ClearGrantedKeys()
        {
            if (grantedKeys != null)
                grantedKeys.Clear();
        }

        // --------------- Backward Compatibility ---------------

        /// <summary>
        /// Add key from legacy int ID (for migration from old templates)
        /// </summary>
        public void AddGrantedKey(int legacyKeyId)
        {
            var key = new KeyIdentity
            {
                globalId = $"legacy_key_{legacyKeyId}",
                displayName = $"Key {legacyKeyId}",
                type = KeyType.Hard
            };
            AddGrantedKey(key);
        }
    }
}