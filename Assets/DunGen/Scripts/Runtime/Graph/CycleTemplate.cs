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

        public CycleType cycleType = CycleType.TwoAlternativePaths;

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
            template.cycle = cycle;
            template.cycleType = cycle.type;
            template.templateName = $"{cycle.type} Template";

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