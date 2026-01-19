using System.Collections.Generic;
using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;

namespace DunGen.Graph.Generation
{
    /// <summary>
    /// Records that a specific seam edge was replaced by an inserted fragment,
    /// including where it was attached (the parent edge endpoints) so the editor can draw nesting.
    /// </summary>
    public sealed class InsertionReplacement
    {
        public InsertionPointInstance Insertion { get; }
        public NodeId ParentFrom { get; }
        public NodeId ParentTo { get; }
        public SubgraphFragment Inserted { get; }

        public IReadOnlyList<NodeId> InsertedNodeIds { get; }

        public InsertionReplacement(
            InsertionPointInstance insertion,
            NodeId parentFrom,
            NodeId parentTo,
            SubgraphFragment inserted)
        {
            Insertion = insertion;
            ParentFrom = parentFrom;
            ParentTo = parentTo;
            Inserted = inserted;

            // Cache node ids for layout convenience.
            var ids = new List<NodeId>(inserted.NewNodes.Count);
            foreach (var n in inserted.NewNodes) ids.Add(n.Id);
            InsertedNodeIds = ids;
        }
    }
}
