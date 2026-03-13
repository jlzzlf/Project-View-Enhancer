using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [InitializeOnLoad]
    public static class ProjectViewEnhancerGuideDrawer
    {
        private static readonly Dictionary<string, bool> s_folderStateCache = new(StringComparer.Ordinal);
        private static string s_activeSelectedAssetPath = string.Empty;

        static ProjectViewEnhancerGuideDrawer()
        {
            EditorApplication.projectChanged -= ClearState;
            EditorApplication.projectChanged += ClearState;

            Undo.undoRedoPerformed -= ClearState;
            Undo.undoRedoPerformed += ClearState;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            RefreshActiveSelectedAssetPath();
        }

        internal static string GetReflectionGuideSelectedPath(bool isAssetTree, bool isTwoColumn)
        {
            return !isTwoColumn && isAssetTree ? s_activeSelectedAssetPath : string.Empty;
        }

        internal static void DrawReflectionTreeGuides(
            Rect selectionRect,
            float currentGuideColumnX,
            float currentJoinWidth,
            string assetPath,
            string selectedPath,
            int depth,
            int nextRowDepth,
            float indentWidth)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var settings = ProjectViewEnhancerSettings.instance;
            if (!settings.enabled)
                return;

            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets", StringComparison.Ordinal))
                return;

            if (settings.showIndentGuides)
            {
                Color normalGuideColor = GetNormalGuideColor(settings);
                DrawFastIndentGuides(
                    selectionRect,
                    currentGuideColumnX,
                    currentJoinWidth,
                    depth,
                    nextRowDepth,
                    settings,
                    normalGuideColor,
                    indentWidth);
            }

            if (!settings.highlightActiveSelectionPath || string.IsNullOrEmpty(selectedPath))
                return;

            bool isSelected = string.Equals(assetPath, selectedPath, StringComparison.Ordinal);
            bool isAncestor = IsChildOf(selectedPath, assetPath);
            if (!isSelected && !isAncestor)
            {
                DrawFastSelectionPassThroughGuides(
                    selectionRect,
                    currentGuideColumnX,
                    assetPath,
                    selectedPath,
                    depth,
                    nextRowDepth,
                    settings,
                    settings.activeSelectionGuideColor,
                    indentWidth);
                return;
            }

            DrawFastSelectionPathGuides(
                selectionRect,
                currentGuideColumnX,
                currentJoinWidth,
                assetPath,
                selectedPath,
                depth,
                nextRowDepth,
                settings,
                settings.activeSelectionGuideColor,
                indentWidth);
        }

        private static void ClearState()
        {
            s_folderStateCache.Clear();
            ProjectViewEnhancerProjectBrowserMode.Invalidate();
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnSelectionChanged()
        {
            RefreshActiveSelectedAssetPath();
        }

        private static void RefreshActiveSelectedAssetPath()
        {
            UnityEngine.Object activeObject = Selection.activeObject;
            if (activeObject == null)
            {
                s_activeSelectedAssetPath = string.Empty;
                return;
            }

            string path = AssetDatabase.GetAssetPath(activeObject);
            s_activeSelectedAssetPath = string.IsNullOrEmpty(path) ? string.Empty : NormalizePath(path);
        }

        private static Color GetNormalGuideColor(ProjectViewEnhancerSettings settings)
        {
            float alpha = Mathf.Clamp01(settings.defaultGuideAlpha);
            Color color = settings.useCustomGuideColor
                ? settings.guideColor
                : (EditorGUIUtility.isProSkin ? Color.white : Color.black);

            color.a = alpha;
            return color;
        }

        private static void DrawFastIndentGuides(
            Rect rect,
            float currentGuideColumnX,
            float currentJoinWidth,
            int depth,
            int nextRowDepth,
            ProjectViewEnhancerSettings settings,
            Color color,
            float indentWidth)
        {
            float thickness = GetGuideThickness(settings);
            float midY = rect.y + rect.height * 0.5f;

            for (int columnDepth = 1; columnDepth <= depth; columnDepth++)
            {
                float x = GetGuideColumnX(currentGuideColumnX, depth, columnDepth, indentWidth);
                float yEnd = ColumnContinuesBelow(nextRowDepth, columnDepth) ? rect.yMax : midY;
                DrawGuideVerticalSegment(x, rect.yMin, yEnd, thickness, color);
            }

            if (settings.showHorizontalJoin && depth > 0)
            {
                DrawGuideHorizontalSegment(currentGuideColumnX, midY, currentJoinWidth, thickness, color);
            }
        }

        private static void DrawFastSelectionPathGuides(
            Rect rect,
            float currentGuideColumnX,
            float currentJoinWidth,
            string currentPath,
            string selectedPath,
            int depth,
            int nextRowDepth,
            ProjectViewEnhancerSettings settings,
            Color color,
            float indentWidth)
        {
            bool isSelected = string.Equals(currentPath, selectedPath, StringComparison.Ordinal);
            bool isAncestor = IsChildOf(selectedPath, currentPath);
            if (!isSelected && !isAncestor)
                return;

            float thickness = GetGuideThickness(settings);
            float midY = rect.y + rect.height * 0.5f;
            float rowBottomY = rect.yMax;
            float parentColumnX = currentGuideColumnX;
            float ownChildrenColumnX = GetGuideColumnX(currentGuideColumnX, depth, depth + 1, indentWidth);

            if (depth > 0)
            {
                DrawHighlightedVerticalSegment(parentColumnX, rect.yMin, midY, thickness, color);
                DrawGuideHorizontalSegment(parentColumnX, midY, currentJoinWidth, thickness, color);
            }

            if (isAncestor)
            {
                float yEnd = ColumnContinuesBelow(nextRowDepth, depth + 1) ? rowBottomY : midY;
                float yStart = GetOwnStemStartY(rect, indentWidth);
                DrawHighlightedVerticalSegment(ownChildrenColumnX, yStart, yEnd, thickness, color);
            }
        }

        private static void DrawFastSelectionPassThroughGuides(
            Rect rect,
            float currentGuideColumnX,
            string currentPath,
            string selectedPath,
            int currentDepth,
            int nextRowDepth,
            ProjectViewEnhancerSettings settings,
            Color color,
            float indentWidth)
        {
            if (string.Equals(currentPath, selectedPath, StringComparison.Ordinal) || IsChildOf(selectedPath, currentPath))
                return;

            float thickness = GetGuideThickness(settings);
            float midY = rect.y + rect.height * 0.5f;
            float rowBottomY = rect.yMax;
            string ancestorPath = "Assets";

            while (true)
            {
                string targetChildOnSelectionChain = GetImmediateChildOnSelectionChain(ancestorPath, selectedPath);
                if (string.IsNullOrEmpty(targetChildOnSelectionChain))
                    break;

                string currentChildUnderAncestor = GetImmediateChildUnderAncestor(currentPath, ancestorPath);
                if (!string.IsNullOrEmpty(currentChildUnderAncestor) &&
                    currentChildUnderAncestor != targetChildOnSelectionChain)
                {
                    bool currentIsFolder = IsFolderPath(currentChildUnderAncestor);
                    bool targetIsFolder = IsFolderPath(targetChildOnSelectionChain);

                    if (CompareProjectItemOrder(
                            currentChildUnderAncestor,
                            currentIsFolder,
                            targetChildOnSelectionChain,
                            targetIsFolder) < 0)
                    {
                        int ancestorDepth = GetDepth(ancestorPath);
                        int childDepth = ancestorDepth + 1;
                        if (currentDepth >= childDepth)
                        {
                            float x = GetGuideColumnX(currentGuideColumnX, currentDepth, childDepth, indentWidth);
                            float yEnd = ColumnContinuesBelow(nextRowDepth, childDepth) ? rowBottomY : midY;
                            DrawHighlightedVerticalSegment(x, rect.yMin, yEnd, thickness, color);
                        }
                    }
                }

                if (targetChildOnSelectionChain == selectedPath)
                    break;

                ancestorPath = targetChildOnSelectionChain;
            }
        }

        private static float GetGuideColumnX(float currentGuideColumnX, int currentDepth, int childDepth, float indentWidth)
        {
            return currentGuideColumnX - ((currentDepth - childDepth) * indentWidth);
        }

        private static string GetImmediateChildUnderAncestor(string path, string ancestorPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(ancestorPath))
                return string.Empty;

            path = NormalizePath(path);
            ancestorPath = NormalizePath(ancestorPath);

            if (!IsChildOf(path, ancestorPath))
                return string.Empty;

            int nextSlashIndex = path.IndexOf('/', ancestorPath.Length + 1);
            return nextSlashIndex < 0 ? path : path.Substring(0, nextSlashIndex);
        }

        private static string GetImmediateChildOnSelectionChain(string parentPath, string selectedPath)
        {
            if (string.IsNullOrEmpty(selectedPath))
                return string.Empty;

            parentPath = NormalizePath(parentPath);
            if (!IsChildOf(selectedPath, parentPath))
                return string.Empty;

            int parentLength = parentPath.Length;
            int nextSlashIndex = selectedPath.IndexOf('/', parentLength + 1);
            return nextSlashIndex < 0
                ? selectedPath
                : selectedPath.Substring(0, nextSlashIndex);
        }

        private static int GetDepth(string path)
        {
            if (path == "Assets")
                return 0;

            int slashCount = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/')
                    slashCount++;
            }

            return Mathf.Max(0, slashCount);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace("\\", "/").TrimEnd('/');
        }

        private static bool IsChildOf(string path, string folderPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folderPath))
                return false;

            if (path.Length <= folderPath.Length)
                return false;

            if (!path.StartsWith(folderPath, StringComparison.Ordinal))
                return false;

            return path[folderPath.Length] == '/';
        }

        private static int CompareProjectItemOrder(
            string pathA,
            bool isFolderA,
            string pathB,
            bool isFolderB)
        {
            if (isFolderA != isFolderB)
                return isFolderA ? -1 : 1;

            string nameA = Path.GetFileName(pathA);
            string nameB = Path.GetFileName(pathB);
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ColumnContinuesBelow(int nextRowDepth, int columnDepth)
        {
            return nextRowDepth >= columnDepth;
        }

        private static bool IsFolderPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (s_folderStateCache.TryGetValue(assetPath, out bool cached))
                return cached;

            bool isFolder = AssetDatabase.IsValidFolder(assetPath);
            s_folderStateCache[assetPath] = isFolder;
            return isFolder;
        }

        private static float GetGuideThickness(ProjectViewEnhancerSettings settings)
        {
            return Mathf.Max(1f, Mathf.Round(settings.guideThickness));
        }

        private static float GetOwnStemStartY(Rect rect, float indentWidth)
        {
            float midY = rect.y + rect.height * 0.5f;
            float rowBottomY = rect.yMax;
            return Mathf.Min(rowBottomY, midY + (GetGuideScale(indentWidth) * 5f));
        }

        private static float GetGuideScale(float indentWidth)
        {
            return Mathf.Max(0.5f, indentWidth / 14f);
        }

        private static void DrawHighlightedVerticalSegment(float x, float yStart, float yEnd, float thickness, Color color)
        {
            if (yEnd <= yStart)
                return;

            DrawGuideVerticalSegment(x, yStart, yEnd, thickness, color);
        }

        private static void DrawGuideVerticalSegment(float x, float yStart, float yEnd, float thickness, Color color)
        {
            if (yEnd <= yStart)
                return;

            float snappedThickness = Mathf.Max(1f, Mathf.Round(thickness));
            float snappedX = Mathf.Round(x);
            float snappedYMin = Mathf.Floor(yStart);
            float snappedYMax = Mathf.Ceil(yEnd);
            if (snappedYMax <= snappedYMin)
                snappedYMax = snappedYMin + snappedThickness;

            EditorGUI.DrawRect(
                new Rect(snappedX, snappedYMin, snappedThickness, snappedYMax - snappedYMin),
                color);
        }

        private static void DrawGuideHorizontalSegment(float x, float y, float width, float thickness, Color color)
        {
            if (width <= 0f)
                return;

            float snappedThickness = Mathf.Max(1f, Mathf.Round(thickness));
            float snappedX = Mathf.Round(x);
            float snappedY = Mathf.Round(y - (snappedThickness * 0.5f));
            float snappedWidth = Mathf.Max(1f, Mathf.Ceil(width));

            EditorGUI.DrawRect(
                new Rect(snappedX, snappedY, snappedWidth, snappedThickness),
                color);
        }
    }
}
