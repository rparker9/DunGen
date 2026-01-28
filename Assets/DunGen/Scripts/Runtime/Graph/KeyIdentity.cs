using System;
using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// Represents a unique key identity in the dungeon graph.
    /// Keys are granted by nodes and required by locks on edges.
    /// 
    /// Design notes:
    /// - globalId is guaranteed unique across all rewrites
    /// - Supports hard/soft keys, abilities, items, etc
    /// - Extensible via metadata dictionary
    /// </summary>
    [Serializable]
    public class KeyIdentity
    {
        /// <summary>Globally unique identifier (generated during rewrite)</summary>
        public string globalId;

        /// <summary>Human-readable display name for editor/debug</summary>
        public string displayName;

        /// <summary>Type of key (Hard, Soft, Ability, etc)</summary>
        public KeyType type;

        /// <summary>Optional color for visual distinction in editor</summary>
        public UnityEngine.Color color;

        /// <summary>Extensible metadata for future features (abilities, terrain types, etc)</summary>
        public Dictionary<string, string> metadata;

        public KeyIdentity()
        {
            metadata = new Dictionary<string, string>();
            color = UnityEngine.Color.yellow; // Default color
        }

        public KeyIdentity(string globalId, string displayName, KeyType type = KeyType.Hard)
        {
            this.globalId = globalId;
            this.displayName = displayName;
            this.type = type;
            this.metadata = new Dictionary<string, string>();
            this.color = UnityEngine.Color.yellow;
        }

        /// <summary>Create a deep copy of this key identity</summary>
        public KeyIdentity Clone()
        {
            var clone = new KeyIdentity
            {
                globalId = this.globalId,
                displayName = this.displayName,
                type = this.type,
                color = this.color,
                metadata = new Dictionary<string, string>(this.metadata)
            };
            return clone;
        }

        public override string ToString()
        {
            return $"{displayName} ({type})";
        }

        public override bool Equals(object obj)
        {
            return obj is KeyIdentity other && globalId == other.globalId;
        }

        public override int GetHashCode()
        {
            return globalId?.GetHashCode() ?? 0;
        }
    }

    /// <summary>
    /// Types of keys in the dungeon system.
    /// Extensible for future gameplay mechanics.
    /// </summary>
    public enum KeyType
    {
        /// <summary>Required to pass, no alternative</summary>
        Hard,

        /// <summary>Optional, may provide shortcuts or bonuses</summary>
        Soft,

        /// <summary>Grants traversal ability (e.g., double jump, swim, fly)</summary>
        Ability,

        /// <summary>Physical item requirement (e.g., torch, rope)</summary>
        Item,

        /// <summary>Event-based unlock (e.g., boss defeated, puzzle solved)</summary>
        Trigger,

        /// <summary>Story/narrative progression requirement</summary>
        Narrative
    }
}