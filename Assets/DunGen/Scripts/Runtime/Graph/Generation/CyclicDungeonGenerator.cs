using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;
using DunGen.Graph.Generation.Rules;

using System;
using System.Collections.Generic;

namespace DunGen.Graph.Generation
{
    public sealed class CyclicDungeonGenerator
    {
        public sealed class Settings
        {
            public int Seed = 12345;
            public int MaxDepth = 3;
            public int MaxInsertionsTotal = 32;
        }

        private readonly ICycleTemplateLibrary _templates;
        private readonly ICycleSelector _selector;
        private readonly GraphRewriteEngine _rewriter;
        private readonly CycleRuleRegistry _rules;

        // Constructor

        /// <summary>
        /// Creates a new cyclic dungeon generator.
        /// </summary>
        /// <param name="templates"></param>
        /// <param name="selector"></param>
        /// <param name="rewriter"></param>
        /// <param name="rules"></param>
        public CyclicDungeonGenerator(
            ICycleTemplateLibrary templates,
            ICycleSelector selector,
            GraphRewriteEngine rewriter,
            CycleRuleRegistry rules)
        {
            _templates = templates;
            _selector = selector;
            _rewriter = rewriter;
            _rules = rules;
        }

        public GenerationResult Generate(Settings s)
        {
            var rng = new Random(s.Seed);
            var graph = new DungeonGraph();

            // NEW: Cycle tracking structures
            var insertionHistory = new List<InsertionEvent>();
            var cycleRegistry = new Dictionary<CycleId, CycleInstanceInfo>();
            var nodeToCycle = new Dictionary<NodeId, CycleId>();
            var edgeToArc = new Dictionary<EdgeId, (CycleId, int)>();

            // 1) Generate overall cycle
            var overallType = _selector.SelectOverall(rng);
            var overallTemplate = _templates.Get(overallType);
            _rules.TryGet(overallType, out var rule);

            var overallCycle = _rewriter.Instantiate(overallTemplate, depth: 0);

            // Add to flat graph
            foreach (var n in overallCycle.NewNodes) graph.AddNode(n);
            foreach (var e in overallCycle.NewEdges) graph.AddEdge(e);

            // NEW: Register cycle info
            RegisterCycle(overallCycle, cycleRegistry, nodeToCycle, edgeToArc);

            rule?.OnOverallInstantiated(graph, overallCycle);

            // 2) Replace insertion points with sub-cycles
            var pending = new Queue<InsertionPoint>(overallCycle.NewInsertions);
            int used = pending.Count;

            while (pending.Count > 0)
            {
                var insertion = pending.Dequeue();

                if (insertion.Depth + 1 > s.MaxDepth || used >= s.MaxInsertionsTotal)
                    continue;

                var subType = _selector.SelectSub(rng, insertion.Depth + 1);
                var subTemplate = _templates.Get(subType);

                if (!graph.Edges.TryGetValue(insertion.SeamEdge, out var seam))
                    throw new InvalidOperationException("Seam edge missing: " + insertion.SeamEdge);

                var parentFrom = seam.From;
                var parentTo = seam.To;
                var parentCycleId = nodeToCycle[parentFrom];

                // Instantiate with parent tracking
                var subCycle = _rewriter.Instantiate(
                    subTemplate,
                    insertion.Depth + 1,
                    parentCycleId,
                    insertion.Id);

                _rewriter.SpliceReplaceEdge(graph, insertion.SeamEdge, subCycle);

                // NEW: Register sub-cycle
                RegisterCycle(subCycle, cycleRegistry, nodeToCycle, edgeToArc);

                insertionHistory.Add(new InsertionEvent(
                    insertion,
                    parentFrom,
                    parentTo,
                    subCycle,
                    subType));

                rule?.OnSubCycleInserted(graph, insertion, subCycle, rng);

                foreach (var ni in subCycle.NewInsertions)
                {
                    pending.Enqueue(ni);
                    used++;
                    if (used >= s.MaxInsertionsTotal)
                        break;
                }
            }

            rule?.OnGenerationFinished(graph);

            return new GenerationResult(
                graph,
                cycleRegistry,
                nodeToCycle,
                edgeToArc,
                insertionHistory,
                overallType,
                overallCycle);
        }

        /// <summary>
        /// Register a cycle instance into the tracking structures.
        /// </summary>
        /// <param name="cycle"></param>
        /// <param name="cycleRegistry"></param>
        /// <param name="nodeToCycle"></param>
        /// <param name="edgeToArc"></param>
        private void RegisterCycle(
            CycleInstance cycle,
            Dictionary<CycleId, CycleInstanceInfo> cycleRegistry,
            Dictionary<NodeId, CycleId> nodeToCycle,
            Dictionary<EdgeId, (CycleId, int)> edgeToArc)
        {
            var info = cycle.CycleInfo;
            cycleRegistry[info.Id] = info;

            // Map nodes to cycle
            foreach (var n in cycle.NewNodes)
                nodeToCycle[n.Id] = info.Id;

            // Map arc A edges
            for (int i = 0; i < info.ArcA.Count; i++)
                edgeToArc[info.ArcA[i]] = (info.Id, 0);

            // Map arc B edges
            for (int i = 0; i < info.ArcB.Count; i++)
                edgeToArc[info.ArcB[i]] = (info.Id, 1);
        }
    }
}
