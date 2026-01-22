using System;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Lightweight handle for a template file.
    /// Replaces the heavy ScriptableObject with just file metadata.
    /// </summary>
    [Serializable]
    public class TemplateHandle
    {
        public string guid;
        public string name;
        public string description;
        public string filePath; // Relative to Assets folder

        [NonSerialized]
        private DungeonCycle _cachedCycle;

        [NonSerialized]
        private bool _cacheValid = false;

        public TemplateHandle(string guid, string name, string filePath)
        {
            this.guid = guid;
            this.name = name;
            this.filePath = filePath;
        }

        /// <summary>
        /// Load the cycle from file (uses cache if available).
        /// </summary>
        public (DungeonCycle cycle, System.Collections.Generic.Dictionary<CycleNode, Vector2> positions) Load()
        {
            if (_cacheValid && _cachedCycle != null)
            {
                // Return cached copy
                return (DeepCopy(_cachedCycle), null); // Positions not cached
            }

            var (cycle, positions, _) = CycleTemplateIO.Load(filePath);

            if (cycle != null)
            {
                _cachedCycle = cycle;
                _cacheValid = true;
            }

            return (cycle, positions);
        }

        /// <summary>
        /// Create a runtime copy for generation (doesn't cache).
        /// </summary>
        public DungeonCycle CreateRuntimeCopy()
        {
            var (cycle, _) = Load();
            return cycle != null ? DeepCopy(cycle) : null;
        }

        public bool IsValid(out string errorMessage)
        {
            var (cycle, _) = Load();
            if (cycle == null)
            {
                errorMessage = $"Failed to load cycle from template '{name}' at path '{filePath}'.";
                return false;
            }
            errorMessage = null;
            return true;
        }


        /// <summary>
        /// Invalidate cache (call after file is modified).
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
            _cachedCycle = null;
        }

        private static DungeonCycle DeepCopy(DungeonCycle source)
        {
            // Same deep copy logic as before
            if (source == null) return null;

            var copy = new DungeonCycle();
            var nodeMap = new System.Collections.Generic.Dictionary<CycleNode, CycleNode>();

            if (source.nodes != null)
            {
                foreach (var oldNode in source.nodes)
                {
                    if (oldNode != null)
                    {
                        var newNode = new CycleNode
                        {
                            label = oldNode.label,
                            grantedKeys = oldNode.grantedKeys != null
                                ? new System.Collections.Generic.List<int>(oldNode.grantedKeys)
                                : new System.Collections.Generic.List<int>()
                        };

                        if (oldNode.roles != null)
                        {
                            foreach (var role in oldNode.roles)
                            {
                                if (role != null)
                                    newNode.AddRole(role.type);
                            }
                        }

                        copy.nodes.Add(newNode);
                        nodeMap[oldNode] = newNode;
                    }
                }
            }

            if (source.edges != null)
            {
                foreach (var oldEdge in source.edges)
                {
                    if (oldEdge != null &&
                        nodeMap.ContainsKey(oldEdge.from) &&
                        nodeMap.ContainsKey(oldEdge.to))
                    {
                        var newEdge = new CycleEdge(
                            nodeMap[oldEdge.from],
                            nodeMap[oldEdge.to],
                            oldEdge.bidirectional,
                            oldEdge.isBlocked,
                            oldEdge.hasSightline
                        );

                        if (oldEdge.requiredKeys != null)
                        {
                            foreach (var keyId in oldEdge.requiredKeys)
                                newEdge.AddRequiredKey(keyId);
                        }

                        copy.edges.Add(newEdge);
                    }
                }
            }

            if (source.rewriteSites != null)
            {
                foreach (var site in source.rewriteSites)
                {
                    if (site != null &&
                        site.placeholder != null &&
                        nodeMap.ContainsKey(site.placeholder))
                    {
                        var newSite = new RewriteSite(nodeMap[site.placeholder])
                        {
                            replacementTemplate = site.replacementTemplate
                        };
                        copy.rewriteSites.Add(newSite);
                    }
                }
            }

            if (source.startNode != null && nodeMap.ContainsKey(source.startNode))
                copy.startNode = nodeMap[source.startNode];

            if (source.goalNode != null && nodeMap.ContainsKey(source.goalNode))
                copy.goalNode = nodeMap[source.goalNode];

            return copy;
        }
    }
}