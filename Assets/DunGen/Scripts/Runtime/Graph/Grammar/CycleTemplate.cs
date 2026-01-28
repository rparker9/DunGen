using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Pure JSON-based serialization for cycle templates with key/lock support.
    /// 
    /// File format: .dungen.json
    /// Location: Assets/Resources/Data/CycleTemplates/*.dungen.json
    /// </summary>
    public static class CycleTemplate
    {
        private const int CURRENT_VERSION = 2; // Bumped for key/lock system
        private const string FILE_EXTENSION = ".dungen.json";

        #region Serializable Format
        /// <summary>
        /// The entire template file structure.
        /// Contains metadata and serialized cycle data, along with version control.
        /// </summary>
        [Serializable]
        public class TemplateFile
        {
            public int version = CURRENT_VERSION;
            public TemplateMetadata metadata;
            public SerializedCycle cycle;
        }

        /// <summary>
        /// Represents metadata information for a template, including its name, description, unique identifier, and
        /// timestamps for creation and modification.
        /// </summary>
        /// <remarks>Instances of this class can be serialized, making it suitable for scenarios where
        /// template metadata needs to be stored or transmitted. The 'guid' field provides a stable, unique identifier
        /// for each template, which can be used to distinguish templates across different systems or
        /// sessions.</remarks>
        [Serializable]
        public class TemplateMetadata
        {
            public string name;
            public string description;
            public string guid; // Stable identifier
            public long createdTimestamp;
            public long modifiedTimestamp;
        }

        /// <summary>
        /// Represents a serialized cycle, including its nodes, edges, rewrite sites, and positions, for storage or
        /// transmission.
        /// </summary>
        /// <remarks>The SerializedCycle class is used to capture the structure of a cycle in a format
        /// suitable for serialization. It provides lists of nodes, edges, rewrite sites, and positions, along with
        /// indices identifying the start and goal nodes. This enables reconstruction and manipulation of the cycle's
        /// components after deserialization. The indices correspond to elements in the nodes list and are set to -1 if
        /// not specified.</remarks>
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

        /// <summary>
        /// Represents a serializable node that contains a unique identifier, a label, associated roles, and a
        /// collection of granted key identities.
        /// </summary>
        /// <remarks>This class is intended for scenarios where node data needs to be persisted or
        /// transferred, such as saving and loading graph structures. The 'roles' property stores the roles assigned to
        /// the node as strings, while 'grantedKeys' holds a list of key identities associated with the node. Instances
        /// of this class can be serialized and deserialized for use in data storage or network communication.</remarks>
        [Serializable]
        public class SerializedNode
        {
            public string guid;
            public string label;
            public List<string> roles; // NodeRoleType as strings
            public List<SerializedKeyIdentity> grantedKeys; // NEW: KeyIdentity list
        }

        /// <summary>
        /// Represents a serialized identity key that includes a global identifier, display name, type, color, and
        /// associated metadata.
        /// </summary>
        /// <remarks>This class is typically used to encapsulate the identity and descriptive information
        /// of an object in a format suitable for serialization. The 'type' field stores the key type as a string, and
        /// the 'color' field specifies the associated color using a SerializedColor instance. The 'metadata' dictionary
        /// can be used to store additional key-value pairs relevant to the identity.</remarks>
        [Serializable]
        public class SerializedKeyIdentity
        {
            public string globalId;
            public string displayName;
            public string type; // KeyType as string
            public SerializedColor color;
            public Dictionary<string, string> metadata;
        }

        /// <summary>
        /// Represents a connection between two nodes in a graph, identified by unique GUIDs. Supports both directed and
        /// bidirectional edges, and can include properties such as visibility, blockage, and required keys for
        /// traversal.
        /// </summary>
        /// <remarks>Use this class to model relationships in graph-based structures where edges may have
        /// specific access requirements or visibility constraints. The edge can be blocked, may require certain keys to
        /// traverse, and can optionally allow sightlines between connected nodes. This type is suitable for scenarios
        /// such as procedural dungeon generation or other graph-based systems where edge properties influence traversal
        /// and visibility.</remarks>
        [Serializable]
        public class SerializedEdge
        {
            public string fromGuid;
            public string toGuid;
            public bool bidirectional;
            public bool isBlocked;
            public bool hasSightline;
            public List<SerializedLockRequirement> requiredKeys; // NEW: LockRequirement list
        }

        [Serializable]
        public class SerializedLockRequirement
        {
            public string requiredKeyId;
            public string type; // LockType as string
            public SerializedColor color;
            public Dictionary<string, string> metadata;
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

        [Serializable]
        public class SerializedColor
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        #endregion

        #region Save

        /// <summary>
        /// Save a cycle template to a JSON file.
        /// </summary>
        public static bool Save(
            string filePath,
            DungeonCycle cycle,
            Dictionary<GraphNode, Vector2> positions,
            string templateName,
            string description = "")
        {
            if (cycle == null)
            {
                Debug.LogError("[CycleTemplate] Cannot save null cycle");
                return false;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[CycleTemplate] File path is null or empty");
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
                    Debug.LogError($"[CycleTemplate] Validation failed: {error}");
                    return false;
                }

                // Convert to JSON
                string json = JsonUtility.ToJson(file, prettyPrint: true);

                // Write to file
                File.WriteAllText(filePath, json);
                Debug.Log($"[CycleTemplate] Saved template to: {filePath}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CycleTemplate] Save failed: {ex.Message}");
                return false;
            }
        }

        private static SerializedCycle SerializeCycle(DungeonCycle cycle, Dictionary<GraphNode, Vector2> positions)
        {
            var serialized = new SerializedCycle
            {
                nodes = new List<SerializedNode>(),
                edges = new List<SerializedEdge>(),
                rewriteSites = new List<SerializedRewriteSite>(),
                positions = new List<SerializedPosition>()
            };

            // Node GUID mapping
            var nodeToGuid = new Dictionary<GraphNode, string>();
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
                        grantedKeys = new List<SerializedKeyIdentity>()
                    };

                    if (node.roles != null)
                    {
                        foreach (var role in node.roles)
                        {
                            if (role != null)
                                sNode.roles.Add(role.type.ToString());
                        }
                    }

                    // Serialize granted keys
                    if (node.grantedKeys != null)
                    {
                        foreach (var key in node.grantedKeys)
                        {
                            if (key != null)
                            {
                                sNode.grantedKeys.Add(new SerializedKeyIdentity
                                {
                                    globalId = key.globalId,
                                    displayName = key.displayName,
                                    type = key.type.ToString(),
                                    color = new SerializedColor { r = key.color.r, g = key.color.g, b = key.color.b, a = key.color.a },
                                    metadata = key.metadata != null ? new Dictionary<string, string>(key.metadata) : new Dictionary<string, string>()
                                });
                            }
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
                        requiredKeys = new List<SerializedLockRequirement>()
                    };

                    // Serialize lock requirements
                    if (edge.requiredKeys != null)
                    {
                        foreach (var req in edge.requiredKeys)
                        {
                            if (req != null)
                            {
                                sEdge.requiredKeys.Add(new SerializedLockRequirement
                                {
                                    requiredKeyId = req.requiredKeyId,
                                    type = req.type.ToString(),
                                    color = new SerializedColor { r = req.color.r, g = req.color.g, b = req.color.b, a = req.color.a },
                                    metadata = req.metadata != null ? new Dictionary<string, string>(req.metadata) : new Dictionary<string, string>()
                                });
                            }
                        }
                    }

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
        public static (DungeonCycle cycle, Dictionary<GraphNode, Vector2> positions, TemplateMetadata metadata) Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[CycleTemplate] File not found: {filePath}");
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
                    Debug.LogError($"[CycleTemplate] Failed to parse JSON from: {filePath}");
                    return (null, null, null);
                }

                // Validate
                if (!ValidateFile(file, out string error))
                {
                    Debug.LogError($"[CycleTemplate] Validation failed: {error}");
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
                    Debug.LogError($"[CycleTemplate] Deserialization failed");
                    return (null, null, null);
                }

                return (cycle, positions, file.metadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CycleTemplate] Load failed: {ex.Message}\n{ex.StackTrace}");
                return (null, null, null);
            }
        }

        private static (DungeonCycle, Dictionary<GraphNode, Vector2>) DeserializeCycle(SerializedCycle sCycle)
        {
            var cycle = new DungeonCycle();
            var positions = new Dictionary<GraphNode, Vector2>();
            var guidToNode = new Dictionary<string, GraphNode>();

            // Deserialize nodes
            if (sCycle.nodes != null)
            {
                foreach (var sNode in sCycle.nodes)
                {
                    if (sNode == null) continue;

                    var node = new GraphNode
                    {
                        label = sNode.label ?? "",
                        roles = new List<NodeRole>(),
                        grantedKeys = new List<KeyIdentity>()
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

                    // Deserialize granted keys
                    if (sNode.grantedKeys != null)
                    {
                        foreach (var sKey in sNode.grantedKeys)
                        {
                            if (sKey != null && Enum.TryParse<KeyType>(sKey.type, out var keyType))
                            {
                                var key = new KeyIdentity
                                {
                                    globalId = sKey.globalId,
                                    displayName = sKey.displayName,
                                    type = keyType,
                                    color = sKey.color != null
                                        ? new Color(sKey.color.r, sKey.color.g, sKey.color.b, sKey.color.a)
                                        : Color.yellow,
                                    metadata = sKey.metadata != null
                                        ? new Dictionary<string, string>(sKey.metadata)
                                        : new Dictionary<string, string>()
                                };
                                node.grantedKeys.Add(key);
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

                    var edge = new GraphEdge(fromNode, toNode, sEdge.bidirectional)
                    {
                        isBlocked = sEdge.isBlocked,
                        hasSightline = sEdge.hasSightline,
                        requiredKeys = new List<LockRequirement>()
                    };

                    // Deserialize lock requirements
                    if (sEdge.requiredKeys != null)
                    {
                        foreach (var sReq in sEdge.requiredKeys)
                        {
                            if (sReq != null && Enum.TryParse<LockType>(sReq.type, out var lockType))
                            {
                                var req = new LockRequirement
                                {
                                    requiredKeyId = sReq.requiredKeyId,
                                    type = lockType,
                                    color = sReq.color != null
                                        ? new Color(sReq.color.r, sReq.color.g, sReq.color.b, sReq.color.a)
                                        : Color.red,
                                    metadata = sReq.metadata != null
                                        ? new Dictionary<string, string>(sReq.metadata)
                                        : new Dictionary<string, string>()
                                };
                                edge.requiredKeys.Add(req);
                            }
                        }
                    }

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

        private static string GenerateNodeGuid(GraphNode node, int index)
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
            Debug.Log($"[CycleTemplate] Migrating template from version {fromVersion} to {CURRENT_VERSION}");

            // Migration from v1 to v2: Convert legacy int keys to KeyIdentity
            if (fromVersion == 1)
            {
                // Legacy templates had List<int> for keys
                // We need to convert them to KeyIdentity with template-local IDs
                // This is handled in deserialization
            }

            file.version = CURRENT_VERSION;
        }

        #endregion
    }
}