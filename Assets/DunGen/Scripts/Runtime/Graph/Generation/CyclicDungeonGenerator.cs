using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;
using DunGen.Graph.Generation.Rules;

using System;
using System.Collections.Generic;

namespace DunGen.Graph.Generation
{
    public sealed class GenerationResult
    {
        public DungeonGraph Graph { get; }
        public IReadOnlyList<InsertionReplacement> Replacements { get; }

        public GenerationResult(DungeonGraph graph, IReadOnlyList<InsertionReplacement> replacements)
        {
            Graph = graph;
            Replacements = replacements;
        }
    }

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

            // For visualization/layout purposes, keep track of which insertion seams
            var replacements = new List<InsertionReplacement>();

            // -----------------------------
            // 1) Pick + instantiate overall cycle
            // -----------------------------
            var overallType = _selector.SelectOverall(rng);
            var overallTemplate = _templates.Get(overallType);

            // Look up optional behavior hooks for this overall cycle type.
            // (If no rule is registered, generation still works as a pure graph rewrite.)
            _rules.TryGet(overallType, out var rule);

            var overallFrag = _rewriter.Instantiate(overallTemplate, depth: 0);
            foreach (var n in overallFrag.NewNodes) graph.AddNode(n);
            foreach (var e in overallFrag.NewEdges) graph.AddEdge(e);

            // Allow the rule to observe/annotate the overall structure (ex: mark goal as locked).
            rule?.OnOverallInstantiated(graph, overallFrag);

            // -----------------------------
            // 2) Repeatedly replace diamond insertion seams with sub-cycles
            // -----------------------------
            var pending = new Queue<InsertionPointInstance>(overallFrag.NewInsertions);
            int used = pending.Count;

            while (pending.Count > 0)
            {
                var ins = pending.Dequeue();

                // Depth limit: insertion at depth D produces a sub-cycle at depth D+1
                if (ins.Depth + 1 > s.MaxDepth)
                    continue;

                // Budget limit
                if (used >= s.MaxInsertionsTotal)
                    break;

                // Pick and instantiate a sub-cycle
                var subType = _selector.SelectSub(rng, ins.Depth + 1);
                var subTemplate = _templates.Get(subType);

                // Capture the parent seam endpoints BEFORE we replace the edge.
                if (!graph.Edges.TryGetValue(ins.SeamEdge, out var seam))
                    throw new InvalidOperationException("Seam edge missing in graph: " + ins.SeamEdge);

                var parentFrom = seam.From;
                var parentTo = seam.To;

                var subFrag = _rewriter.Instantiate(subTemplate, ins.Depth + 1);
                _rewriter.SpliceReplaceEdge(graph, ins.SeamEdge, subFrag);

                // Record the replacement for visualization/layout purposes
                replacements.Add(new InsertionReplacement(ins, parentFrom, parentTo, subFrag));

                // Allow the overall-cycle rule to react to this insertion
                // (ex: TwoKeysRule tags inserted goals as keys).
                rule?.OnSubCycleInserted(graph, ins, subFrag, rng);

                // Enqueue any new diamond insert seams created by the inserted sub-cycle
                foreach (var ni in subFrag.NewInsertions)
                {
                    pending.Enqueue(ni);
                    used++;
                    if (used >= s.MaxInsertionsTotal)
                        break;
                }
            }

            // -----------------------------
            // 3) Finalization hook (ex: attach gates, cleanup tags, etc.)
            // -----------------------------
            rule?.OnGenerationFinished(graph);

            return new GenerationResult(graph, replacements);
        }
    }
}
