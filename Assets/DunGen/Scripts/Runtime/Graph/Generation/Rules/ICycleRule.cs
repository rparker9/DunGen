using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;
using System;

namespace DunGen.Graph.Generation.Rules
{
    /// <summary>
    /// A CycleRule is where "cycle-specific behavior" lives.
    /// The generator stays generic (instantiate/splice/queue),
    /// while each cycle type can inject behavior at key moments.
    /// </summary>
    public interface ICycleRule
    {
        CycleType Type { get; }

        void OnOverallInstantiated(DungeonGraph graph, SubgraphFragment overallFrag);

        void OnSubCycleInserted(
            DungeonGraph graph,
            InsertionPointInstance replacedInsertion,
            SubgraphFragment insertedFragment,
            Random rng);

        void OnGenerationFinished(DungeonGraph graph);
    }
}
