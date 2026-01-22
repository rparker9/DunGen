using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen
{
    [System.Serializable]
    public class CycleNode
    {
        // Presentation only (editor)
        public string label = "";

        // Semantic roles (solver + rewrite)
        public List<NodeRole> roles = new List<NodeRole>();

        // KEYS: Keys granted when this node is visited/completed
        public List<int> grantedKeys = new List<int>();

        public CycleNode()
        {
            // Empty constructor for manual authoring
        }

        // ---------- role helpers ----------
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

        // ---------- key helpers ----------
        public bool GrantsAnyKey()
        {
            return grantedKeys != null && grantedKeys.Count > 0;
        }

        public bool GrantsKey(int keyId)
        {
            return grantedKeys != null && grantedKeys.Contains(keyId);
        }

        public void AddGrantedKey(int keyId)
        {
            if (grantedKeys == null)
                grantedKeys = new List<int>();

            if (!grantedKeys.Contains(keyId))
                grantedKeys.Add(keyId);
        }

        public void RemoveGrantedKey(int keyId)
        {
            if (grantedKeys != null)
                grantedKeys.Remove(keyId);
        }

        public void ClearGrantedKeys()
        {
            if (grantedKeys != null)
                grantedKeys.Clear();
        }
    }
}