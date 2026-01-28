using System;
using System.Collections.Generic;

namespace DunGen
{
    /// <summary>
    /// Represents a lock requirement on an edge.
    /// References a KeyIdentity by globalId.
    /// 
    /// Design notes:
    /// - Stores reference to required key (not the key itself)
    /// - Supports different lock types (standard, terrain, puzzle, etc)
    /// - Extensible via metadata for future mechanics
    /// </summary>
    [Serializable]
    public class LockRequirement
    {
        /// <summary>Global ID of the required key (references KeyIdentity.globalId)</summary>
        public string requiredKeyId;

        /// <summary>Type of lock (standard, terrain, puzzle, etc)</summary>
        public LockType type;

        /// <summary>Optional color for visual distinction in editor</summary>
        public UnityEngine.Color color;

        /// <summary>Extensible metadata for terrain types, ability checks, etc</summary>
        public Dictionary<string, string> metadata;

        public LockRequirement()
        {
            metadata = new Dictionary<string, string>();
            color = UnityEngine.Color.red; // Default color
        }

        public LockRequirement(string requiredKeyId, LockType type = LockType.Standard)
        {
            this.requiredKeyId = requiredKeyId;
            this.type = type;
            this.metadata = new Dictionary<string, string>();
            this.color = UnityEngine.Color.red;
        }

        /// <summary>Create a deep copy of this lock requirement</summary>
        public LockRequirement Clone()
        {
            var clone = new LockRequirement
            {
                requiredKeyId = this.requiredKeyId,
                type = this.type,
                color = this.color,
                metadata = new Dictionary<string, string>(this.metadata)
            };
            return clone;
        }

        public override string ToString()
        {
            return $"Lock ({type}): requires {requiredKeyId}";
        }

        public override bool Equals(object obj)
        {
            return obj is LockRequirement other &&
                   requiredKeyId == other.requiredKeyId &&
                   type == other.type;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (requiredKeyId?.GetHashCode() ?? 0);
                hash = hash * 31 + type.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Types of locks in the dungeon system.
    /// Determines how the lock blocks traversal.
    /// </summary>
    public enum LockType
    {
        /// <summary>Standard key-and-lock mechanism</summary>
        Standard,

        /// <summary>Terrain blocking (lava, chasm, water, etc)</summary>
        Terrain,

        /// <summary>Requires specific ability to traverse</summary>
        Ability,

        /// <summary>Puzzle or challenge requirement</summary>
        Puzzle,

        /// <summary>One-way door (can't return without key)</summary>
        OneWay,

        /// <summary>Narrative/story requirement</summary>
        Narrative,

        /// <summary>Boss door (requires boss defeat)</summary>
        Boss
    }
}