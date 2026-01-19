using DunGen.Graph.Generation.Rules;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;

namespace DunGen.Graph.Generation
{
    /// <summary>
    /// One place to assemble the generator and all its dependencies.
    /// For MVP, you can call this from an EditorWindow button.
    /// Later, you can reuse it for runtime generation too.
    /// </summary>
    public static class GenerationBootstrap
    {
        public static CyclicDungeonGenerator CreateDefaultGenerator()
        {
            // 1) Templates
            var lib = new CycleTemplateLibrary();
            BuiltInTemplates.Register(lib); // your TwoAlternativePaths + TwoKeys templates live here

            // 2) Rewrite engine infrastructure
            var ids = new IdAllocator();
            var rewriter = new GraphRewriteEngine(ids);

            // 3) Selector (random choice policy)
            ICycleSelector selector = new DefaultCycleSelector(); // :contentReference[oaicite:1]{index=1}

            // 4) Rules (cycle-specific behaviors)
            var rules = new CycleRuleRegistry();
            rules.Register(new TwoKeysRule()); // adds gating + key placement behavior

            // 5) Generator
            return new CyclicDungeonGenerator(lib, selector, rewriter, rules);
        }
    }
}
