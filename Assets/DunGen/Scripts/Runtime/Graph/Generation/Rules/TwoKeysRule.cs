using DunGen.Graph.Core;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;

using System;
using System.Collections.Generic;

namespace DunGen.Graph.Generation.Rules
{
    public sealed class TwoKeysRule : ICycleRule
    {
        public CycleType Type => CycleType.TwoKeys;

        private NodeId _overallGoal;
        private int _nextKeyIndex;
        private readonly List<KeyId> _spawnedKeys = new List<KeyId>();

        public void OnOverallInstantiated(DungeonGraph graph, CycleInstance overall)
        {
            _overallGoal = overall.Exit;
            _nextKeyIndex = 1;
            _spawnedKeys.Clear();

            // Mark goal as "locked" for display / debugging.
            var goal = graph.GetNode(_overallGoal);
            goal.Tags.Add(new NodeTag(NodeTagKind.LockHint));
            if (string.IsNullOrEmpty(goal.DebugLabel))
                goal.DebugLabel = "Goal (Lock)";
        }

        public void OnSubCycleInserted(
            DungeonGraph graph,
            InsertionPoint replacedInsertion,
            CycleInstance inserted,
            Random rng)
        {
            // TwoKeys needs exactly two keys.
            if (_spawnedKeys.Count >= 2)
                return;

            var keyId = new KeyId(_nextKeyIndex++);
            _spawnedKeys.Add(keyId);

            var keyNode = graph.GetNode(inserted.Exit);
            keyNode.Tags.Add(new NodeTag(NodeTagKind.Key, keyId.Value));
            keyNode.DebugLabel = $"Key {keyId.Value}";
        }

        public void OnGenerationFinished(DungeonGraph graph)
        {
            if (_spawnedKeys.Count != 2)
                return;

            // Lock every edge that ENTERS the overall goal node.
            foreach (var kv in graph.Edges)
            {
                var edge = kv.Value;
                if (edge.To != _overallGoal)
                    continue;

                edge.Gate = new EdgeGate(
                    new GateId(1),            // MVP placeholder; later make unique
                    GateKind.Lock,
                    GateStrength.Hard,
                    _spawnedKeys);            // multi-key lock
            }
        }
    }
}
