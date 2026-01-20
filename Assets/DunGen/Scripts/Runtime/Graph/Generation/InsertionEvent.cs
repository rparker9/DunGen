using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;

using System.Collections.Generic;

namespace DunGen.Graph.Generation
{
    /// <summary>
    /// Records that a specific seam edge was replaced by an inserted fragment,
    /// including where it was attached (the parent edge endpoints) so the editor can draw nesting.
    /// </summary>
    public sealed class InsertionEvent
    {
        public InsertionPoint Insertion { get; }
        public NodeId ParentFrom { get; }
        public NodeId ParentTo { get; }
        public CycleInstance Inserted { get; }
        public CycleType InsertedType { get; }

        public IReadOnlyList<NodeId> InsertedNodeIds { get; }

        public InsertionEvent(
            InsertionPoint insertion,
            NodeId parentFrom,
            NodeId parentTo,
            CycleInstance inserted,
            CycleType insertedType)
        {
            Insertion = insertion;
            ParentFrom = parentFrom;
            ParentTo = parentTo;
            Inserted = inserted;
            InsertedType = insertedType;

            // Cache node ids for layout convenience.
            var ids = new List<NodeId>(inserted.NewNodes.Count);
            foreach (var n in inserted.NewNodes) 
                ids.Add(n.Id);
            InsertedNodeIds = ids;
        }
    }
}