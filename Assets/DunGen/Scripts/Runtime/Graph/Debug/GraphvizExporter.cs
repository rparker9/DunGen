using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DunGen.Graph.Core;
using DunGen.Graph.Generation;
using DunGen.Graph.Rewrite;

namespace DunGen.Graph.Debug
{
    /// <summary>
    /// Exports a DungeonGraph (optionally GenerationResult) to Graphviz DOT.
    ///
    /// Features:
    /// - Start -> Goal shortest path highlighting (BFS, ignores gates)
    /// - Cluster nesting using insertion seam ownership (based on which fragment created the seam edge)
    /// - Node & edge styling (start/goal, tags, gates)
    /// </summary>
    public static class GraphvizExporter
    {
        public static string Export(GenerationResult result)
            => Export(result.Graph, result);

        public static string Export(DungeonGraph graph, GenerationResult result = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("digraph DunGen {");
            sb.AppendLine("  rankdir=LR;");
            sb.AppendLine("  splines=ortho;");
            sb.AppendLine("  nodesep=0.6;");
            sb.AppendLine("  ranksep=0.8;");
            sb.AppendLine("  node [shape=box, style=rounded];");
            sb.AppendLine();

            // --------------------------------------------------
            // Compute solution path (Start -> Goal)
            // --------------------------------------------------

            var solutionEdgeIds = new HashSet<EdgeId>();
            var solutionNodeIds = new HashSet<NodeId>();

            if (TryComputeSolutionPath(graph, out var pathNodeIds))
            {
                foreach (var nid in pathNodeIds)
                    solutionNodeIds.Add(nid);

                for (int i = 0; i < pathNodeIds.Count - 1; i++)
                {
                    var from = pathNodeIds[i];
                    var to = pathNodeIds[i + 1];

                    // OutEdges() returns EdgeId, so resolve to RoomEdge.
                    var eid = graph.OutEdges(from)
                                   .FirstOrDefault(x => graph.GetEdge(x).To == to);

                    if (!eid.Equals(default(EdgeId)))
                        solutionEdgeIds.Add(eid);
                }
            }

            // --------------------------------------------------
            // Clusters (optional) + node emission
            // --------------------------------------------------

            // node -> clusterId (only one cluster per node; deepest cluster wins)
            var nodeToCluster = new Dictionary<NodeId, int>();

            // Build cluster tree (includes an "Overall" root if result != null)
            var roots = BuildClusters(graph, result, nodeToCluster);

            // Emit clusters (nested). Nodes inside clusters are declared here (with styling).
            foreach (var root in roots)
                EmitClusterRecursive(sb, graph, root, nodeToCluster, solutionNodeIds);

            // Emit nodes not assigned to any cluster.
            foreach (var node in graph.Nodes.Values)
            {
                if (nodeToCluster.ContainsKey(node.Id))
                    continue;

                EmitNode(sb, node, solutionNodeIds.Contains(node.Id));
            }

            sb.AppendLine();

            // --------------------------------------------------
            // Emit edges
            // --------------------------------------------------

            foreach (var edge in graph.Edges.Values)
                EmitEdge(sb, edge, solutionEdgeIds.Contains(edge.Id));

            sb.AppendLine("}");
            return sb.ToString();
        }

        // ==================================================
        // Clusters
        // ==================================================

        private sealed class Cluster
        {
            public int Id;
            public string Label;
            public List<Cluster> Children = new List<Cluster>();

            // Nodes that conceptually belong to this cluster (we’ll only *emit* nodes
            // whose nodeToCluster[node] == this.Id to avoid duplicate declarations).
            public List<NodeId> NodeIds = new List<NodeId>();
        }

        private static List<Cluster> BuildClusters(
            DungeonGraph graph,
            GenerationResult result,
            Dictionary<NodeId, int> nodeToCluster)
        {
            // No result => no clusters.
            if (result == null)
                return new List<Cluster>();

            // Create a root cluster for the overall fragment.
            var overall = new Cluster
            {
                Id = 0,
                Label = $"Overall ({result.OverallType})"
            };

            int nextId = 1;

            // Map: replacement -> cluster
            var repClusters = new List<(InsertionReplacement rep, Cluster cluster)>();

            // Map: EdgeId -> cluster that originally created that edge
            // (used to infer parent cluster for seam edges).
            var edgeOwner = new Dictionary<EdgeId, Cluster>();

            foreach (var rep in result.Replacements)
            {
                var c = new Cluster
                {
                    Id = nextId++,
                    Label = $"{rep.InsertedType} (depth {rep.Insertion.Depth})"
                };

                // Nodes in this inserted fragment
                foreach (var nid in rep.InsertedNodeIds)
                {
                    c.NodeIds.Add(nid);
                    nodeToCluster[nid] = c.Id;
                }

                // Record ownership of edges created by this fragment
                foreach (var e in rep.Inserted.NewEdges)
                    edgeOwner[e.Id] = c;

                repClusters.Add((rep, c));
            }

            // Assign overall nodes to overall cluster (unless already assigned to an inserted cluster).
            foreach (var n in result.OverallFragment.NewNodes)
            {
                if (!nodeToCluster.ContainsKey(n.Id))
                {
                    overall.NodeIds.Add(n.Id);
                    nodeToCluster[n.Id] = overall.Id;
                }
            }

            // Build nesting:
            // The parent of a replacement cluster is the cluster that owned the seam edge being replaced.
            // If none (seam belonged to overall), attach directly to overall.
            foreach (var (rep, child) in repClusters)
            {
                if (edgeOwner.TryGetValue(rep.Insertion.SeamEdge, out var parent) && parent != child)
                {
                    parent.Children.Add(child);
                }
                else
                {
                    overall.Children.Add(child);
                }
            }

            return new List<Cluster> { overall };
        }

        private static void EmitClusterRecursive(
            StringBuilder sb,
            DungeonGraph graph,
            Cluster c,
            Dictionary<NodeId, int> nodeToCluster,
            HashSet<NodeId> solutionNodeIds)
        {
            sb.AppendLine($"  subgraph cluster_{c.Id} {{");
            sb.AppendLine($"    label=\"{Escape(c.Label)}\";");
            sb.AppendLine("    color=blue;");

            // Emit node declarations for nodes whose *assigned* cluster is this one.
            foreach (var nid in c.NodeIds)
            {
                if (!nodeToCluster.TryGetValue(nid, out var assigned) || assigned != c.Id)
                    continue;

                var node = graph.GetNode(nid);
                EmitNode(sb, node, solutionNodeIds.Contains(nid), indent: "    ");
            }

            // Children
            foreach (var child in c.Children)
                EmitClusterRecursive(sb, graph, child, nodeToCluster, solutionNodeIds);

            sb.AppendLine("  }");
        }

        // ==================================================
        // Nodes
        // ==================================================

        private static void EmitNode(StringBuilder sb, RoomNode n, bool onPath, string indent = "  ")
        {
            var attrs = new List<string>();

            if (n.Kind == NodeKind.Start)
            {
                attrs.Add("shape=ellipse");
                attrs.Add("color=green");
            }
            else if (n.Kind == NodeKind.Goal)
            {
                attrs.Add("shape=doublecircle");
                attrs.Add("color=red");
            }
            else
            {
                attrs.Add("color=gray");
            }

            if (onPath)
                attrs.Add("penwidth=3");

            attrs.Add($"label=\"{Escape(BuildNodeLabel(n))}\"");

            sb.AppendLine($"{indent}{NodeName(n.Id)} [{string.Join(",", attrs)}];");
        }

        private static string BuildNodeLabel(RoomNode n)
        {
            var sb = new StringBuilder();

            sb.Append(string.IsNullOrEmpty(n.DebugLabel) ? n.Id.ToString() : n.DebugLabel);

            if (n.Kind == NodeKind.Start) sb.Append("\\n(Start)");
            if (n.Kind == NodeKind.Goal) sb.Append("\\n(Goal)");

            // NodeTag is (Kind, Data). ToString already prints Kind or Kind(Data).
            foreach (var tag in n.Tags)
                sb.Append("\\n" + tag.ToString());

            return sb.ToString();
        }

        // ==================================================
        // Edges
        // ==================================================

        private static void EmitEdge(StringBuilder sb, RoomEdge e, bool onPath)
        {
            var attrs = new List<string>();

            if (e.Gate != null)
            {
                attrs.Add("color=red");
                attrs.Add("penwidth=2");
                attrs.Add($"label=\"{Escape(BuildGateLabel(e.Gate))}\"");
            }

            if (onPath)
            {
                // If it’s both gated and on-path, keep it on-path prominent.
                attrs.Add("color=blue");
                attrs.Add("penwidth=3");
            }

            if (e.Traversal != EdgeTraversal.Normal)
                attrs.Add($"xlabel=\"{Escape(e.Traversal.ToString())}\"");

            string attrStr = attrs.Count > 0 ? $" [{string.Join(",", attrs)}]" : "";
            sb.AppendLine($"  {NodeName(e.From)} -> {NodeName(e.To)}{attrStr};");
        }

        private static string BuildGateLabel(EdgeGate g)
        {
            if (g.IsMultiKey)
                return $"{g.Kind}/{g.Strength}: {string.Join("+", g.RequiredKeys)}";

            if (g.RequiredKey.HasValue)
                return $"{g.Kind}/{g.Strength}: {g.RequiredKey.Value}";

            return $"{g.Kind}/{g.Strength}";
        }

        // ==================================================
        // Solution path (BFS)
        // ==================================================

        private static bool TryComputeSolutionPath(DungeonGraph g, out List<NodeId> path)
        {
            path = null;

            var start = g.Nodes.Values.FirstOrDefault(n => n.Kind == NodeKind.Start);
            var goal = g.Nodes.Values.FirstOrDefault(n => n.Kind == NodeKind.Goal);
            if (start == null || goal == null)
                return false;

            var prev = new Dictionary<NodeId, NodeId>();
            var q = new Queue<NodeId>();

            q.Enqueue(start.Id);
            prev[start.Id] = default;

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == goal.Id)
                    break;

                foreach (var eid in g.OutEdges(cur))
                {
                    var edge = g.GetEdge(eid);
                    var next = edge.To;

                    if (prev.ContainsKey(next))
                        continue;

                    prev[next] = cur;
                    q.Enqueue(next);
                }
            }

            if (!prev.ContainsKey(goal.Id))
                return false;

            var outPath = new List<NodeId>();
            var n = goal.Id;

            // Walk back until we hit the sentinel default NodeId (set for start).
            while (true)
            {
                outPath.Add(n);
                if (!prev.TryGetValue(n, out var p))
                    break;

                if (p.Equals(default(NodeId)))
                    break;

                n = p;
            }

            // Ensure start included
            outPath.Add(start.Id);

            outPath.Reverse();
            path = outPath;
            return true;
        }

        // ==================================================
        // Helpers
        // ==================================================

        private static string NodeName(NodeId id) => $"N{id.Value}";

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            // DOT strings: escape backslash and quotes.
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
