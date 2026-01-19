#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

using DunGen.Graph.Core;
using DunGen.Graph.Generation;
using DunGen.Graph.Generation.Rules;
using DunGen.Graph.Rewrite;
using DunGen.Graph.Templates;

public sealed class DungeonGraphEditorWindow : EditorWindow
{
    // -----------------------------
    // Settings UI
    // -----------------------------
    private int _seed = 12345;
    private int _maxDepth = 3;
    private int _maxInsertions = 32;

    // Optional: force overall type for debugging.
    private bool _forceOverall;
    private CycleType _forcedOverallType = CycleType.TwoAlternativePaths;

    // Generated output
    private GenerationResult _result;

    // Simple layout cache (node -> position)
    private readonly Dictionary<NodeId, Vector2> _nodePos = new Dictionary<NodeId, Vector2>();

    // Circle layout cache (node -> angle on circle)
    private readonly Dictionary<NodeId, float> _nodeAngleRad = new Dictionary<NodeId, float>();

    // Per-node ring info (so we can draw nested cycles with different centers/radii)
    private readonly Dictionary<NodeId, Vector2> _nodeCircleCenter = new Dictionary<NodeId, Vector2>();
    private readonly Dictionary<NodeId, float> _nodeCircleRadius = new Dictionary<NodeId, float>();

    // Scroll
    private Vector2 _scroll;

    [MenuItem("Tools/DunGen/Dungeon Graph Viewer")]
    public static void Open()
    {
        GetWindow<DungeonGraphEditorWindow>("Dungeon Graph");
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space();

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawGraph(rect, _result);
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Randomize seed button
                if (GUILayout.Button("Randomize Seed", GUILayout.Width(120)))
                {
                    _seed = new Random().Next();
                }
                _seed = EditorGUILayout.IntField("Seed", _seed);
                _maxDepth = EditorGUILayout.IntField("Max Depth", _maxDepth);
                _maxInsertions = EditorGUILayout.IntField("Max Insertions", _maxInsertions);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _forceOverall = EditorGUILayout.ToggleLeft("Force Overall Type", _forceOverall, GUILayout.Width(150));
                using (new EditorGUI.DisabledScope(!_forceOverall))
                {
                    _forcedOverallType = (CycleType)EditorGUILayout.EnumPopup(_forcedOverallType);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Generate", GUILayout.Width(120)))
                {
                    Generate();
                    Repaint();
                }

                if (GUILayout.Button("Clear", GUILayout.Width(120)))
                {
                    _result = null;
                    _nodePos.Clear();
                    _nodeAngleRad.Clear();
                    _nodeCircleCenter.Clear();
                    _nodeCircleRadius.Clear();
                }
            }
        }
    }

    private void Generate()
    {
        // -----------------------------
        // Bootstrap dependencies
        // -----------------------------

        // Templates
        var templates = new CycleTemplateLibrary();
        BuiltInTemplates.Register(templates); // your TwoAlternativePaths + TwoKeys are registered here

        // Rewrite engine
        var ids = new IdAllocator();
        var rewriter = new GraphRewriteEngine(ids);

        // Selector (random policy)
        ICycleSelector selector =
            _forceOverall
                ? new ForcedOverallCycleSelector(_forcedOverallType)
                : (ICycleSelector)new DefaultCycleSelector(); 

        // Rules (cycle-specific behaviors)
        var rules = new CycleRuleRegistry();
        rules.Register(new TwoKeysRule()); // makes TwoKeys produce keys + locks

        // Generator
        var gen = new CyclicDungeonGenerator(templates, selector, rewriter, rules);

        // Generate
        _result = gen.Generate(new CyclicDungeonGenerator.Settings
        {
            Seed = _seed,
            MaxDepth = _maxDepth,
            MaxInsertionsTotal = _maxInsertions
        });

        // Layout nodes for display (nested)
        LayoutNested(_result);
    }

    // -----------------------------
    // Rendering
    // -----------------------------

    private void DrawGraph(Rect rect, GenerationResult result)
    {
        // Background
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));

        if (result == null || result.Graph == null)
        {
            GUI.Label(new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 20), "No graph generated yet.");
            return;
        }

        var graph = result.Graph;

        // Offset all drawing by rect origin (and some padding)
        var origin = new Vector2(rect.x + 20, rect.y + 20);

        foreach (var kv in graph.Edges)
        {
            var e = kv.Value;

            if (!_nodePos.TryGetValue(e.From, out var fromPos) || !_nodePos.TryGetValue(e.To, out var toPos))
                continue;

            bool isLocked =
                e.Gate != null &&
                (e.Gate.RequiredKey.HasValue ||
                 (e.Gate.RequiredKeys != null && e.Gate.RequiredKeys.Count > 0));

            Handles.color = isLocked
                ? new Color(1f, 0.35f, 0.35f, 1f)
                : new Color(0.85f, 0.85f, 0.85f, 1f);

            // Offset all drawing by rect origin
            fromPos += origin;
            toPos += origin;

            // Pull ring + angle data into locals first so the compiler knows they're assigned.
            Vector2 cA, cB;
            float rA = 0f, rB = 0f;
            float angA = 0f, angB = 0f;

            bool hasRingA = _nodeCircleCenter.TryGetValue(e.From, out cA) && _nodeCircleRadius.TryGetValue(e.From, out rA);
            bool hasRingB = _nodeCircleCenter.TryGetValue(e.To, out cB) && _nodeCircleRadius.TryGetValue(e.To, out rB);
            bool hasAngA = _nodeAngleRad.TryGetValue(e.From, out angA);
            bool hasAngB = _nodeAngleRad.TryGetValue(e.To, out angB);

            bool sameRing =
                hasRingA && hasRingB && hasAngA && hasAngB &&
                (cA - cB).sqrMagnitude < 0.01f &&
                Mathf.Abs(rA - rB) < 0.01f;


            if (sameRing)
            {
                // arc along that ring
                var centerWorld = origin + cA;
                DrawCircularArc(centerWorld, rA, angA, angB, segments: 48, thickness: 3f);

                if (isLocked)
                {
                    float midAng = MidAngleAlongShortestArc(angA, angB);
                    var mid = PointOnCircle(centerWorld, rA, midAng);
                    Handles.DrawSolidDisc(mid, Vector3.forward, 4f);
                }

                DrawArcArrowHead(centerWorld, rA, angA, angB);
            }
            else
            {
                // connector between rings (straight for MVP)
                Handles.DrawAAPolyLine(3f, fromPos, toPos);
                DrawArrowHead(fromPos, toPos);

                if (isLocked)
                {
                    var mid = (fromPos + toPos) * 0.5f;
                    Handles.DrawSolidDisc(mid, Vector3.forward, 4f);
                }
            }
        }

        // Draw nodes
        foreach (var kv in graph.Nodes)
        {
            var n = kv.Value;
            if (!_nodePos.TryGetValue(n.Id, out var p))
                continue;

            p += origin;

            var r = new Rect(p.x - 35, p.y - 18, 70, 36);

            // Color by kind
            Color fill = new Color(0.25f, 0.25f, 0.25f, 1f);
            if (n.Kind == NodeKind.Start) fill = new Color(0.25f, 0.45f, 0.25f, 1f);
            if (n.Kind == NodeKind.Goal) fill = new Color(0.45f, 0.35f, 0.25f, 1f);

            EditorGUI.DrawRect(r, fill);
            GUI.Box(r, GUIContent.none);

            string label = string.IsNullOrEmpty(n.DebugLabel) ? n.Id.ToString() : n.DebugLabel;

            // Append key info if present
            var keyTag = n.Tags.FirstOrDefault(t => t.Kind == NodeTagKind.Key);
            if (!keyTag.Equals(default(NodeTag)) && keyTag.Kind == NodeTagKind.Key)
            {
                label += $"\nKey {keyTag.Data}";
            }

            // Append lock hint if present
            bool lockHint = n.Tags.Any(t => t.Kind == NodeTagKind.LockHint);
            if (lockHint)
                label += "\n(LOCK)";

            GUI.Label(r, label, EditorStyles.centeredGreyMiniLabel);
        }
    }

    // -----------------------------
    // Layout
    // -----------------------------

    private void LayoutNested(GenerationResult result)
    {
        _nodePos.Clear();
        _nodeAngleRad.Clear();
        _nodeCircleCenter.Clear();
        _nodeCircleRadius.Clear();

        if (result == null || result.Graph == null)
            return;

        var graph = result.Graph;

        var insertedNodes = new HashSet<NodeId>();
        foreach (var rep in result.Replacements)
        {
            foreach (var nid in rep.InsertedNodeIds)
                insertedNodes.Add(nid);
        }

        // Big ring is only nodes that are not inside inserted sub-cycles
        var initial = graph.Nodes.Values.Select(n => n.Id).Where(id => !insertedNodes.Contains(id)).ToList();
        if (initial.Count == 0)
            initial = graph.Nodes.Values.Select(n => n.Id).ToList(); // fallback

        // Heuristic for MVP:
        // Treat "depth 0 ring" as the first nodes added (the overall fragment).
        // We can approximate by: nodes that are Start/Goal AND have the "Goal (Lock)" label when TwoKeys,
        // but that’s fragile. Better: use replacements to grow rings around attachment points.
        //
        // For now: choose an initial ring consisting of ALL nodes that currently have Kind Start/Goal,
        // then nested rings for inserted fragments will overwrite their positions.
        //
        // This works well because inserted fragments are a small number of nodes compared to full graph.

        // Big ring
        PlaceRing(initial, center: new Vector2(420f, 320f), radius: 220f);

        // Attach inserted rings near their seam midpoints.
        // Do shallow first so deeper rings can attach to already-positioned parents.
        foreach (var rep in result.Replacements.OrderBy(r => r.Insertion.Depth))
        {
            if (!_nodePos.TryGetValue(rep.ParentFrom, out var pA) || !_nodePos.TryGetValue(rep.ParentTo, out var pB))
                continue;

            // Parent seam midpoint
            var mid = (pA + pB) * 0.5f;

            // Push outward from the parent ring center to make it look "nested"
            // (like the PDF example: small cycles outside the main ring)
            var parentCenter = _nodeCircleCenter.TryGetValue(rep.ParentFrom, out var pc) ? pc : new Vector2(420f, 320f);
            var dir = (mid - parentCenter);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();

            float parentRadius = _nodeCircleRadius.TryGetValue(rep.ParentFrom, out var pr) ? pr : 220f;

            // Shrink per depth
            float childRadius = Mathf.Max(60f, parentRadius * Mathf.Pow(0.42f, rep.Insertion.Depth + 1));
            float offset = parentRadius * 0.55f;

            var childCenter = mid + dir * offset;

            PlaceRing(rep.InsertedNodeIds, childCenter, childRadius);
        }
    }

    private void PlaceRing(IReadOnlyList<NodeId> nodes, Vector2 center, float radius)
    {
        int count = nodes.Count;
        if (count == 0) return;

        // Deterministic ordering for stable layouts
        var sorted = nodes.OrderBy(n => n.Value).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            float t = (sorted.Count == 1) ? 0f : (i / (float)sorted.Count) * Mathf.PI * 2f;

            _nodeAngleRad[sorted[i]] = t;
            _nodeCircleCenter[sorted[i]] = center;
            _nodeCircleRadius[sorted[i]] = radius;

            _nodePos[sorted[i]] = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
        }
    }



    // -----------------------------
    // Helpers
    // -----------------------------
    private static Vector2 PointOnCircle(Vector2 center, float radius, float angleRad)
    {
        return center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
    }

    private static float WrapAngle0To2Pi(float a)
    {
        float twoPi = Mathf.PI * 2f;
        a %= twoPi;
        if (a < 0f) a += twoPi;
        return a;
    }

    private static float ShortestDeltaAngle(float from, float to)
    {
        float twoPi = Mathf.PI * 2f;
        float delta = WrapAngle0To2Pi(to) - WrapAngle0To2Pi(from);

        if (delta > Mathf.PI) delta -= twoPi;
        if (delta < -Mathf.PI) delta += twoPi;

        return delta; // [-pi, +pi]
    }

    private static float MidAngleAlongShortestArc(float from, float to)
    {
        float delta = ShortestDeltaAngle(from, to);
        return WrapAngle0To2Pi(from + delta * 0.5f);
    }

    private static void DrawCircularArc(Vector2 center, float radius, float fromAngle, float toAngle, int segments, float thickness)
    {
        float delta = ShortestDeltaAngle(fromAngle, toAngle);
        if (Mathf.Abs(delta) < 0.001f)
            return;

        var pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float ang = WrapAngle0To2Pi(fromAngle + delta * t);
            Vector2 p2 = PointOnCircle(center, radius, ang);
            pts[i] = new Vector3(p2.x, p2.y, 0f);
        }

        Handles.DrawAAPolyLine(thickness, pts);
    }

    private static void DrawArrowHead(Vector2 from, Vector2 to)
    {
        var dir = (to - from);
        if (dir.sqrMagnitude < 0.001f) return;

        dir.Normalize();

        // Put the arrow at the midpoint of the connector
        var mid = (from + to) * 0.5f;

        var perp = new Vector2(-dir.y, dir.x);
        float size = 7f;

        var p1 = mid - dir * size + perp * (size * 0.6f);
        var p2 = mid - dir * size - perp * (size * 0.6f);

        Handles.DrawAAConvexPolygon(mid, p1, p2);
    }

    private static void DrawArcArrowHead(Vector2 center, float radius, float fromAngle, float toAngle)
    {
        float delta = ShortestDeltaAngle(fromAngle, toAngle);
        if (Mathf.Abs(delta) < 0.001f)
            return;

        // Arrow near the "to" end but slightly backed off from the node
        float headT = 0.90f;
        float tailT = 0.82f;

        float angHead = WrapAngle0To2Pi(fromAngle + delta * headT);
        float angTail = WrapAngle0To2Pi(fromAngle + delta * tailT);

        Vector2 head = PointOnCircle(center, radius, angHead);
        Vector2 tail = PointOnCircle(center, radius, angTail);

        var dir = head - tail;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        var perp = new Vector2(-dir.y, dir.x);
        float size = 7f;

        var p1 = head - dir * size + perp * (size * 0.6f);
        var p2 = head - dir * size - perp * (size * 0.6f);

        Handles.DrawAAConvexPolygon(
            new Vector3(head.x, head.y, 0f),
            new Vector3(p1.x, p1.y, 0f),
            new Vector3(p2.x, p2.y, 0f));
    }


    /// <summary>
    /// Debug helper to force the overall cycle type in the editor.
    /// Sub-cycles still use DefaultCycleSelector.
    /// </summary>
    private sealed class ForcedOverallCycleSelector : ICycleSelector
    {
        private readonly CycleType _overall;

        public ForcedOverallCycleSelector(CycleType overall)
        {
            _overall = overall;
        }

        public CycleType SelectOverall(Random rng)
        {
            return _overall;
        }

        public CycleType SelectSub(Random rng, int depth)
        {
            // Sub-cycles still random
            return new DefaultCycleSelector().SelectSub(rng, depth);
        }
    }
}
#endif
