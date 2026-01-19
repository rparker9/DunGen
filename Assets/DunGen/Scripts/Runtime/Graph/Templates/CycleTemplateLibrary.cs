using System;
using System.Collections.Generic;

namespace DunGen.Graph.Templates
{
    public interface ICycleTemplateLibrary
    {
        CycleTemplate Get(CycleType type);
    }

    public sealed class CycleTemplateLibrary : ICycleTemplateLibrary
    {
        private readonly Dictionary<CycleType, CycleTemplate> _templates =
            new Dictionary<CycleType, CycleTemplate>();

        public void Register(CycleTemplate template)
        {
            _templates[template.Type] = template;
        }

        public CycleTemplate Get(CycleType type)
        {
            CycleTemplate t;
            if (!_templates.TryGetValue(type, out t))
                throw new InvalidOperationException("No CycleTemplate registered for: " + type);
            return t;
        }
    }
}
