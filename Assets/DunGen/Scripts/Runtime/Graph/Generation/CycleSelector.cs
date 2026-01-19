using System;
using DunGen.Graph.Templates;

namespace DunGen.Graph.Generation
{
    public interface ICycleSelector
    {
        CycleType SelectOverall(Random rng);
        CycleType SelectSub(Random rng, int depth);
    }

    public sealed class DefaultCycleSelector : ICycleSelector
    {
        // Only pick from templates that actually exist right now.
        private static readonly CycleType[] Implemented =
        {
            CycleType.TwoAlternativePaths,
            CycleType.TwoKeys
        };

        public CycleType SelectOverall(Random rng)
        {
            return Implemented[rng.Next(0, Implemented.Length)];
        }

        public CycleType SelectSub(Random rng, int depth)
        {
            return Implemented[rng.Next(0, Implemented.Length)];
        }
    }
}
