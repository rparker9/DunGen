using DunGen.Graph.Core;

namespace DunGen.Graph.Templates
{
    /// <summary>
    /// Built-in CycleTemplate definitions that match the PDF’s diagrams.
    ///
    /// These are "blueprints" (templates), not the final runtime DungeonGraph.
    /// - Graph.Core types (DungeonGraph / RoomNode / RoomEdge) are the OUTPUT graph you generate and display.
    /// - Graph.Templates types (CycleTemplate / TemplateNode / TemplateEdge) are reusable patterns you instantiate
    ///   into Graph.Core later.
    /// </summary>
    public static class BuiltInTemplates
    {
        /// <summary>
        /// Call this once at startup to register the built-in templates into your library.
        /// </summary>
        public static void Register(CycleTemplateLibrary lib)
        {
            lib.Register(BuildTwoAlternativePaths());
            lib.Register(BuildTwoKeys());
        }

        /// <summary>
        /// PDF Cycle #1: Two alternative paths.
        ///
        /// Diagram summary:
        /// - Start and Goal connected by two long arcs
        /// - Each arc represents a distinct "theme" (Theme 1 vs Theme 2)
        /// - Each arc has a diamond insertion point where a sub-cycle can be inserted
        /// </summary>
        private static CycleTemplate BuildTwoAlternativePaths()
        {
            // Arcs are defined as ordered edge chains from Start -> Goal.
            var arcTop = new TemplateArc("Theme 1", ArcLengthHint.Long);
            var arcBottom = new TemplateArc("Theme 2", ArcLengthHint.Long);

            var t = new CycleTemplate(CycleType.TwoAlternativePaths, arcTop, arcBottom);

            // Template-local node IDs:
            // 1 = Start, 2 = Goal
            t.AddNode(1, NodeKind.Start, "Start");
            t.AddNode(2, NodeKind.Goal, "Goal");

            t.SetStart(new TNodeId(1));
            t.SetGoal(new TNodeId(2));

            // Template-local edge IDs:
            // 1 = top arc Start->Goal
            // 2 = bottom arc Start->Goal
            //
            // NOTE: In the PDF the loop is conceptually bidirectional unless an arc is marked one-way.
            // For MVP, arcs are represented as Start->Goal edges. If you want explicit backtracking,
            // you can later add the reverse edges (Goal->Start) in instantiation.
            t.AddEdge(1, from: 1, to: 2, traversal: EdgeTraversal.Normal);
            t.AddEdge(2, from: 1, to: 2, traversal: EdgeTraversal.Normal);

            arcTop.EdgeChain.Add(new TEdgeId(1));
            arcBottom.EdgeChain.Add(new TEdgeId(2));

            // Diamonds are insertion points.
            // We represent an insertion point as an "edge seam": later rewriting will replace that edge with a sub-cycle.
            //
            // Here: one seam per arc.
            t.AddInsertion(insertionId: 1, seamEdgeId: 1); // Diamond insertion point on Theme 1 arc
            t.AddInsertion(insertionId: 2, seamEdgeId: 2); // Diamond insertion point on Theme 2 arc

            return t;
        }

        /// <summary>
        /// PDF Cycle #2: Two keys.
        ///
        /// Diagram summary:
        /// - Start and Goal connected by two long arcs
        /// - Goal contains a lock (Goal (LOCK))
        /// - Each long path contains a key (Key 1 on one arc, Key 2 on the other)
        /// - Each arc has a diamond insertion point
        ///
        /// IMPORTANT MVP NOTE:
        /// In the PDF process, the diamonds are exactly where you insert sub-cycles (wings),
        /// and those wings typically become where each key is found.
        /// So this base template focuses on the structure + insertion seams,
        /// and later the generator can force “inserted sub-cycle’s goal = Key”.
        /// </summary>
        private static CycleTemplate BuildTwoKeys()
        {
            var arcTop = new TemplateArc("Key 1 path", ArcLengthHint.Long);
            var arcBottom = new TemplateArc("Key 2 path", ArcLengthHint.Long);

            var t = new CycleTemplate(CycleType.TwoKeys, arcTop, arcBottom);

            // 1 = Start, 2 = Goal (Lock)
            t.AddNode(1, NodeKind.Start, "Start");
            t.AddNode(2, NodeKind.Goal, "Goal (Lock)")
                // Beginner hint: NodeTag is on nodes. This is not a real “Lock” system yet,
                // but it gives the UI/generator a marker that this goal is locked.
                .Tags.Add(new NodeTag(NodeTagKind.LockHint));

            t.SetStart(new TNodeId(1));
            t.SetGoal(new TNodeId(2));

            // 1 = top arc Start->Goal (Key 1 path)
            // 2 = bottom arc Start->Goal (Key 2 path)
            t.AddEdge(1, from: 1, to: 2, traversal: EdgeTraversal.Normal);
            t.AddEdge(2, from: 1, to: 2, traversal: EdgeTraversal.Normal);

            arcTop.EdgeChain.Add(new TEdgeId(1));
            arcBottom.EdgeChain.Add(new TEdgeId(2));

            // Diamonds on each arc: where we will splice in sub-cycles that (typically) contain the keys.
            t.AddInsertion(insertionId: 1, seamEdgeId: 1); // Diamond insertion point for Key 1 wing
            t.AddInsertion(insertionId: 2, seamEdgeId: 2); // Diamond insertion point for Key 2 wing

            return t;
        }
    }
}
