using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    public static class ProjectViewEnhancerSettingsProvider
    {
        [MenuItem("Tools/Project View Enhancer/Settings")]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/Project View Enhancer");
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Project View Enhancer", SettingsScope.Project)
            {
                label = "Project View Enhancer",
                guiHandler = _ => DrawGUI(),
                keywords = new HashSet<string>(new[]
                {
                    "Project",
                    "Guide",
                    "Color",
                    "Folder",
                    "Line",
                    "Selection",
                    "Project Window"
                })
            };
        }

        private static void DrawGUI()
        {
            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.HelpBox(
                "Project View Enhancer:\n" +
                "1. Draws indent guides in the Project window tree.\n" +
                "2. Supports alternating row backgrounds.\n" +
                "3. Supports per-folder manual styles.",
                MessageType.Info);

            settings.enabled = EditorGUILayout.Toggle("Enabled", settings.enabled);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Guides", EditorStyles.boldLabel);

            settings.showIndentGuides = EditorGUILayout.Toggle("Show Indent Guides", settings.showIndentGuides);
            EditorGUILayout.HelpBox(
                "Reflection-based tree guides align to Unity TreeView geometry. The two-column right pane does not use this guide drawing path.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(!settings.showIndentGuides))
            {
                settings.showHorizontalJoin = EditorGUILayout.Toggle("Show Horizontal Join", settings.showHorizontalJoin);
                settings.guideThickness = Mathf.Clamp(
                    EditorGUILayout.DelayedFloatField("Guide Thickness", settings.guideThickness),
                    1f,
                    4f);
                settings.defaultGuideAlpha = Mathf.Clamp01(
                    EditorGUILayout.Slider("Default Guide Alpha", settings.defaultGuideAlpha, 0.05f, 0.60f));

                settings.useCustomGuideColor = EditorGUILayout.Toggle("Use Custom Guide Color", settings.useCustomGuideColor);
                if (settings.useCustomGuideColor)
                    settings.guideColor = EditorGUILayout.ColorField("Guide Color", settings.guideColor);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!settings.showIndentGuides))
            {
                settings.highlightActiveSelectionPath = EditorGUILayout.Toggle("Highlight Selection Path", settings.highlightActiveSelectionPath);
                if (settings.highlightActiveSelectionPath)
                    settings.activeSelectionGuideColor = EditorGUILayout.ColorField("Selection Path Color", settings.activeSelectionGuideColor);
            }

            EditorGUILayout.HelpBox(
                "Selection path highlighting now only applies to the single-column Project tree. The two-column left pane keeps it disabled to avoid drag lag.",
                MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Row Background", EditorStyles.boldLabel);

            settings.enableAlternatingRowBackground = EditorGUILayout.Toggle("Enable Alternating Rows", settings.enableAlternatingRowBackground);
            if (settings.enableAlternatingRowBackground)
            {
                settings.alternatingRowEvenColor = EditorGUILayout.ColorField("Even Row Color", settings.alternatingRowEvenColor);
                settings.alternatingRowOddColor = EditorGUILayout.ColorField("Odd Row Color", settings.alternatingRowOddColor);
                EditorGUILayout.HelpBox(
                    "Use low alpha values so Unity selection and guide highlights remain visible.",
                    MessageType.None);
            }

            if (EditorGUI.EndChangeCheck())
                settings.SaveAndRepaint();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Reset To Defaults"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset Settings",
                        "Reset Project View Enhancer settings to defaults?",
                        "Reset",
                        "Cancel"))
                {
                    settings.ResetToDefaults();
                }
            }
        }
    }
}
