using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [InitializeOnLoad]
    public static class ProjectViewEnhancerTwoColumnListOverlay
    {
        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const float ListIconWidth = 16f;
        private const float SpaceBetweenIconAndText = 2f;

        private static readonly Dictionary<ListPaneStyleCacheKey, GUIStyle> s_listPaneStyleCache = new();
        private static readonly Type s_objectListAreaType = Type.GetType("UnityEditor.ObjectListArea, UnityEditor");
        private static readonly Type s_objectListAreaStylesType = s_objectListAreaType?.GetNestedType("Styles", StaticBindingFlags);
        private static readonly FieldInfo s_resultsLabelField =
            s_objectListAreaStylesType?.GetField("resultsLabel", StaticBindingFlags);
        private static readonly FieldInfo s_versionControlEnabledField =
            s_objectListAreaType?.GetField("s_VCEnabled", StaticBindingFlags);

        static ProjectViewEnhancerTwoColumnListOverlay()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;

            EditorApplication.projectChanged -= ClearState;
            EditorApplication.projectChanged += ClearState;

            Undo.undoRedoPerformed -= ClearState;
            Undo.undoRedoPerformed += ClearState;
        }

        private static void ClearState()
        {
            s_listPaneStyleCache.Clear();
            ProjectViewEnhancerProjectBrowserMode.Invalidate();
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            // Right pane list rendering is handled by the ObjectListArea reflection patch.
        }

        private static bool IsListPaneClip(Rect clipRect, Rect treeViewRect, Rect listAreaRect)
        {
            if (!OverlapsVertically(clipRect, listAreaRect))
                return false;

            float listOverlap = GetHorizontalOverlap(clipRect, listAreaRect);
            if (listOverlap <= 0.5f)
                return false;

            float treeOverlap = GetHorizontalOverlap(clipRect, treeViewRect);
            return listOverlap >= treeOverlap;
        }

        private static float GetHorizontalOverlap(Rect a, Rect b)
        {
            float min = Mathf.Max(a.xMin, b.xMin);
            float max = Mathf.Min(a.xMax, b.xMax);
            return Mathf.Max(0f, max - min);
        }

        private static bool OverlapsVertically(Rect a, Rect b)
        {
            return a.yMax > b.yMin && a.yMin < b.yMax;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace("\\", "/").TrimEnd('/');
        }

        private static void DrawListPaneFolderOverlay(
            Rect selectionRect,
            string folderPath,
            ProjectViewEnhancerVisualState visualState)
        {
            bool hasIconColor = visualState.UseIconColor && visualState.IconColor.a > 0.001f;
            bool hasNameStyle = visualState.UseNameColor || visualState.UseNameFontStyle;
            if (!hasIconColor && !hasNameStyle)
                return;

            GUIStyle baseStyle = s_resultsLabelField?.GetValue(null) as GUIStyle ?? EditorStyles.label;
            float versionControlPadding = GetVersionControlPadding();
            Rect iconRect = selectionRect;
            iconRect.width = ListIconWidth;
            iconRect.x += versionControlPadding * 0.5f;

            if (hasIconColor)
            {
                Texture iconTexture = AssetDatabase.GetCachedIcon(folderPath);
                if (iconTexture != null)
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit, true, 0f, visualState.IconColor, 0f, 0f);
            }

            if (!hasNameStyle)
                return;

            Rect labelRect = GetLabelRect(selectionRect, baseStyle, versionControlPadding);
            GUIStyle style = GetListPaneLabelStyle(baseStyle, visualState);
            style.Draw(labelRect, Path.GetFileName(folderPath), false, false, false, false);
        }

        private static Rect GetLabelRect(Rect selectionRect, GUIStyle baseStyle, float versionControlPadding)
        {
            Rect labelRect = selectionRect;
            labelRect.xMin += baseStyle.margin.left + versionControlPadding + ListIconWidth + SpaceBetweenIconAndText;
            labelRect.xMax -= baseStyle.margin.right;
            return labelRect;
        }

        private static GUIStyle GetListPaneLabelStyle(
            GUIStyle baseStyle,
            ProjectViewEnhancerVisualState visualState)
        {
            ListPaneStyleCacheKey key = new(
                visualState.UseNameColor,
                visualState.NameColor,
                visualState.UseNameFontStyle,
                visualState.NameFontStyle);
            if (s_listPaneStyleCache.TryGetValue(key, out GUIStyle cachedStyle))
                return cachedStyle;

            GUIStyle style = new(baseStyle)
            {
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            style.padding.left = 0;
            style.padding.right = Mathf.Max(0, style.padding.right);

            if (visualState.UseNameFontStyle)
                style.fontStyle = visualState.NameFontStyle;

            if (visualState.UseNameColor)
                ApplyTextColor(style, visualState.NameColor);

            s_listPaneStyleCache[key] = style;
            return style;
        }

        private static void ApplyTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static float GetVersionControlPadding()
        {
            return s_versionControlEnabledField != null && Convert.ToBoolean(s_versionControlEnabledField.GetValue(null))
                ? 14f
                : 0f;
        }

        private readonly struct ListPaneStyleCacheKey : IEquatable<ListPaneStyleCacheKey>
        {
            public ListPaneStyleCacheKey(bool useNameColor, Color nameColor, bool useNameFontStyle, FontStyle nameFontStyle)
            {
                UseNameColor = useNameColor;
                NameColor = (Color32)nameColor;
                UseNameFontStyle = useNameFontStyle;
                NameFontStyle = nameFontStyle;
            }

            public bool UseNameColor { get; }
            public Color32 NameColor { get; }
            public bool UseNameFontStyle { get; }
            public FontStyle NameFontStyle { get; }

            public bool Equals(ListPaneStyleCacheKey other)
            {
                return UseNameColor == other.UseNameColor
                    && NameColor.Equals(other.NameColor)
                    && UseNameFontStyle == other.UseNameFontStyle
                    && NameFontStyle == other.NameFontStyle;
            }

            public override bool Equals(object obj)
            {
                return obj is ListPaneStyleCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = UseNameColor ? 1 : 0;
                    hashCode = (hashCode * 397) ^ NameColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ (UseNameFontStyle ? 1 : 0);
                    hashCode = (hashCode * 397) ^ (int)NameFontStyle;
                    return hashCode;
                }
            }
        }
    }
}
