#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using GraphPlanarityTesting.Graphs.DataStructures;
using GraphPlanarityTesting.PlanarityTesting.BoyerMyrvold;

namespace DunGen.Editor
{
    /// <summary>
    /// Planar preview layout for a <see cref="FlatGraph"/>.
    ///
    /// Pipeline:
    /// 1) Convert FlatGraph -> undirected simple IGraph.
    /// 2) Run planarity + get faces.
    /// 3) Choose an outer face.
    /// 4) Compute a straight-line embedding via iterative barycentric placement:
    ///    - Outer face fixed on a circle
    ///    - Interior vertices = average of neighbors (Gauss–Seidel)
    ///
    /// Notes:
    /// - This is a pragmatic editor preview layout.
    /// - Some planar graphs may still produce poor-looking drawings (non-3-connected, articulation, etc),
    ///   but edge crossings should be avoided for truly planar inputs.
    /// </summary>
    public static class PreviewLayoutEngine
    {
        public sealed class Result
        {
            public bool isPlanar;
            public string warning;
            public Dictionary<GraphNode, Vector2> positions;
            public PlanarEmbedding<GraphNode> embedding;
            public PlanarFaces<GraphNode> faces;
            public GraphNode startNode;
            public GraphNode goalNode;
        }

        public static Result Compute(FlatGraph flat, float outerRadius = 350f, int relaxIterations = 400)
        {
            var r = new Result
            {
                isPlanar = false,
                warning = null,
                positions = new Dictionary<GraphNode, Vector2>()
            };

            if (flat == null || flat.IsEmpty)
            {
                r.warning = "No graph to layout.";
                return r;
            }

            var g = BuildUndirectedSimpleGraph(flat);

            var bm = new BoyerMyrvold<GraphNode>();

            if (!bm.IsPlanar(g, out var embedding))
            {
                r.isPlanar = false;
                r.warning = "Non-planar: crossings are unavoidable. Using fallback layout.";
                return r;
            }

            r.isPlanar = true;
            r.embedding = embedding;

            // Faces are useful to pick an outer boundary.
            if (!bm.TryGetPlanarFaces(g, out var faces) || faces == null)
            {
                // Still planar, but without faces we'll pick a fallback outer ring.
                r.faces = null;
                r.warning = "Planar, but faces unavailable. Using fallback planar placement.";
                r.positions = FallbackCircle(flat, outerRadius);
                return r;
            }

            r.faces = faces;

            // Choose an outer face cycle (best-effort).
            var outerCycle = TryChooseOuterFaceCycle(faces);

            if (outerCycle == null || outerCycle.Count < 3)
            {
                r.warning = "Planar, but could not choose an outer face. Using fallback planar placement.";
                r.positions = FallbackCircle(flat, outerRadius);
                return r;
            }

            r.positions = ComputeBarycentricEmbedding(g, outerCycle, outerRadius, relaxIterations);
            return r;
        }

        // =========================================================
        // Graph conversion (FlatGraph -> IGraph<GraphNode>)
        // =========================================================

        private static IGraph<GraphNode> BuildUndirectedSimpleGraph(FlatGraph flat)
        {
            var graph = new UndirectedAdjacencyListGraph<GraphNode>();

            if (flat.nodes != null)
            {
                for (int i = 0; i < flat.nodes.Count; i++)
                {
                    var v = flat.nodes[i];
                    if (v != null)
                        graph.AddVertex(v);
                }
            }

            // No parallel edges allowed (your rule).
            var seen = new HashSet<UndirectedPair>();

            if (flat.edges != null)
            {
                for (int i = 0; i < flat.edges.Count; i++)
                {
                    var e = flat.edges[i];
                    if (e == null || e.from == null || e.to == null)
                        continue;

                    if (ReferenceEquals(e.from, e.to))
                        continue;

                    var key = new UndirectedPair(e.from, e.to);
                    if (!seen.Add(key))
                        continue;

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
                // deterministic ordering by hash; good enough for editor-time dedupe
                int ha = a != null ? a.GetHashCode() : 0;
                int hb = b != null ? b.GetHashCode() : 0;

                if (ha <= hb)
                {
                    _a = a;
                    _b = b;
                }
                else
                {
                    _a = b;
                    _b = a;
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
                return obj is UndirectedPair other &&
                       ReferenceEquals(_a, other._a) &&
                       ReferenceEquals(_b, other._b);
            }
        }

        // =========================================================
        // Outer face selection (best effort)
        // =========================================================

        private static List<GraphNode> TryChooseOuterFaceCycle(PlanarFaces<GraphNode> faces)
        {
            // We don't know PlanarFaces<T> exact structure from your paste,
            // so we choose conservatively:
            // - Try common property names via reflection
            // - Expect "Faces" or something enumerable of face-walks
            //
            // If you want this non-reflection, paste PlanarFaces<T> type next.
            var t = faces.GetType();

            // Try property: Faces
            var facesProp = t.GetProperty("Faces");
            if (facesProp != null)
            {
                if (facesProp.GetValue(faces) is System.Collections.IEnumerable enumerable)
                    return ChooseLargestCycle(enumerable);
            }

            // Try property: AllFaces
            var allFacesProp = t.GetProperty("AllFaces");
            if (allFacesProp != null)
            {
                if (allFacesProp.GetValue(faces) is System.Collections.IEnumerable enumerable)
                    return ChooseLargestCycle(enumerable);
            }

            // Give up (caller will fallback).
            return null;
        }

        private static List<GraphNode> ChooseLargestCycle(System.Collections.IEnumerable facesEnumerable)
        {
            List<GraphNode> best = null;
            int bestCount = -1;

            foreach (var face in facesEnumerable)
            {
                if (face is System.Collections.IEnumerable walk)
                {
                    var cycle = new List<GraphNode>();
                    foreach (var v in walk)
                    {
                        if (v is GraphNode n && n != null)
                            cycle.Add(n);
                    }

                    // A "face walk" may repeat vertices; sanitize to a simple cycle.
                    cycle = CompressCycle(cycle);

                    if (cycle.Count > bestCount)
                    {
                        bestCount = cycle.Count;
                        best = cycle;
                    }
                }
            }

            return best;
        }

        private static List<GraphNode> CompressCycle(List<GraphNode> cycle)
        {
            if (cycle == null || cycle.Count == 0)
                return cycle;

            // Remove consecutive duplicates
            var cleaned = new List<GraphNode>(cycle.Count);
            GraphNode prev = null;
            for (int i = 0; i < cycle.Count; i++)
            {
                var n = cycle[i];
                if (n == null) continue;
                if (!ReferenceEquals(prev, n))
                    cleaned.Add(n);
                prev = n;
            }

            // Remove closing duplicate if present
            if (cleaned.Count >= 2 && ReferenceEquals(cleaned[0], cleaned[^1]))
                cleaned.RemoveAt(cleaned.Count - 1);

            return cleaned;
        }

        // =========================================================
        // Barycentric / Tutte-style placement
        // =========================================================

        private static Dictionary<GraphNode, Vector2> ComputeBarycentricEmbedding(
            IGraph<GraphNode> g,
            List<GraphNode> outerCycle,
            float outerRadius,
            int iterations)
        {
            var pos = new Dictionary<GraphNode, Vector2>();
            var isOuter = new HashSet<GraphNode>(outerCycle);

            // 1) Place outer vertices on a circle
            for (int i = 0; i < outerCycle.Count; i++)
            {
                float a = (i / (float)outerCycle.Count) * Mathf.PI * 2f;
                pos[outerCycle[i]] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outerRadius;
            }

            // 2) Initialize interior vertices near origin (or small jitter)
            foreach (var v in g.Vertices)
            {
                if (v == null) continue;
                if (pos.ContainsKey(v)) continue;

                pos[v] = UnityEngine.Random.insideUnitCircle * (outerRadius * 0.1f);
            }

            // 3) Relax interior vertices: v = avg(neighbors)
            // (Gauss–Seidel updates in-place)
            for (int it = 0; it < iterations; it++)
            {
                foreach (var v in g.Vertices)
                {
                    if (v == null) continue;
                    if (isOuter.Contains(v)) continue;

                    Vector2 sum = Vector2.zero;
                    int deg = 0;

                    foreach (var n in g.GetNeighbours(v))
                    {
                        if (n == null) continue;
                        sum += pos[n];
                        deg++;
                    }

                    if (deg > 0)
                        pos[v] = sum / deg;
                }
            }

            return pos;
        }

        // =========================================================
        // Fallback
        // =========================================================

        public static Dictionary<GraphNode, Vector2> FallbackCircle(FlatGraph flat, float radius)
        {
            var pos = new Dictionary<GraphNode, Vector2>();

            if (flat?.nodes == null || flat.nodes.Count == 0)
                return pos;

            var nodes = flat.nodes;
            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null) count++;

            if (count == 0) return pos;

            int k = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                float a = (k / (float)count) * Mathf.PI * 2f;
                pos[n] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                k++;
            }

            return pos;
        }
    }
}
#endif
