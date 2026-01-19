#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

using DunGen.Graph.Debug;
using DunGen.Graph.Generation;

namespace DunGen.Editor
{
    public sealed class GraphvizExportWindow : EditorWindow
    {
        // Generator settings (mirrors CyclicDungeonGenerator.Settings)
        [SerializeField] private int seed = 12345;
        [SerializeField] private int maxDepth = 3;
        [SerializeField] private int maxInsertionsTotal = 32;

        private string _lastDot;
        private string _lastSavedPath;

        [MenuItem("Tools/DunGen/Export Graphviz (.dot)...")]
        private static void Open()
        {
            var w = GetWindow<GraphvizExportWindow>("Graphviz Export");
            w.minSize = new Vector2(420, 220);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Generate + Export (Graphviz DOT)", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                seed = EditorGUILayout.IntField(new GUIContent("Seed"), seed);
                maxDepth = EditorGUILayout.IntField(new GUIContent("Max Depth"), maxDepth);
                maxInsertionsTotal = EditorGUILayout.IntField(new GUIContent("Max Insertions Total"), maxInsertionsTotal);
            }

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate DOT"))
                {
                    _lastDot = GenerateDot();
                    _lastSavedPath = null;
                }

                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_lastDot));
                if (GUILayout.Button("Copy DOT"))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastDot;
                    ShowNotification(new GUIContent("DOT copied to clipboard."));
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Generate & Save .dot..."))
            {
                var dot = GenerateDot();
                if (string.IsNullOrEmpty(dot))
                    return;

                var defaultName = $"dungen_{DateTime.Now:yyyyMMdd_HHmmss}.dot";
                var path = EditorUtility.SaveFilePanel(
                    "Save Graphviz DOT",
                    Application.dataPath,
                    defaultName,
                    "dot");

                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, dot);
                    _lastDot = dot;
                    _lastSavedPath = path;

                    Debug.Log($"[DunGen] Wrote Graphviz DOT: {path}");
                    ShowNotification(new GUIContent("Saved .dot file."));
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Last Export", EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(_lastSavedPath))
                    EditorGUILayout.SelectableLabel(_lastSavedPath, EditorStyles.textField, GUILayout.Height(18));

                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_lastDot));
                EditorGUILayout.LabelField("DOT Preview (first ~40 lines):");
                EditorGUI.EndDisabledGroup();

                if (!string.IsNullOrEmpty(_lastDot))
                {
                    var preview = FirstLines(_lastDot, 40);
                    EditorGUILayout.TextArea(preview, GUILayout.MinHeight(80));
                }
                else
                {
                    EditorGUILayout.LabelField("No DOT generated yet.");
                }
            }
        }

        private string GenerateDot()
        {
            try
            {
                var gen = GenerationBootstrap.CreateDefaultGenerator(); // :contentReference[oaicite:1]{index=1}

                var s = new CyclicDungeonGenerator.Settings
                {
                    Seed = seed,
                    MaxDepth = maxDepth,
                    MaxInsertionsTotal = maxInsertionsTotal
                };

                var result = gen.Generate(s);
                return GraphvizExporter.Export(result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[DunGen] Graphviz export failed:\n" + ex);
                return null;
            }
        }

        private static string FirstLines(string text, int maxLines)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            int lines = 0;
            int i = 0;

            for (; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                    if (lines >= maxLines)
                    {
                        i++; // include newline
                        break;
                    }
                }
            }

            if (i <= 0) return "";
            return text.Substring(0, Mathf.Min(i, text.Length));
        }
    }
}
#endif
