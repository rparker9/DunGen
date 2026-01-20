using DunGen.Graph.Core;
using DunGen.Graph.Templates;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DunGen.Graph.Rewrite
{
    public sealed class GraphRewriteEngine
    {
        private readonly IdAllocator _ids;

        public GraphRewriteEngine(IdAllocator ids)
        {
            _ids = ids;
        }

        public CycleInstance Instantiate(
            CycleTemplate template,
            int depth,
            CycleId? parentCycleId = null,
            InsertionId? parentInsertionId = null)
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
            var cycleId = _ids.NewCycle();  // NEW

            var instance = new CycleInstance(entry, exit);

            // Materialize nodes
            foreach (var kv in template.Nodes)
            {
                var tNode = kv.Value;
                var id = mapNode[tNode.Id];

                var kind = NodeKind.Normal;
                if (depth == 0)
                {
                    if (tNode.Kind == TNodeKind.Start)
                        kind = NodeKind.Entrance;
                    else if (tNode.Kind == TNodeKind.Goal)
                        kind = NodeKind.Exit;
                }

                var node = new RoomNode(id, kind, tNode.DebugLabel);
                node.Tags.AddRange(tNode.Tags);

                if (tNode.Kind == TNodeKind.Start)
                    node.Tags.Add(new NodeTag(NodeTagKind.CycleStart));
                if (tNode.Kind == TNodeKind.Goal)
                    node.Tags.Add(new NodeTag(NodeTagKind.CycleGoal));

                instance.NewNodes.Add(node);
            }

            // Materialize edges (tracking arc membership)
            var arcAEdges = new List<EdgeId>();
            var arcBEdges = new List<EdgeId>();

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
                instance.NewEdges.Add(edge);

                // NEW: Track which arc this edge belongs to
                if (template.ArcA.Contains(tEdge.Id))
                    arcAEdges.Add(eid);
                else if (template.ArcB.Contains(tEdge.Id))
                    arcBEdges.Add(eid);
            }

            // Create insertion points
            foreach (var ins in template.Insertions)
            {
                EdgeId seam;
                if (!mapEdge.TryGetValue(ins.SeamEdge, out seam))
                    throw new InvalidOperationException("Template insertion refers to missing seam edge.");

                instance.NewInsertions.Add(
                    new InsertionPoint(_ids.NewInsertion(), seam, depth));
            }

            // NEW: Build cycle info
            instance.CycleInfo = new CycleInstanceInfo(
                cycleId,
                template.Type,
                depth,
                entry,
                exit,
                arcAEdges,
                arcBEdges,
                instance.NewInsertions,
                parentCycleId,
                parentInsertionId);

            return instance;
        }

        /// <summary>
        /// Replace existing edge A->B with: A->frag.Entry ... frag.Exit->B.
        /// </summary>
        public void SpliceReplaceEdge(DungeonGraph graph, EdgeId seamEdgeId, CycleInstance frag)
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
