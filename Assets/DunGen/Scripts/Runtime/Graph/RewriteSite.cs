using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// A rewrite site: a placeholder node that can be rewritten by inserting a subgraph pattern.
    /// </summary>
    [System.Serializable]
    public sealed class RewriteSite
    {
        public CycleNode placeholder;
        public DungeonCycle replacementPattern;

        public RewriteSite(CycleNode placeholder)
        {
            this.placeholder = placeholder;
        }

        public bool HasReplacement() => replacementPattern != null;
    }
}
