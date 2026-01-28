using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// Central registry for managing unique key identities during dungeon generation.
    /// 
    /// Responsibilities:
    /// - Generate globally unique key IDs during rewriting
    /// - Track all keys in the current dungeon
    /// - Provide key lookup and validation
    /// 
    /// Usage:
    /// - Create one registry per dungeon generation
    /// - Pass to CloneCycle() to remap template-local keys to global keys
    /// - Query after generation to get all keys in the dungeon
    /// </summary>
    public class KeyRegistry
    {
        private Dictionary<string, KeyIdentity> _keys = new Dictionary<string, KeyIdentity>();
        private int _nextId = 0;

        /// <summary>
        /// Register a new unique key identity.
        /// Converts template-local key ID to globally unique ID.
        /// </summary>
        /// <param name="templateLocalId">Original key ID from template (e.g., "1", "2")</param>
        /// <param name="templateName">Name of template this key came from</param>
        /// <param name="type">Type of key</param>
        /// <param name="displayName">Optional display name override</param>
        /// <returns>New globally unique KeyIdentity</returns>
        public KeyIdentity RegisterKey(
            string templateLocalId,
            string templateName,
            KeyType type = KeyType.Hard,
            string displayName = null)
        {
            // Generate globally unique ID
            string globalId = $"{templateName}_k{templateLocalId}_{_nextId++}";

            var key = new KeyIdentity
            {
                globalId = globalId,
                displayName = displayName ?? $"Key {templateLocalId} ({templateName})",
                type = type,
                color = GetColorForKeyType(type)
            };

            _keys[globalId] = key;

            Debug.Log($"[KeyRegistry] Registered key: {key.displayName} -> {globalId}");

            return key;
        }

        /// <summary>
        /// Get a key by its global ID.
        /// </summary>
        public KeyIdentity GetKey(string globalId)
        {
            return _keys.TryGetValue(globalId, out var key) ? key : null;
        }

        /// <summary>
        /// Get all keys in the registry.
        /// </summary>
        public List<KeyIdentity> GetAllKeys()
        {
            return _keys.Values.ToList();
        }

        /// <summary>
        /// Check if a key exists in the registry.
        /// </summary>
        public bool HasKey(string globalId)
        {
            return _keys.ContainsKey(globalId);
        }

        /// <summary>
        /// Get the count of keys in the registry.
        /// </summary>
        public int KeyCount => _keys.Count;

        /// <summary>
        /// Clear all keys from the registry.
        /// </summary>
        public void Clear()
        {
            _keys.Clear();
            _nextId = 0;
        }

        /// <summary>
        /// Get default color for a key type.
        /// </summary>
        private Color GetColorForKeyType(KeyType type)
        {
            switch (type)
            {
                case KeyType.Hard:
                    return new Color(1.0f, 0.85f, 0.3f); // Yellow
                case KeyType.Soft:
                    return new Color(0.5f, 0.85f, 1.0f); // Light Blue
                case KeyType.Ability:
                    return new Color(0.5f, 1.0f, 0.5f); // Green
                case KeyType.Item:
                    return new Color(1.0f, 0.5f, 0.2f); // Orange
                case KeyType.Trigger:
                    return new Color(0.8f, 0.3f, 1.0f); // Purple
                case KeyType.Narrative:
                    return new Color(0.3f, 0.6f, 1.0f); // Blue
                default:
                    return Color.yellow;
            }
        }

        /// <summary>
        /// Debug: Print all keys in the registry.
        /// </summary>
        public void DebugPrintKeys()
        {
            Debug.Log($"[KeyRegistry] Total keys: {_keys.Count}");
            foreach (var kvp in _keys)
            {
                Debug.Log($"  - {kvp.Value.displayName} ({kvp.Value.type}): {kvp.Key}");
            }
        }
    }
}