using UnityEngine;
using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// ScriptableObject asset for storing cycle templates.
    /// Templates can be created in Author Canvas and loaded in Preview Canvas.
    /// </summary>
    [CreateAssetMenu(fileName = "CycleTemplate", menuName = "DunGen/Cycle Template", order = 1)]
    public class CycleTemplate : ScriptableObject
    {
        [Header("Template Info")]
        public string templateName = "New Template";

        [TextArea(3, 10)]
        public string description = "";

        [Header("Template Data")]
        public DungeonCycle cycle;

        [Header("Author Mode Data")]
        // Store manual positions from author mode
        public List<NodePosition> authorPositions = new List<NodePosition>();

        // Preview image (optional)
        public Texture2D thumbnail;

        /// <summary>
        /// Helper class to serialize node positions
        /// </summary>
        [System.Serializable]
        public class NodePosition
        {
            public string nodeId; // We'll use node label as ID for now
            public Vector2 position;

            public NodePosition(string id, Vector2 pos)
            {
                nodeId = id;
                position = pos;
            }
        }

        /// <summary>
        /// Creates a clean runtime copy suitable for preview/generation.
        /// This ensures:
        /// - No replacementPattern graphs are retained (runtime-only).
        /// - No shared references that can get serialized back into the asset.
        /// </summary>
        public DungeonCycle CreateRuntimeCopy()
        {
            return CleanCycleForSerialization(cycle);
        }


        /// <summary>
        /// Create a new template from a cycle
        /// </summary>
        public static CycleTemplate CreateFromCycle(DungeonCycle cycle, Dictionary<CycleNode, Vector2> positions)
        {
            var template = CreateInstance<CycleTemplate>();

            // Create a clean copy of the cycle WITHOUT replacement patterns
            // (replacement patterns cause infinite serialization depth)
            template.cycle = CleanCycleForSerialization(cycle);
            template.templateName = "New Template";

            // Store positions
            template.authorPositions.Clear();
            if (positions != null)
            {
                foreach (var kvp in positions)
                {
                    if (kvp.Key != null)
                    {
                        template.authorPositions.Add(new NodePosition(
                            kvp.Key.label,
                            kvp.Value
                        ));
                    }
                }
            }

            return template;
        }

        // =========================================================
        // Serialization cleanup (deep copy WITHOUT runtime links)
        // =========================================================

        private static DungeonCycle CleanCycleForSerialization(DungeonCycle original)
        {
            if (original == null)
                return null;

            var nodeMap = new Dictionary<CycleNode, CycleNode>();
            var cleaned = new DungeonCycle();

            // Copy nodes
            cleaned.nodes = new List<CycleNode>();
            foreach (var n in original.nodes)
            {
                if (n == null) continue;
                var nn = new CycleNode
                {
                    label = n.label,
                    roles = new List<NodeRole>(),
                    grantedKeys = n.grantedKeys != null ? new List<int>(n.grantedKeys) : new List<int>()
                };

                if (n.roles != null)
                {
                    foreach (var r in n.roles)
                    {
                        if (r == null) continue;
                        nn.roles.Add(new NodeRole(r.type));
                    }
                }

                nodeMap[n] = nn;
                cleaned.nodes.Add(nn);
            }

            // Copy start/goal refs
            if (original.startNode != null && nodeMap.TryGetValue(original.startNode, out var s))
                cleaned.startNode = s;
            if (original.goalNode != null && nodeMap.TryGetValue(original.goalNode, out var g))
                cleaned.goalNode = g;

            // Copy edges
            cleaned.edges = new List<CycleEdge>();
            if (original.edges != null)
            {
                foreach (var e in original.edges)
                {
                    if (e == null) continue;
                    if (e.from == null || e.to == null) continue;
                    if (!nodeMap.TryGetValue(e.from, out var from)) continue;
                    if (!nodeMap.TryGetValue(e.to, out var to)) continue;

                    var ne = new CycleEdge(from, to, e.bidirectional)
                    {
                        isBlocked = e.isBlocked,
                        hasSightline = e.hasSightline,
                        requiredKeys = e.requiredKeys != null ? new List<int>(e.requiredKeys) : new List<int>()
                    };

                    cleaned.edges.Add(ne);
                }
            }

            // Copy rewrite sites (but NEVER copy replacementPattern)
            cleaned.rewriteSites = new List<RewriteSite>();
            if (original.rewriteSites != null)
            {
                foreach (var site in original.rewriteSites)
                {
                    if (site == null || site.placeholder == null) continue;
                    if (!nodeMap.TryGetValue(site.placeholder, out var ph)) continue;

                    var ns = new RewriteSite(ph);
                    // ns.replacementPattern intentionally NOT copied
                    cleaned.rewriteSites.Add(ns);
                }
            }

            return cleaned;
        }
    

        /// <summary>
        /// Verify that a cycle has no nested replacement patterns
        /// </summary>
        private static void VerifyNoNestedPatterns(DungeonCycle cycle, int depth = 0)
        {
            if (cycle == null || depth > 5) return;

            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site != null && site.replacementPattern != null)
                    {
                        UnityEngine.Debug.LogError($"[CycleTemplate] VERIFICATION FAILED at depth {depth}: RewriteSite still has replacementPattern! This will cause serialization errors.");
                        // Recurse to check nested
                        VerifyNoNestedPatterns(site.replacementPattern, depth + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Create a deep copy of a node (without nested cycles)
        /// </summary>
        private static CycleNode DeepCopyNode(CycleNode source)
        {
            var copy = new CycleNode();
            copy.label = source.label;

            // Copy keys
            if (source.grantedKeys != null)
            {
                foreach (var keyId in source.grantedKeys)
                    copy.AddGrantedKey(keyId);
            }

            // Copy roles
            if (source.roles != null)
            {
                foreach (var role in source.roles)
                {
                    if (role != null)
                        copy.AddRole(role.type);
                }
            }

            return copy;
        }

        /// <summary>
        /// Get positions dictionary from stored data
        /// </summary>
        public Dictionary<CycleNode, Vector2> GetPositionsDictionary()
        {
            var positions = new Dictionary<CycleNode, Vector2>();

            if (cycle == null || cycle.nodes == null || authorPositions == null)
                return positions;

            // Match nodes by label (simple approach)
            foreach (var nodePos in authorPositions)
            {
                foreach (var node in cycle.nodes)
                {
                    if (node != null && node.label == nodePos.nodeId)
                    {
                        positions[node] = nodePos.position;
                        break;
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// Validate template data
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (cycle == null)
            {
                errorMessage = "Cycle data is null";
                return false;
            }

            if (cycle.nodes == null || cycle.nodes.Count == 0)
            {
                errorMessage = "Template has no nodes";
                return false;
            }

            if (cycle.startNode == null)
            {
                errorMessage = "Template has no start node";
                return false;
            }

            if (cycle.goalNode == null)
            {
                errorMessage = "Template has no goal node";
                return false;
            }

            errorMessage = "";
            return true;
        }
    }
}