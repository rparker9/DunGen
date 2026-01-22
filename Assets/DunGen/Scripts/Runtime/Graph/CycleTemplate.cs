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

        /// <summary>
        /// Create a clean copy of cycle without replacement patterns
        /// (to avoid serialization depth issues)
        /// </summary>
        private static DungeonCycle CleanCycleForSerialization(DungeonCycle source)
        {
            if (source == null)
                return null;

            var clean = new DungeonCycle();

            // Clear default nodes/edges from constructor
            clean.nodes.Clear();
            clean.edges.Clear();

            // Create mapping from old nodes to new nodes
            var nodeMap = new Dictionary<CycleNode, CycleNode>();

            // Deep copy all nodes
            foreach (var oldNode in source.nodes)
            {
                if (oldNode != null)
                {
                    var newNode = DeepCopyNode(oldNode);
                    clean.nodes.Add(newNode);
                    nodeMap[oldNode] = newNode;
                }
            }

            // Deep copy all edges (with remapped node references)
            foreach (var oldEdge in source.edges)
            {
                if (oldEdge != null && nodeMap.ContainsKey(oldEdge.from) && nodeMap.ContainsKey(oldEdge.to))
                {
                    var newEdge = new CycleEdge(
                        nodeMap[oldEdge.from],
                        nodeMap[oldEdge.to],
                        oldEdge.bidirectional,
                        oldEdge.isBlocked,
                        oldEdge.hasSightline
                    );

                    // Copy locks
                    if (oldEdge.requiredKeys != null)
                    {
                        foreach (var keyId in oldEdge.requiredKeys)
                            newEdge.AddRequiredKey(keyId);
                    }

                    clean.edges.Add(newEdge);
                }
            }

            // Copy rewrite sites but WITHOUT replacement patterns
            clean.rewriteSites.Clear();
            if (source.rewriteSites != null)
            {
                foreach (var site in source.rewriteSites)
                {
                    if (site != null && site.placeholder != null && nodeMap.ContainsKey(site.placeholder))
                    {
                        // Create NEW rewrite site with remapped node
                        var newPlaceholder = nodeMap[site.placeholder];
                        var cleanSite = new RewriteSite(newPlaceholder);

                        // CRITICAL: Explicitly ensure no replacement pattern
                        // (Unity might serialize old references otherwise)
                        cleanSite.replacementPattern = null;

                        clean.rewriteSites.Add(cleanSite);
                    }
                }
            }

            // Remap start and goal references
            if (source.startNode != null && nodeMap.ContainsKey(source.startNode))
                clean.startNode = nodeMap[source.startNode];

            if (source.goalNode != null && nodeMap.ContainsKey(source.goalNode))
                clean.goalNode = nodeMap[source.goalNode];

            // VERIFICATION: Check that no patterns leaked through
            foreach (var site in clean.rewriteSites)
            {
                if (site != null && site.replacementPattern != null)
                {
                    UnityEngine.Debug.LogError($"[CycleTemplate] BUG: RewriteSite '{site.placeholder?.label}' still has replacementPattern after cleaning!");
                }
            }

            return clean;
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