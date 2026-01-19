using DunGen.Graph.Templates;
using System.Collections.Generic;

namespace DunGen.Graph.Generation.Rules
{
    public sealed class CycleRuleRegistry
    {
        private readonly Dictionary<CycleType, ICycleRule> _rules = new Dictionary<CycleType, ICycleRule>();

        public void Register(ICycleRule rule)
        {
            _rules[rule.Type] = rule;
        }

        public bool TryGet(CycleType type, out ICycleRule rule)
        {
            return _rules.TryGetValue(type, out rule);
        }
    }
}
