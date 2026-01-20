using DunGen.Graph.Core;
using DunGen.Graph.Templates.Core;
using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    public sealed class CycleTemplateBuilder
    {
        private readonly CycleType _type;
        private int _nextTNodeId = 1;
        private int _nextTEdgeId = 1;
        private int _nextTInsertionId = 1;

        private readonly Dictionary<TNodeId, TNode> _nodes = new Dictionary<TNodeId, TNode>();
        private readonly Dictionary<TEdgeId, TEdge> _edges = new Dictionary<TEdgeId, TEdge>();
        private readonly List<TInsertion> _insertions = new List<TInsertion>();

        public CycleTemplateBuilder(CycleType type)
        {
            _type = type;
        }

        public TNodeId AddNode(TNodeKind kind, string debugLabel = null)
        {
            var id = new TNodeId(_nextTNodeId++);
            _nodes.Add(id, new TNode(id, kind, debugLabel));
            return id;
        }

        public TEdgeId AddEdge(TNodeId from, TNodeId to, EdgeTraversal traversal = EdgeTraversal.Normal)
        {
            var id = new TEdgeId(_nextTEdgeId++);
            _edges.Add(id, new TEdge(id, from, to, traversal));
            return id;
        }

        public void AddInsertion(TEdgeId seamEdge)
        {
            var id = new TInsertionId(_nextTInsertionId++);
            _insertions.Add(new TInsertion(id, seamEdge));
        }

        public CycleTemplate Build(
            TNodeId start,
            TNodeId goal,
            IReadOnlyList<TEdgeId> arcA,
            IReadOnlyList<TEdgeId> arcB)
        {
            return new CycleTemplate(
                _type,
                _nodes,
                _edges,
                _insertions,
                start,
                goal,
                arcA,
                arcB);
        }
    }
}