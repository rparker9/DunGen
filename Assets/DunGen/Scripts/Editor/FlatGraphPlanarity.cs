#if UNITY_EDITOR
using System.Collections.Generic;
using GraphPlanarityTesting.Graphs.DataStructures;
using GraphPlanarityTesting.PlanarityTesting.BoyerMyrvold;

namespace DunGen.Editor
{
    /// <summary>
    /// Adapter between DunGen's <see cref="FlatGraph"/> and GraphPlanarityTesting.
    ///
    /// Responsibilities:
    /// - Convert FlatGraph to an undirected simple graph (no parallel edges).
    /// - Run Boyer–Myrvold planarity test.
    /// - Return embedding / faces when planar.
    /// </summary>
    public static class FlatGraphPlanarity
    {
        public static bool IsPlanar(FlatGraph flat)
        {
            if (flat == null || flat.IsEmpty)
                return false;

            var g = BuildUndirectedSimpleGraph(flat);
            return new BoyerMyrvold<GraphNode>().IsPlanar(g);
        }

        public static bool TryGetEmbedding(FlatGraph flat, out PlanarEmbedding<GraphNode> embedding)
        {
            embedding = null;

            if (flat == null || flat.IsEmpty)
                return false;

            var g = BuildUndirectedSimpleGraph(flat);
            return new BoyerMyrvold<GraphNode>().IsPlanar(g, out embedding);
        }

        public static bool TryGetFaces(FlatGraph flat, out PlanarFaces<GraphNode> faces)
        {
            faces = null;

            if (flat == null || flat.IsEmpty)
                return false;

            var g = BuildUndirectedSimpleGraph(flat);
            return new BoyerMyrvold<GraphNode>().TryGetPlanarFaces(g, out faces);
        }

        /// <summary>
        /// Build an undirected simple graph from a FlatGraph:
        /// - Undirected for planarity.
        /// - Dedup edges (no parallel edges allowed).
        /// - Skip self-loops.
        /// </summary>
        private static IGraph<GraphNode> BuildUndirectedSimpleGraph(FlatGraph flat)
        {
            var graph = new UndirectedAdjacencyListGraph<GraphNode>();

            // Add vertices
            if (flat.nodes != null)
            {
                for (int i = 0; i < flat.nodes.Count; i++)
                {
                    var v = flat.nodes[i];
                    if (v != null)
                        graph.AddVertex(v);
                }
            }

            // Add edges (dedup as undirected)
            var seen = new HashSet<UndirectedPair>();

            if (flat.edges != null)
            {
                for (int i = 0; i < flat.edges.Count; i++)
                {
                    var e = flat.edges[i];
                    if (e == null || e.from == null || e.to == null)
                        continue;

                    if (ReferenceEquals(e.from, e.to))
                        continue; // self-loop

                    var key = new UndirectedPair(e.from, e.to);
                    if (!seen.Add(key))
                        continue; // parallel edge (disallowed)

                    // graph.AddEdge is undirected in this implementation.
                    graph.AddEdge(e.from, e.to);
                }
            }

            return graph;
        }

        private readonly struct UndirectedPair
        {
            private readonly GraphNode _a;
            private readonly GraphNode _b;

            public UndirectedPair(GraphNode a, GraphNode b)
            {
                // Order deterministically by reference hash to make (a,b)==(b,a)
                int ha = a != null ? a.GetHashCode() : 0;
                int hb = b != null ? b.GetHashCode() : 0;

                if (ha < hb)
                {
                    _a = a;
                    _b = b;
                }
                else if (hb < ha)
                {
                    _a = b;
                    _b = a;
                }
                else
                {
                    // Rare hash collision: fall back to ReferenceEquals ordering
                    _a = a;
                    _b = b;
                }
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_a != null ? _a.GetHashCode() : 0) * 397) ^ (_b != null ? _b.GetHashCode() : 0);
                }
            }

            public override bool Equals(object obj)
            {
                return obj is UndirectedPair other && ReferenceEquals(_a, other._a) && ReferenceEquals(_b, other._b);
            }
        }
    }
}
#endif
