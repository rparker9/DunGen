using DunGen.Graph;
using DunGen.Graph.Core;
using DunGen.Graph.Templates;
using System;
using System.Collections.Generic;

namespace DunGen.Graph.Rewrite
{
    /// <summary>
    /// Concrete insertion seam in the *runtime graph*.
    /// The seam is an existing edge that we will replace with an inserted subgraph.
    /// </summary>
    public sealed class InsertionPointInstance
    {
        public InsertionId Id { get; }
        public EdgeId SeamEdge { get; }
        public int Depth { get; }

        public InsertionPointInstance(InsertionId id, EdgeId seamEdge, int depth)
        {
            Id = id;
            SeamEdge = seamEdge;
            Depth = depth;
        }
    }

    /// <summary>
    /// Temporary chunk of graph produced from a CycleTemplate.
    /// </summary>
    public sealed class SubgraphFragment
    {
        public NodeId Entry { get; }
        public NodeId Exit { get; }

        public List<RoomNode> NewNodes { get; } = new List<RoomNode>();
        public List<RoomEdge> NewEdges { get; } = new List<RoomEdge>();
        public List<InsertionPointInstance> NewInsertions { get; } = new List<InsertionPointInstance>();

        public SubgraphFragment(NodeId entry, NodeId exit)
        {
            Entry = entry;
            Exit = exit;
        }
    }

    public sealed class GraphRewriteEngine
    {
        private readonly IdAllocator _ids;

        public GraphRewriteEngine(IdAllocator ids)
        {
            _ids = ids;
        }

        public SubgraphFragment Instantiate(CycleTemplate template, int depth)
        {
            var mapNode = new Dictionary<TNodeId, NodeId>();
            var mapEdge = new Dictionary<TEdgeId, EdgeId>();

            // Allocate all nodes first
            foreach (var kv in template.Nodes)
            {
                var tNode = kv.Value;
                var newId = _ids.NewNode();
                mapNode.Add(tNode.Id, newId);
            }

            var entry = mapNode[template.Start];
            var exit = mapNode[template.Goal];

            var frag = new SubgraphFragment(entry, exit);

            // Materialize nodes
            foreach (var kv in template.Nodes)
            {
                var tNode = kv.Value;
                var id = mapNode[tNode.Id];

                var node = new RoomNode(id, tNode.Kind, tNode.DebugLabel);
                node.Tags.AddRange(tNode.Tags);
                frag.NewNodes.Add(node);
            }

            // Materialize edges
            foreach (var kv in template.Edges)
            {
                var tEdge = kv.Value;
                var eid = _ids.NewEdge();

                var edge = new RoomEdge(
                    eid,
                    mapNode[tEdge.From],
                    mapNode[tEdge.To],
                    tEdge.Traversal);

                mapEdge.Add(tEdge.Id, eid);
                frag.NewEdges.Add(edge);
            }

            // Create insertion seams (diamonds) as instances referencing runtime edges
            foreach (var ins in template.Insertions)
            {
                EdgeId seam;
                if (!mapEdge.TryGetValue(ins.SeamEdge, out seam))
                    throw new InvalidOperationException("Template insertion refers to missing seam edge.");

                frag.NewInsertions.Add(new InsertionPointInstance(_ids.NewInsertion(), seam, depth));
            }

            return frag;
        }

        /// <summary>
        /// Replace existing edge A->B with: A->frag.Entry ... frag.Exit->B.
        /// </summary>
        public void SpliceReplaceEdge(DungeonGraph graph, EdgeId seamEdgeId, SubgraphFragment frag)
        {
            var seam = graph.GetEdge(seamEdgeId);
            var a = seam.From;
            var b = seam.To;

            graph.RemoveEdge(seamEdgeId);

            foreach (var n in frag.NewNodes) graph.AddNode(n);
            foreach (var e in frag.NewEdges) graph.AddEdge(e);

            graph.AddEdge(new RoomEdge(_ids.NewEdge(), a, frag.Entry));
            graph.AddEdge(new RoomEdge(_ids.NewEdge(), frag.Exit, b));
        }
    }
}
