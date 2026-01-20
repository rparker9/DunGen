using DunGen.Graph.Core;
using DunGen.Graph.Templates.Core;
using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    /// <summary>
    /// Built-in cycle templates matching the 12 cycles from the PDF.
    /// Currently implements: TwoAlternativePaths, TwoKeys
    /// </summary>
    public static class BuiltInTemplates
    {
        public static void Register(CycleTemplateLibrary lib)
        {
            lib.Register(TwoAlternativePaths());
            lib.Register(TwoKeys());
            // TODO: Add remaining 10 cycle types
        }

        public static CycleTemplate TwoAlternativePaths()
        {
            var b = new CycleTemplateBuilder(CycleType.TwoAlternativePaths);

            var start = b.AddNode(TNodeKind.Start, "Start");
            var goal = b.AddNode(TNodeKind.Goal, "Goal");

            var mid1 = b.AddNode(TNodeKind.Normal, "Theme1");
            var mid2 = b.AddNode(TNodeKind.Normal, "Theme2");

            // Build Arc A: start -> mid1 -> goal
            var e1 = b.AddEdge(start, mid1, EdgeTraversal.Normal);
            var e2 = b.AddEdge(mid1, goal, EdgeTraversal.Normal);
            var arcA = new List<TEdgeId> { e1, e2 };

            // Build Arc B: start -> mid2 -> goal
            var e3 = b.AddEdge(start, mid2, EdgeTraversal.Normal);
            var e4 = b.AddEdge(mid2, goal, EdgeTraversal.Normal);
            var arcB = new List<TEdgeId> { e3, e4 };

            // Insertion points (diamonds on each arc)
            b.AddInsertion(e1);  // diamond on arc A
            b.AddInsertion(e3);  // diamond on arc B

            return b.Build(start, goal, arcA, arcB);
        }

        public static CycleTemplate TwoKeys()
        {
            var b = new CycleTemplateBuilder(CycleType.TwoKeys);

            var start = b.AddNode(TNodeKind.Start, "Start");
            var goal = b.AddNode(TNodeKind.Goal, "Goal (Lock)");

            var keyMid1 = b.AddNode(TNodeKind.Normal, "Key1");
            var keyMid2 = b.AddNode(TNodeKind.Normal, "Key2");

            // Arc A: start -> keyMid1 -> goal
            var e1 = b.AddEdge(start, keyMid1, EdgeTraversal.Normal);
            var e2 = b.AddEdge(keyMid1, goal, EdgeTraversal.Normal);
            var arcA = new List<TEdgeId> { e1, e2 };

            // Arc B: start -> keyMid2 -> goal
            var e3 = b.AddEdge(start, keyMid2, EdgeTraversal.Normal);
            var e4 = b.AddEdge(keyMid2, goal, EdgeTraversal.Normal);
            var arcB = new List<TEdgeId> { e3, e4 };

            b.AddInsertion(e1);
            b.AddInsertion(e3);

            return b.Build(start, goal, arcA, arcB);
        }
    }
}