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

/// <summary>
/// Complete editor with full information display:
/// - Stats panel with counts and metadata
/// - Edge visualization (one-way, barriers, key requirements)
/// - Expandable replacement details
/// - Visual legend
/// </summary>
public sealed class DungeonGraphEditorWindow : EditorWindow
{
    private int _seed = 12345;
    private int _maxDepth = 3;
    private int _maxInsertions = 32;
    private bool _forceOverall;
    private CycleType _forcedOverallType = CycleType.TwoAlternativePaths;
    private bool _randomSeedOnGenerate = false;
    private GenerationResult _result;

    // UI state
    private bool _showStats = true;
    private bool _showLegend = true;
    private Vector2 _statsScroll;
    private Dictionary<InsertionReplacement, bool> _replacementFoldouts = new Dictionary<InsertionReplacement, bool>();

    private readonly Dictionary<NodeId, Vector2> _nodePos = new Dictionary<NodeId, Vector2>();
    private readonly Dictionary<NodeId, float> _nodeAngleRad = new Dictionary<NodeId, float>();
    private readonly Dictionary<NodeId, Vector2> _nodeCircleCenter = new Dictionary<NodeId, Vector2>();
    private readonly Dictionary<NodeId, float> _nodeCircleRadius = new Dictionary<NodeId, float>();

    private Vector2 _pan = Vector2.zero;
    private float _zoom = 1f;
    private bool _isPanning = false;
    private Vector2 _lastMousePos;

    [MenuItem("Tools/DunGen/Dungeon Graph Viewer")]
    public static void Open()
    {
        GetWindow<DungeonGraphEditorWindow>("Dungeon Graph");
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        // Main graph view
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        HandlePanZoom(rect);
        DrawGraph(rect, _result);
        EditorGUILayout.EndVertical();

        // Side panel
        if (_result != null)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawSidePanel();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Randomize Seed", GUILayout.Width(120)))
                    _seed = new Random().Next();

                _seed = EditorGUILayout.IntField("Seed", _seed);
                _maxDepth = EditorGUILayout.IntField("Max Depth", _maxDepth);
                _maxInsertions = EditorGUILayout.IntField("Max Insertions", _maxInsertions);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Reset View", GUILayout.Width(100)))
                {
                    _pan = Vector2.zero;
                    _zoom = 1f;
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _forceOverall = EditorGUILayout.ToggleLeft("Force Overall Type", _forceOverall, GUILayout.Width(150));
                using (new EditorGUI.DisabledScope(!_forceOverall))
                {
                    _forcedOverallType = (CycleType)EditorGUILayout.EnumPopup(_forcedOverallType);
                }

                GUILayout.FlexibleSpace();

                _randomSeedOnGenerate = EditorGUILayout.ToggleLeft("Random Seed", _randomSeedOnGenerate, GUILayout.Width(100));

                if (GUILayout.Button("Generate", GUILayout.Width(120)))
                {
                    if (_randomSeedOnGenerate)
                        _seed = new Random().Next();
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
                    _replacementFoldouts.Clear();
                    Repaint();
                }
            }
        }
    }

    private void DrawSidePanel()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_statsScroll))
        {
            _statsScroll = scroll.scrollPosition;

            // Stats section
            _showStats = EditorGUILayout.Foldout(_showStats, "Statistics", true, EditorStyles.foldoutHeader);
            if (_showStats)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawStatistics();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // Legend section
            _showLegend = EditorGUILayout.Foldout(_showLegend, "Legend", true, EditorStyles.foldoutHeader);
            if (_showLegend)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawLegend();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // Replacements section
            if (_result.Replacements.Count > 0)
            {
                EditorGUILayout.LabelField("Cycle Replacements", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                foreach (var rep in _result.Replacements.OrderBy(r => r.Insertion.Depth).ThenBy(r => r.ParentFrom.Value))
                {
                    DrawReplacementDetail(rep);
                }
            }
        }
    }

    private void DrawStatistics()
    {
        var graph = _result.Graph;

        EditorGUILayout.LabelField("Graph Statistics", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Generation settings
        EditorGUILayout.LabelField("Generation:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"  Seed: {_seed}");
        EditorGUILayout.LabelField($"  Max Depth: {_maxDepth}");
        EditorGUILayout.LabelField($"  Max Insertions: {_maxInsertions}");
        EditorGUILayout.LabelField($"  Actual Insertions: {_result.Replacements.Count}");

        int actualMaxDepth = _result.Replacements.Count > 0
            ? _result.Replacements.Max(r => r.Insertion.Depth) + 1
            : 0;
        EditorGUILayout.LabelField($"  Actual Max Depth: {actualMaxDepth}");

        EditorGUILayout.Space();

        // Node counts
        EditorGUILayout.LabelField("Nodes:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"  Total: {graph.Nodes.Count}");

        int startCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.Start);
        int goalCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.Goal);
        int normalCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.Normal);

        EditorGUILayout.LabelField($"  Start: {startCount}");
        EditorGUILayout.LabelField($"  Goal: {goalCount}");
        EditorGUILayout.LabelField($"  Normal: {normalCount}");

        int keyCount = CountNodesWithTag(NodeTagKind.Key);
        int lockHintCount = CountNodesWithTag(NodeTagKind.LockHint);

        EditorGUILayout.LabelField($"  With Keys: {keyCount}");
        EditorGUILayout.LabelField($"  With Lock Hints: {lockHintCount}");

        EditorGUILayout.Space();

        // Edge counts
        EditorGUILayout.LabelField("Edges:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"  Total: {graph.Edges.Count}");

        int lockedEdges = 0;
        int oneWayEdges = 0;
        int barrierEdges = 0;

        foreach (var edge in graph.Edges.Values)
        {
            if (edge.Gate != null && (edge.Gate.RequiredKey.HasValue ||
                (edge.Gate.RequiredKeys != null && edge.Gate.RequiredKeys.Count > 0)))
                lockedEdges++;

            if (edge.Traversal == EdgeTraversal.OneWay)
                oneWayEdges++;

            if (edge.Gate != null && edge.Gate.Kind == GateKind.Barrier)
                barrierEdges++;
        }

        EditorGUILayout.LabelField($"  Locked: {lockedEdges}");
        EditorGUILayout.LabelField($"  One-Way: {oneWayEdges}");
        EditorGUILayout.LabelField($"  With Barriers: {barrierEdges}");

        EditorGUILayout.Space();

        // Cycle type breakdown
        EditorGUILayout.LabelField("Cycle Types:", EditorStyles.miniBoldLabel);
        var typeCounts = new Dictionary<CycleType, int>();

        foreach (var rep in _result.Replacements)
        {
            if (!typeCounts.ContainsKey(rep.InsertedType))
                typeCounts[rep.InsertedType] = 0;
            typeCounts[rep.InsertedType]++;
        }

        foreach (var kvp in typeCounts.OrderBy(k => k.Key))
        {
            EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value}");
        }
    }

    private int CountNodesWithTag(NodeTagKind kind)
    {
        int count = 0;
        foreach (var node in _result.Graph.Nodes.Values)
        {
            foreach (var tag in node.Tags)
            {
                if (tag.Kind == kind)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private void DrawLegend()
    {
        EditorGUILayout.LabelField("Visual Legend", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Node colors
        EditorGUILayout.LabelField("Nodes:", EditorStyles.miniBoldLabel);
        DrawLegendItem(new Color(0.25f, 0.45f, 0.25f), "Start Node");
        DrawLegendItem(new Color(0.45f, 0.35f, 0.25f), "Goal Node");
        DrawLegendItem(new Color(0.25f, 0.25f, 0.25f), "Normal Node");
        EditorGUILayout.Space();

        // Edge colors
        EditorGUILayout.LabelField("Edges:", EditorStyles.miniBoldLabel);
        DrawLegendItem(new Color(0.85f, 0.85f, 0.85f), "Normal Edge");
        DrawLegendItem(new Color(1f, 0.35f, 0.35f), "Locked Edge (requires key)");
        DrawLegendItem(new Color(0.35f, 0.8f, 1f), "One-Way Edge");
        DrawLegendItem(new Color(1f, 0.5f, 0f), "Barrier Edge");
        EditorGUILayout.Space();

        // View controls
        EditorGUILayout.LabelField("Controls:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  MMB / Alt+LMB: Pan");
        EditorGUILayout.LabelField("  Mouse Wheel: Zoom");
        EditorGUILayout.LabelField("  Reset View: Center graph");
    }

    private void DrawLegendItem(Color color, string label)
    {
        EditorGUILayout.BeginHorizontal();

        Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
        EditorGUI.DrawRect(colorRect, color);
        GUI.Box(colorRect, GUIContent.none);

        EditorGUILayout.LabelField(label);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawReplacementDetail(InsertionReplacement rep)
    {
        if (!_replacementFoldouts.ContainsKey(rep))
            _replacementFoldouts[rep] = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Foldout header
        string header = $"[Depth {rep.Insertion.Depth}] {rep.InsertedType}";
        _replacementFoldouts[rep] = EditorGUILayout.Foldout(_replacementFoldouts[rep], header, true);

        if (_replacementFoldouts[rep])
        {
            EditorGUI.indentLevel++;

            // Basic info
            EditorGUILayout.LabelField("Cycle Type:", rep.InsertedType.ToString());
            EditorGUILayout.LabelField("Depth:", rep.Insertion.Depth.ToString());
            EditorGUILayout.LabelField("Nodes:", rep.InsertedNodeIds.Count.ToString());

            EditorGUILayout.Space();

            // Parent seam info
            EditorGUILayout.LabelField("Parent Seam:", EditorStyles.miniBoldLabel);

            var graph = _result.Graph;
            var fromNode = graph.GetNode(rep.ParentFrom);
            var toNode = graph.GetNode(rep.ParentTo);

            string fromLabel = fromNode.DebugLabel ?? rep.ParentFrom.ToString();
            string toLabel = toNode.DebugLabel ?? rep.ParentTo.ToString();

            EditorGUILayout.LabelField($"  From: {fromLabel}");
            EditorGUILayout.LabelField($"  To: {toLabel}");
            EditorGUILayout.LabelField($"  Seam Edge: {rep.Insertion.SeamEdge}");

            EditorGUILayout.Space();

            // Fragment info
            EditorGUILayout.LabelField("Inserted Fragment:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"  Entry: {rep.Inserted.Entry}");
            EditorGUILayout.LabelField($"  Exit: {rep.Inserted.Exit}");

            EditorGUILayout.Space();

            // Key nodes
            var keyNodes = new List<(NodeId id, int keyIndex)>();
            foreach (var nodeId in rep.InsertedNodeIds)
            {
                var node = graph.GetNode(nodeId);
                foreach (var tag in node.Tags)
                {
                    if (tag.Kind == NodeTagKind.Key)
                    {
                        keyNodes.Add((nodeId, tag.Data));
                        break;
                    }
                }
            }

            if (keyNodes.Count > 0)
            {
                EditorGUILayout.LabelField("Keys in Fragment:", EditorStyles.miniBoldLabel);
                foreach (var (id, keyIndex) in keyNodes.OrderBy(k => k.keyIndex))
                {
                    var node = graph.GetNode(id);
                    string nodeLabel = node.DebugLabel ?? id.ToString();
                    EditorGUILayout.LabelField($"  Key {keyIndex}: {nodeLabel}");
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void Generate()
    {
        var templates = new CycleTemplateLibrary();
        BuiltInTemplates.Register(templates);

        var ids = new IdAllocator();
        var rewriter = new GraphRewriteEngine(ids);

        ICycleSelector selector = _forceOverall
            ? new ForcedOverallCycleSelector(_forcedOverallType)
            : (ICycleSelector)new DefaultCycleSelector();

        var rules = new CycleRuleRegistry();
        rules.Register(new TwoKeysRule());

        var gen = new CyclicDungeonGenerator(templates, selector, rewriter, rules);

        _result = gen.Generate(new CyclicDungeonGenerator.Settings
        {
            Seed = _seed,
            MaxDepth = _maxDepth,
            MaxInsertionsTotal = _maxInsertions
        });

        _replacementFoldouts.Clear();
        LayoutNested(_result);
    }

    private void HandlePanZoom(Rect viewRect)
    {
        var e = Event.current;
        if (!viewRect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.MouseDown && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            _isPanning = true;
            _lastMousePos = e.mousePosition;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            _isPanning = false;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && _isPanning)
        {
            _pan += e.mousePosition - _lastMousePos;
            _lastMousePos = e.mousePosition;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.ScrollWheel)
        {
            float oldZoom = _zoom;
            float zoomFactor = Mathf.Exp(-e.delta.y * 0.03f);
            _zoom = Mathf.Clamp(_zoom * zoomFactor, 0.25f, 3.0f);

            Vector2 mouseInView = e.mousePosition - viewRect.position;
            Vector2 worldBefore = (mouseInView - _pan) / oldZoom;
            _pan = mouseInView - worldBefore * _zoom;

            e.Use();
            Repaint();
        }
    }

    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        return worldPos * _zoom + _pan;
    }

    private void DrawGraph(Rect rect, GenerationResult result)
    {
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));

        if (result == null || result.Graph == null)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.5f - 10, rect.width, 20),
                "No graph generated yet. Click 'Generate'.", style);
            return;
        }

        GUI.BeginClip(rect);

        // Draw schematic view 
        DrawSchematicView(result);

        GUI.EndClip();

        var hint = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        hint.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        GUI.Label(new Rect(rect.x + 10, rect.yMax - 22, rect.width - 20, 18),
            "Pan: MMB or Alt+LMB   Zoom: Scroll Wheel", hint);
    }

    private void DrawKeyRequirements(Vector2 screenPos, EdgeGate gate)
    {
        if (gate == null) return;

        string keyText = "";

        if (gate.RequiredKey.HasValue)
        {
            keyText = gate.RequiredKey.Value.ToString();
        }
        else if (gate.RequiredKeys != null && gate.RequiredKeys.Count > 0)
        {
            keyText = string.Join(",", gate.RequiredKeys.Select(k => k.ToString()));
        }

        if (!string.IsNullOrEmpty(keyText))
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(screenPos.x - 15, screenPos.y + 6, 30, 14), keyText, style);
        }
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, float thickness)
    {
        Vector2 dir = to - from;
        float length = dir.magnitude;
        dir.Normalize();

        float dashLength = 8f;
        float gapLength = 4f;
        float traveled = 0f;

        while (traveled < length)
        {
            float segmentLength = Mathf.Min(dashLength, length - traveled);
            Vector2 segmentStart = from + dir * traveled;
            Vector2 segmentEnd = from + dir * (traveled + segmentLength);

            Handles.DrawAAPolyLine(thickness, segmentStart, segmentEnd);

            traveled += dashLength + gapLength;
        }
    }

    private void DrawDashedCircularArc(Vector2 screenCenter, float screenRadius, float fromAngle, float toAngle, int segments, float thickness)
    {
        float delta = ShortestDeltaAngle(fromAngle, toAngle);
        if (Mathf.Abs(delta) < 0.001f)
            return;

        float dashArcLength = 0.1f; // radians
        float gapArcLength = 0.05f;
        float traveled = 0f;

        while (traveled < Mathf.Abs(delta))
        {
            float segmentLength = Mathf.Min(dashArcLength, Mathf.Abs(delta) - traveled);

            float startAngle = fromAngle + Mathf.Sign(delta) * traveled;
            float endAngle = fromAngle + Mathf.Sign(delta) * (traveled + segmentLength);

            int segSegs = Mathf.Max(2, (int)(segments * segmentLength / Mathf.Abs(delta)));
            DrawCircularArc(screenCenter, screenRadius, startAngle, endAngle, segSegs, thickness);

            traveled += dashArcLength + gapArcLength;
        }
    }

    private void DrawRuntimeNode(Vector2 screenPos, RoomNode node)
    {
        var r = new Rect(screenPos.x - 35, screenPos.y - 18, 70, 36);

        Color fill = new Color(0.25f, 0.25f, 0.25f, 1f);
        if (node.Kind == NodeKind.Start) fill = new Color(0.25f, 0.45f, 0.25f, 1f);
        if (node.Kind == NodeKind.Goal) fill = new Color(0.45f, 0.35f, 0.25f, 1f);

        EditorGUI.DrawRect(r, fill);
        GUI.Box(r, GUIContent.none);

        string label = string.IsNullOrEmpty(node.DebugLabel) ? node.Id.ToString() : node.DebugLabel;

        foreach (var tag in node.Tags)
        {
            if (tag.Kind == NodeTagKind.Key)
            {
                label += $"\nKey {tag.Data}";
                break;
            }
        }

        foreach (var tag in node.Tags)
        {
            if (tag.Kind == NodeTagKind.LockHint)
            {
                label += "\n(LOCK)";
                break;
            }
        }

        GUI.Label(r, label, EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawSchematicView(GenerationResult result)
    {
        FindOuterStartGoal(result, out var overallStart, out var overallGoal);
        var overallType = DetermineOverallType(result.Graph, overallGoal);
        var root = BuildRingTree(result, overallStart, overallGoal, overallType);
        DrawRingRecursive(root, Vector2.zero, 220f);
    }

    private void LayoutNested(GenerationResult result)
    {
        _nodePos.Clear();
        _nodeAngleRad.Clear();
        _nodeCircleCenter.Clear();
        _nodeCircleRadius.Clear();

        if (result == null || result.Graph == null)
            return;

        var insertedNodes = new HashSet<NodeId>();
        foreach (var rep in result.Replacements)
            foreach (var nid in rep.InsertedNodeIds)
                insertedNodes.Add(nid);

        var initialNodes = new List<NodeId>();
        foreach (var n in result.Graph.Nodes.Values)
            if (!insertedNodes.Contains(n.Id))
                initialNodes.Add(n.Id);

        if (initialNodes.Count == 0)
            foreach (var n in result.Graph.Nodes.Values)
                initialNodes.Add(n.Id);

        PlaceRing(initialNodes, Vector2.zero, 220f);

        foreach (var rep in result.Replacements.OrderBy(r => r.Insertion.Depth))
        {
            if (!_nodePos.TryGetValue(rep.ParentFrom, out var posA) ||
                !_nodePos.TryGetValue(rep.ParentTo, out var posB))
                continue;

            var midpoint = (posA + posB) * 0.5f;
            var parentCenter = _nodeCircleCenter.TryGetValue(rep.ParentFrom, out var pc) ? pc : Vector2.zero;

            var direction = (midpoint - parentCenter);
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
            direction.Normalize();

            float parentRadius = _nodeCircleRadius.TryGetValue(rep.ParentFrom, out var pr) ? pr : 220f;
            float childRadius = Mathf.Max(60f, parentRadius * Mathf.Pow(0.42f, rep.Insertion.Depth + 1));
            float offset = parentRadius * 0.55f;

            PlaceRing(rep.InsertedNodeIds, midpoint + direction * offset, childRadius);
        }
    }

    private void PlaceRing(IReadOnlyList<NodeId> nodes, Vector2 center, float radius)
    {
        if (nodes == null || nodes.Count == 0) return;

        var sorted = nodes.OrderBy(n => n.Value).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            float angle = (sorted.Count == 1) ? 0f : (i / (float)sorted.Count) * Mathf.PI * 2f;

            _nodeAngleRad[sorted[i]] = angle;
            _nodeCircleCenter[sorted[i]] = center;
            _nodeCircleRadius[sorted[i]] = radius;
            _nodePos[sorted[i]] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }

    private sealed class Ring
    {
        public CycleType Type;
        public NodeId Start, Goal;
        public int Depth;
        public string UpperLabel, LowerLabel;
        public bool GoalLocked;
        public Ring UpperChild, LowerChild;

        public Ring(CycleType type, NodeId start, NodeId goal, int depth)
        {
            Type = type;
            Start = start;
            Goal = goal;
            Depth = depth;
        }
    }

    private static CycleType DetermineOverallType(DungeonGraph graph, NodeId overallGoal)
    {
        var goalNode = graph.GetNode(overallGoal);

        foreach (var tag in goalNode.Tags)
            if (tag.Kind == NodeTagKind.LockHint)
                return CycleType.TwoKeys;

        foreach (var e in graph.Edges.Values)
            if (e.To == overallGoal && e.Gate != null && e.Gate.IsMultiKey)
                return CycleType.TwoKeys;

        return CycleType.TwoAlternativePaths;
    }

    private static void FindOuterStartGoal(GenerationResult result, out NodeId start, out NodeId goal)
    {
        start = default;
        goal = default;

        var inserted = new HashSet<NodeId>();
        foreach (var rep in result.Replacements)
            foreach (var nid in rep.InsertedNodeIds)
                inserted.Add(nid);

        foreach (var n in result.Graph.Nodes.Values)
        {
            if (inserted.Contains(n.Id)) continue;
            if (n.Kind == NodeKind.Start) start = n.Id;
            if (n.Kind == NodeKind.Goal) goal = n.Id;
        }

        if (start.Equals(default(NodeId)) || goal.Equals(default(NodeId)))
        {
            foreach (var n in result.Graph.Nodes.Values)
            {
                if (n.Kind == NodeKind.Start) start = n.Id;
                if (n.Kind == NodeKind.Goal) goal = n.Id;
            }
        }
    }

    private Ring BuildRingTree(GenerationResult result, NodeId overallStart, NodeId overallGoal, CycleType overallType)
    {
        var root = new Ring(overallType, overallStart, overallGoal, 0);

        if (overallType == CycleType.TwoKeys)
        {
            root.UpperLabel = "KEY 1";
            root.LowerLabel = "KEY 2";
            root.GoalLocked = true;
        }
        else
        {
            root.UpperLabel = "THEME 1";
            root.LowerLabel = "THEME 2";
            root.GoalLocked = false;
        }

        var depth0 = result.Replacements.Where(r => r.Insertion.Depth == 0).ToList();

        if (overallType == CycleType.TwoKeys)
        {
            foreach (var rep in depth0)
            {
                int keyIndex = FindKeyIndexForReplacement(result.Graph, rep);
                if (keyIndex == 1) root.UpperChild = MakeChildRing(rep, 1);
                if (keyIndex == 2) root.LowerChild = MakeChildRing(rep, 1);
            }
        }
        else
        {
            depth0 = depth0.OrderBy(r => r.ParentFrom.Value).ThenBy(r => r.ParentTo.Value).ToList();
            if (depth0.Count > 0) root.UpperChild = MakeChildRing(depth0[0], 1);
            if (depth0.Count > 1) root.LowerChild = MakeChildRing(depth0[1], 1);
        }

        return root;
    }

    private static Ring MakeChildRing(InsertionReplacement rep, int depth)
    {
        var r = new Ring(rep.InsertedType, rep.Inserted.Entry, rep.Inserted.Exit, depth);

        if (rep.InsertedType == CycleType.TwoKeys)
        {
            r.UpperLabel = "KEY 1";
            r.LowerLabel = "KEY 2";
            r.GoalLocked = true;
        }
        else
        {
            r.UpperLabel = "THEME 1";
            r.LowerLabel = "THEME 2";
            r.GoalLocked = false;
        }

        return r;
    }

    private static int FindKeyIndexForReplacement(DungeonGraph graph, InsertionReplacement rep)
    {
        foreach (var nid in rep.InsertedNodeIds)
        {
            var n = graph.GetNode(nid);
            foreach (var tag in n.Tags)
                if (tag.Kind == NodeTagKind.Key)
                    return tag.Data;
        }
        return 0;
    }

    private void DrawRingRecursive(Ring ring, Vector2 worldCenter, float worldRadius)
    {
        DrawRing(ring, worldCenter, worldRadius);

        var topDiamond = worldCenter + new Vector2(0f, -worldRadius);
        var botDiamond = worldCenter + new Vector2(0f, worldRadius);

        float childRadius = Mathf.Max(55f, worldRadius * 0.42f);
        float gap = childRadius * 1.15f;

        if (ring.UpperChild != null)
        {
            var childCenter = topDiamond + new Vector2(0f, -gap);
            DrawConnector(topDiamond, childCenter + new Vector2(0f, childRadius));
            DrawRingRecursive(ring.UpperChild, childCenter, childRadius);
        }

        if (ring.LowerChild != null)
        {
            var childCenter = botDiamond + new Vector2(0f, +gap);
            DrawConnector(botDiamond, childCenter + new Vector2(0f, -childRadius));
            DrawRingRecursive(ring.LowerChild, childCenter, childRadius);
        }
    }

    private void DrawRing(Ring ring, Vector2 worldCenter, float worldRadius)
    {
        Vector2 screenCenter = WorldToScreen(worldCenter);
        float screenRadius = worldRadius * _zoom;

        Handles.BeginGUI();

        Handles.color = Color.white;
        Handles.DrawWireDisc(screenCenter, Vector3.forward, screenRadius, 3f);

        DrawInsertionDiamond(WorldToScreen(worldCenter + new Vector2(0f, -worldRadius)));
        DrawInsertionDiamond(WorldToScreen(worldCenter + new Vector2(0f, worldRadius)));

        DrawFlowNode(WorldToScreen(worldCenter + new Vector2(-worldRadius, 0f)), "START", false, ring.Depth);
        DrawFlowNode(WorldToScreen(worldCenter + new Vector2(worldRadius, 0f)), ring.GoalLocked ? "GOAL\n(LOCK)" : "GOAL", true, ring.Depth);

        Handles.EndGUI();

        DrawCenteredLabel(WorldToScreen(worldCenter + new Vector2(0f, -worldRadius * 0.35f)), ring.UpperLabel);
        DrawCenteredLabel(WorldToScreen(worldCenter + new Vector2(0f, worldRadius * 0.35f)), ring.LowerLabel);
    }

    private void DrawConnector(Vector2 worldA, Vector2 worldB)
    {
        Handles.BeginGUI();
        Handles.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        Handles.DrawAAPolyLine(3f, WorldToScreen(worldA), WorldToScreen(worldB));
        Handles.EndGUI();
    }

    private static void DrawCenteredLabel(Vector2 screenPos, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var st = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(screenPos.x - 90, screenPos.y - 10, 180, 20), text, st);
    }

    private static void DrawFlowNode(Vector2 screenPos, string text, bool isGoal, int depth)
    {
        float size = Mathf.Max(28f, 52f - depth * 6f);

        var fill = isGoal
            ? new Color(0.45f, 0.35f, 0.25f, 1f)
            : new Color(0.25f, 0.45f, 0.25f, 1f);

        Handles.color = fill;
        Handles.DrawSolidDisc(screenPos, Vector3.forward, size * 0.5f);

        Handles.color = Color.white;
        Handles.DrawWireDisc(screenPos, Vector3.forward, size * 0.5f, 2f);

        var st = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            fontSize = (text.Length > 6) ? 10 : 12
        };

        GUI.Label(new Rect(screenPos.x - size * 0.5f, screenPos.y - size * 0.35f, size, size * 0.7f), text, st);
    }

    private static void DrawInsertionDiamond(Vector2 screenPos)
    {
        float size = 10f;

        Vector3 p0 = screenPos + new Vector2(0, -size);
        Vector3 p1 = screenPos + new Vector2(size, 0);
        Vector3 p2 = screenPos + new Vector2(0, size);
        Vector3 p3 = screenPos + new Vector2(-size, 0);

        Handles.color = Color.black;
        Handles.DrawSolidDisc(screenPos, Vector3.forward, size * 1.2f);

        Handles.color = new Color(1f, 0.8f, 0f);
        Handles.DrawAAConvexPolygon(p0, p1, p2, p3);

        Handles.color = Color.white;
        Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
    }

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

        return delta;
    }

    private static float MidAngleAlongShortestArc(float from, float to)
    {
        float delta = ShortestDeltaAngle(from, to);
        return WrapAngle0To2Pi(from + delta * 0.5f);
    }

    private static void DrawCircularArc(Vector2 screenCenter, float screenRadius, float fromAngle, float toAngle, int segments, float thickness)
    {
        float delta = ShortestDeltaAngle(fromAngle, toAngle);
        if (Mathf.Abs(delta) < 0.001f)
            return;

        var pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float ang = WrapAngle0To2Pi(fromAngle + delta * t);
            Vector2 p2 = PointOnCircle(screenCenter, screenRadius, ang);
            pts[i] = new Vector3(p2.x, p2.y, 0f);
        }

        Handles.DrawAAPolyLine(thickness, pts);
    }

    private static void DrawArrowHead(Vector2 screenFrom, Vector2 screenTo)
    {
        var dir = (screenTo - screenFrom);
        if (dir.sqrMagnitude < 0.001f) return;

        dir.Normalize();

        var mid = (screenFrom + screenTo) * 0.5f;
        var perp = new Vector2(-dir.y, dir.x);
        float size = 7f;

        var p1 = mid - dir * size + perp * (size * 0.6f);
        var p2 = mid - dir * size - perp * (size * 0.6f);

        Handles.DrawAAConvexPolygon(mid, p1, p2);
    }

    private static void DrawArcArrowHead(Vector2 screenCenter, float screenRadius, float fromAngle, float toAngle)
    {
        float delta = ShortestDeltaAngle(fromAngle, toAngle);
        if (Mathf.Abs(delta) < 0.001f)
            return;

        float headT = 0.90f;
        float tailT = 0.82f;

        float angHead = WrapAngle0To2Pi(fromAngle + delta * headT);
        float angTail = WrapAngle0To2Pi(fromAngle + delta * tailT);

        Vector2 head = PointOnCircle(screenCenter, screenRadius, angHead);
        Vector2 tail = PointOnCircle(screenCenter, screenRadius, angTail);

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

    private sealed class ForcedOverallCycleSelector : ICycleSelector
    {
        private readonly CycleType _overall;

        public ForcedOverallCycleSelector(CycleType overall)
        {
            _overall = overall;
        }

        public CycleType SelectOverall(Random rng) => _overall;
        public CycleType SelectSub(Random rng, int depth) => new DefaultCycleSelector().SelectSub(rng, depth);
    }
}
#endif