using System;
using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// A rewrite site: a placeholder node that can be rewritten by inserting a subcycle.
    ///
    /// Serialization rules:
    /// - replacementTemplate IS serialized (asset reference, safe).
    /// - replacementPattern is NOT serialized (runtime-only), prevents Unity depth-limit cycles.
    /// </summary>
    [Serializable]
    public sealed class RewriteSite
    {
        public CycleNode placeholder;

        [Tooltip("Asset template to use when rewriting this site (serialized, safe).")]
        public CycleTemplate replacementTemplate;

        [NonSerialized]
        public DungeonCycle replacementPattern;

        public RewriteSite(CycleNode placeholder)
        {
            this.placeholder = placeholder;
        }

        public bool HasReplacementTemplate() => replacementTemplate != null && replacementTemplate.cycle != null;

        public bool HasReplacementPattern() => replacementPattern != null;
    }
}
