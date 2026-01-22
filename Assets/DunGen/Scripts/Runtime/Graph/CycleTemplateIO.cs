using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Pure JSON-based serialization for cycle templates.
    /// No ScriptableObjects, no Unity serialization, just clean JSON files.
    /// 
    /// File format: .dungen.json
    /// Location: Assets/Resources/Data/CycleTemplates/*.dungen.json
    /// </summary>
    public static class CycleTemplateIO
    {
        private const int CURRENT_VERSION = 1;
        private const string FILE_EXTENSION = ".dungen.json";

        #region Serializable Format

        [Serializable]
        public class TemplateFile
        {
            public int version = CURRENT_VERSION;
            public TemplateMetadata metadata;
            public SerializedCycle cycle;
        }

        [Serializable]
        public class TemplateMetadata
        {
            public string name;
            public string description;
            public string guid; // Stable identifier
            public long createdTimestamp;
            public long modifiedTimestamp;
        }

        [Serializable]
        public class SerializedCycle
        {
            public List<SerializedNode> nodes;
            public List<SerializedEdge> edges;
            public List<SerializedRewriteSite> rewriteSites;
            public List<SerializedPosition> positions;

            // Indices into nodes list
            public int startNodeIndex = -1;
            public int goalNodeIndex = -1;
        }

        [Serializable]
        public class SerializedNode
        {
            public string guid;
            public string label;
            public List<string> roles; // NodeRoleType as strings
            public List<int> grantedKeys;
        }

        [Serializable]
        public class SerializedEdge
        {
            public string fromGuid;
            public string toGuid;
            public bool bidirectional;
            public bool isBlocked;
            public bool hasSightline;
            public List<int> requiredKeys;
        }

        [Serializable]
        public class SerializedRewriteSite
        {
            public string placeholderGuid;
            public string replacementTemplateGuid; // GUID of another template file
        }

        [Serializable]
        public class SerializedPosition
        {
            public string nodeGuid;
            public float x;
            public float y;
        }

        #endregion

        #region Save

        /// <summary>
        /// Save a cycle template to a JSON file.
        /// </summary>
        public static bool Save(
            string filePath,
            DungeonCycle cycle,
            Dictionary<CycleNode, Vector2> positions,
            string templateName,
            string description = "")
        {
            if (cycle == null)
            {
                Debug.LogError("[CycleTemplateIO] Cannot save null cycle");
                return false;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[CycleTemplateIO] File path is null or empty");
                return false;
            }

            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Build file structure
                var file = new TemplateFile
                {
                    metadata = new TemplateMetadata
                    {
                        name = templateName,
                        description = description,
                        guid = GenerateFileGuid(filePath),
                        createdTimestamp = GetUnixTimestamp(),
                        modifiedTimestamp = GetUnixTimestamp()
                    },
                    cycle = SerializeCycle(cycle, positions)
                };

                // Validate before saving
                if (!ValidateFile(file, out string error))
                {
                    Debug.LogError($"[CycleTemplateIO] Validation failed: {error}");
                    return false;
                }

                // Convert to JSON
                string json = JsonUtility.ToJson(file, prettyPrint: true);

                // Write to file
                File.WriteAllText(filePath, json);

                Debug.Log($"[CycleTemplateIO] Saved template to: {filePath}");
                Debug.Log($"  Nodes: {file.cycle.nodes.Count}");
                Debug.Log($"  Edges: {file.cycle.edges.Count}");
                Debug.Log($"  Rewrite Sites: {file.cycle.rewriteSites.Count}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CycleTemplateIO] Save failed: {ex.Message}");
                return false;
            }
        }

        private static SerializedCycle SerializeCycle(DungeonCycle cycle, Dictionary<CycleNode, Vector2> positions)
        {
            var serialized = new SerializedCycle
            {
                nodes = new List<SerializedNode>(),
                edges = new List<SerializedEdge>(),
                rewriteSites = new List<SerializedRewriteSite>(),
                positions = new List<SerializedPosition>()
            };

            // Node GUID mapping
            var nodeToGuid = new Dictionary<CycleNode, string>();
            var guidToIndex = new Dictionary<string, int>();

            // Serialize nodes
            if (cycle.nodes != null)
            {
                for (int i = 0; i < cycle.nodes.Count; i++)
                {
                    var node = cycle.nodes[i];
                    if (node == null) continue;

                    string guid = GenerateNodeGuid(node, i);
                    nodeToGuid[node] = guid;
                    guidToIndex[guid] = i;

                    var sNode = new SerializedNode
                    {
                        guid = guid,
                        label = node.label ?? "",
                        roles = new List<string>(),
                        grantedKeys = new List<int>(node.grantedKeys ?? new List<int>())
                    };

                    if (node.roles != null)
                    {
                        foreach (var role in node.roles)
                        {
                            if (role != null)
                                sNode.roles.Add(role.type.ToString());
                        }
                    }

                    serialized.nodes.Add(sNode);

                    // Track special nodes
                    if (node == cycle.startNode)
                        serialized.startNodeIndex = i;
                    if (node == cycle.goalNode)
                        serialized.goalNodeIndex = i;
                }
            }

            // Serialize edges
            if (cycle.edges != null)
            {
                foreach (var edge in cycle.edges)
                {
                    if (edge == null || edge.from == null || edge.to == null) continue;
                    if (!nodeToGuid.ContainsKey(edge.from) || !nodeToGuid.ContainsKey(edge.to))
                        continue;

                    var sEdge = new SerializedEdge
                    {
                        fromGuid = nodeToGuid[edge.from],
                        toGuid = nodeToGuid[edge.to],
                        bidirectional = edge.bidirectional,
                        isBlocked = edge.isBlocked,
                        hasSightline = edge.hasSightline,
                        requiredKeys = new List<int>(edge.requiredKeys ?? new List<int>())
                    };

                    serialized.edges.Add(sEdge);
                }
            }

            // Serialize rewrite sites
            if (cycle.rewriteSites != null)
            {
                foreach (var site in cycle.rewriteSites)
                {
                    if (site == null || site.placeholder == null) continue;
                    if (!nodeToGuid.ContainsKey(site.placeholder))
                        continue;

                    var sSite = new SerializedRewriteSite
                    {
                        placeholderGuid = nodeToGuid[site.placeholder],
                        replacementTemplateGuid = site.replacementTemplate != null
                            ? site.replacementTemplate.guid
                            : null
                    };

                    serialized.rewriteSites.Add(sSite);
                }
            }

            // Serialize positions
            if (positions != null)
            {
                foreach (var kvp in positions)
                {
                    if (kvp.Key == null || !nodeToGuid.ContainsKey(kvp.Key))
                        continue;

                    var sPos = new SerializedPosition
                    {
                        nodeGuid = nodeToGuid[kvp.Key],
                        x = kvp.Value.x,
                        y = kvp.Value.y
                    };

                    serialized.positions.Add(sPos);
                }
            }

            return serialized;
        }

        #endregion

        #region Load

        /// <summary>
        /// Load a cycle template from a JSON file.
        /// </summary>
        public static (DungeonCycle cycle, Dictionary<CycleNode, Vector2> positions, TemplateMetadata metadata) Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[CycleTemplateIO] File not found: {filePath}");
                return (null, null, null);
            }

            try
            {
                // Read JSON
                string json = File.ReadAllText(filePath);

                // Parse
                var file = JsonUtility.FromJson<TemplateFile>(json);

                if (file == null)
                {
                    Debug.LogError($"[CycleTemplateIO] Failed to parse JSON from: {filePath}");
                    return (null, null, null);
                }

                // Validate
                if (!ValidateFile(file, out string error))
                {
                    Debug.LogError($"[CycleTemplateIO] Validation failed: {error}");
                    return (null, null, null);
                }

                // Version migration
                if (file.version < CURRENT_VERSION)
                {
                    MigrateFile(file, file.version);
                }

                // Deserialize
                var (cycle, positions) = DeserializeCycle(file.cycle);

                if (cycle == null)
                {
                    Debug.LogError($"[CycleTemplateIO] Deserialization failed");
                    return (null, null, null);
                }

                Debug.Log($"[CycleTemplateIO] Loaded template from: {filePath}");
                Debug.Log($"  Nodes: {cycle.nodes.Count}");
                Debug.Log($"  Edges: {cycle.edges.Count}");
                Debug.Log($"  Rewrite Sites: {cycle.rewriteSites.Count}");

                return (cycle, positions, file.metadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CycleTemplateIO] Load failed: {ex.Message}\n{ex.StackTrace}");
                return (null, null, null);
            }
        }

        private static (DungeonCycle, Dictionary<CycleNode, Vector2>) DeserializeCycle(SerializedCycle sCycle)
        {
            var cycle = new DungeonCycle();
            var positions = new Dictionary<CycleNode, Vector2>();
            var guidToNode = new Dictionary<string, CycleNode>();

            // Deserialize nodes
            if (sCycle.nodes != null)
            {
                foreach (var sNode in sCycle.nodes)
                {
                    if (sNode == null) continue;

                    var node = new CycleNode
                    {
                        label = sNode.label ?? "",
                        roles = new List<NodeRole>(),
                        grantedKeys = new List<int>(sNode.grantedKeys ?? new List<int>())
                    };

                    // Deserialize roles
                    if (sNode.roles != null)
                    {
                        foreach (var roleStr in sNode.roles)
                        {
                            if (Enum.TryParse<NodeRoleType>(roleStr, out var roleType))
                            {
                                node.AddRole(roleType);
                            }
                        }
                    }

                    cycle.nodes.Add(node);
                    guidToNode[sNode.guid] = node;
                }
            }

            // Restore special nodes
            if (sCycle.startNodeIndex >= 0 && sCycle.startNodeIndex < cycle.nodes.Count)
                cycle.startNode = cycle.nodes[sCycle.startNodeIndex];

            if (sCycle.goalNodeIndex >= 0 && sCycle.goalNodeIndex < cycle.nodes.Count)
                cycle.goalNode = cycle.nodes[sCycle.goalNodeIndex];

            // Deserialize edges
            if (sCycle.edges != null)
            {
                foreach (var sEdge in sCycle.edges)
                {
                    if (sEdge == null) continue;

                    if (!guidToNode.TryGetValue(sEdge.fromGuid, out var fromNode))
                        continue;
                    if (!guidToNode.TryGetValue(sEdge.toGuid, out var toNode))
                        continue;

                    var edge = new CycleEdge(fromNode, toNode, sEdge.bidirectional)
                    {
                        isBlocked = sEdge.isBlocked,
                        hasSightline = sEdge.hasSightline,
                        requiredKeys = new List<int>(sEdge.requiredKeys ?? new List<int>())
                    };

                    cycle.edges.Add(edge);
                }
            }

            // Deserialize rewrite sites
            if (sCycle.rewriteSites != null)
            {
                foreach (var sSite in sCycle.rewriteSites)
                {
                    if (sSite == null) continue;

                    if (!guidToNode.TryGetValue(sSite.placeholderGuid, out var placeholder))
                        continue;

                    var site = new RewriteSite(placeholder);

                    // Restore replacement template reference
                    if (!string.IsNullOrEmpty(sSite.replacementTemplateGuid))
                    {
                        site.replacementTemplate = TemplateRegistry.GetByGuid(sSite.replacementTemplateGuid);
                    }

                    // Ensure RewriteSite role
                    if (!placeholder.HasRole(NodeRoleType.RewriteSite))
                    {
                        placeholder.AddRole(NodeRoleType.RewriteSite);
                    }

                    cycle.rewriteSites.Add(site);
                }
            }

            // Deserialize positions
            if (sCycle.positions != null)
            {
                foreach (var sPos in sCycle.positions)
                {
                    if (sPos == null) continue;

                    if (guidToNode.TryGetValue(sPos.nodeGuid, out var node))
                    {
                        positions[node] = new Vector2(sPos.x, sPos.y);
                    }
                }
            }

            return (cycle, positions);
        }

        #endregion

        #region Validation

        private static bool ValidateFile(TemplateFile file, out string error)
        {
            if (file == null)
            {
                error = "File is null";
                return false;
            }

            if (file.metadata == null)
            {
                error = "Metadata is null";
                return false;
            }

            if (file.cycle == null)
            {
                error = "Cycle is null";
                return false;
            }

            if (file.cycle.nodes == null || file.cycle.nodes.Count == 0)
            {
                error = "No nodes";
                return false;
            }

            if (file.cycle.startNodeIndex < 0 || file.cycle.startNodeIndex >= file.cycle.nodes.Count)
            {
                error = "Invalid start node index";
                return false;
            }

            if (file.cycle.goalNodeIndex < 0 || file.cycle.goalNodeIndex >= file.cycle.nodes.Count)
            {
                error = "Invalid goal node index";
                return false;
            }

            // Validate GUIDs are unique
            var guids = new HashSet<string>();
            foreach (var node in file.cycle.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.guid))
                {
                    error = "Node has null or empty GUID";
                    return false;
                }

                if (!guids.Add(node.guid))
                {
                    error = $"Duplicate node GUID: {node.guid}";
                    return false;
                }
            }

            error = "";
            return true;
        }

        #endregion

        #region Utilities

        private static string GenerateNodeGuid(CycleNode node, int index)
        {
            return $"{node.label}_{index}";
        }

        private static string GenerateFileGuid(string filePath)
        {
            // Use filename without extension as base GUID
            string filename = Path.GetFileNameWithoutExtension(filePath);
            return filename;
        }

        private static long GetUnixTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static void MigrateFile(TemplateFile file, int fromVersion)
        {
            // Add migration logic here when version increases
            file.version = CURRENT_VERSION;
        }

        #endregion
    }
}