using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal static class ProjectViewEnhancerProjectBrowserMode
    {
        public readonly struct FolderTreeRowInfo
        {
            public FolderTreeRowInfo(int rowIndex, int depth, int nextRowDepth)
            {
                RowIndex = rowIndex;
                Depth = depth;
                NextRowDepth = nextRowDepth;
            }

            public int RowIndex { get; }
            public int Depth { get; }
            public int NextRowDepth { get; }
        }

        public readonly struct TreeOwnerInfo
        {
            public TreeOwnerInfo(bool isAssetTree, bool isTwoColumn)
            {
                IsAssetTree = isAssetTree;
                IsTwoColumn = isTwoColumn;
            }

            public bool IsAssetTree { get; }
            public bool IsTwoColumn { get; }
        }

        private sealed class TreeOwnerReference
        {
            public TreeOwnerReference(object browser, bool isAssetTree)
            {
                Browser = browser;
                IsAssetTree = isAssetTree;
            }

            public object Browser { get; }
            public bool IsAssetTree { get; }
        }

        private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type s_projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
        private static readonly FieldInfo s_lastInteractedProjectBrowserField =
            s_projectBrowserType?.GetField("s_LastInteractedProjectBrowser", StaticBindingFlags);
        private static readonly FieldInfo s_viewModeField =
            s_projectBrowserType?.GetField("m_ViewMode", InstanceBindingFlags);
        private static readonly PropertyInfo s_viewModeProperty =
            s_projectBrowserType?.GetProperty("viewMode", InstanceBindingFlags);
        private static readonly FieldInfo s_treeViewRectField =
            s_projectBrowserType?.GetField("m_TreeViewRect", InstanceBindingFlags);
        private static readonly FieldInfo s_listAreaRectField =
            s_projectBrowserType?.GetField("m_ListAreaRect", InstanceBindingFlags);
        private static readonly FieldInfo s_listAreaField =
            s_projectBrowserType?.GetField("m_ListArea", InstanceBindingFlags);
        private static readonly FieldInfo s_assetTreeField =
            s_projectBrowserType?.GetField("m_AssetTree", InstanceBindingFlags);
        private static readonly FieldInfo s_folderTreeField =
            s_projectBrowserType?.GetField("m_FolderTree", InstanceBindingFlags);
        private static readonly Type s_objectListAreaType = Type.GetType("UnityEditor.ObjectListArea, UnityEditor");
        private static readonly MethodInfo s_objectListAreaIsListModeMethod =
            s_objectListAreaType?.GetMethod("IsListMode", InstanceBindingFlags, null, Type.EmptyTypes, null);
        private static readonly FieldInfo s_localAssetsField =
            s_objectListAreaType?.GetField("m_LocalAssets", InstanceBindingFlags);
        private static readonly Type s_localGroupType = Type.GetType("UnityEditor.ObjectListArea+LocalGroup, UnityEditor");
        private static readonly FieldInfo s_localGroupListModeField =
            s_localGroupType?.GetField("m_ListMode", InstanceBindingFlags);
        private static readonly Type s_guiClipType = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip");
        private static readonly PropertyInfo s_guiClipVisibleRectProperty =
            s_guiClipType?.GetProperty("visibleRect", StaticBindingFlags);
        private static readonly MethodInfo s_guiClipUnclipToWindowRectMethod =
            s_guiClipType?.GetMethod("UnclipToWindow", StaticBindingFlags, null, new[] { typeof(Rect) }, null);

        private static readonly Dictionary<string, int> s_assetInstanceIdByPath = new(StringComparer.Ordinal);
        private static readonly Dictionary<object, TreeOwnerReference> s_treeOwnerReferenceByTree = new();

        private static double s_nextCheckTime;
        private static bool s_isTwoColumn;

        public static bool IsTwoColumn()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < s_nextCheckTime)
                return s_isTwoColumn;

            s_nextCheckTime = now + 0.25d;
            s_isTwoColumn = QueryIsTwoColumn();
            return s_isTwoColumn;
        }

        public static void Invalidate()
        {
            s_nextCheckTime = 0d;
            s_isTwoColumn = false;
            s_assetInstanceIdByPath.Clear();
            s_treeOwnerReferenceByTree.Clear();
        }

        public static bool TryGetTwoColumnPaneRects(out Rect treeViewRect, out Rect listAreaRect)
        {
            treeViewRect = default;
            listAreaRect = default;

            if (s_projectBrowserType == null)
                return false;

            try
            {
                object browser = GetCurrentProjectBrowser();
                if (browser == null)
                    return false;

                object viewMode = s_viewModeField?.GetValue(browser) ?? s_viewModeProperty?.GetValue(browser);
                if (!IsTwoColumnViewMode(viewMode))
                    return false;

                if (s_treeViewRectField?.GetValue(browser) is not Rect treeRect ||
                    s_listAreaRectField?.GetValue(browser) is not Rect listRect)
                {
                    return false;
                }

                treeViewRect = treeRect;
                listAreaRect = listRect;
                return treeViewRect.width > 0f;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetCurrentClipRect(out Rect clipRect)
        {
            clipRect = default;

            try
            {
                if (s_guiClipVisibleRectProperty?.GetValue(null) is not Rect visibleRect)
                    return false;

                clipRect = visibleRect;
                if (s_guiClipUnclipToWindowRectMethod?.Invoke(null, new object[] { visibleRect }) is Rect unclippedRect)
                    clipRect = unclippedRect;

                return clipRect.width > 0f && clipRect.height > 0f;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetFolderTreeRowIndex(string assetPath, out int rowIndex)
        {
            rowIndex = -1;

            if (!TryGetTreeDataSource(s_folderTreeField, out object dataSource))
                return false;

            int assetInstanceId = GetAssetInstanceId(assetPath);
            if (assetInstanceId == 0)
                return false;

            return TryGetFolderTreeRowIndex(dataSource, assetInstanceId, out rowIndex);
        }

        public static bool TryGetAssetTreeRowInfo(string assetPath, out FolderTreeRowInfo rowInfo)
        {
            return TryGetTreeRowInfo(s_assetTreeField, assetPath, out rowInfo);
        }

        public static bool TryGetFolderTreeRowInfo(string assetPath, out FolderTreeRowInfo rowInfo)
        {
            return TryGetTreeRowInfo(s_folderTreeField, assetPath, out rowInfo);
        }

        public static bool TryGetAssetTreeSelectedPath(out string selectedPath)
        {
            return TryGetTreeSelectedPath(s_assetTreeField, out selectedPath);
        }

        public static bool TryGetTreeOwnerInfo(object tree, out TreeOwnerInfo ownerInfo)
        {
            ownerInfo = default;

            if (tree == null || s_projectBrowserType == null)
                return false;

            if (s_treeOwnerReferenceByTree.TryGetValue(tree, out TreeOwnerReference cachedReference))
                return TryBuildTreeOwnerInfo(cachedReference, out ownerInfo);

            try
            {
                UnityEngine.Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(s_projectBrowserType);
                if (projectBrowsers == null || projectBrowsers.Length == 0)
                    return false;

                for (int i = 0; i < projectBrowsers.Length; i++)
                {
                    object browser = projectBrowsers[i];
                    if (browser == null)
                        continue;

                    object assetTree = s_assetTreeField?.GetValue(browser);
                    if (assetTree != null && ReferenceEquals(tree, assetTree))
                    {
                        var ownerReference = new TreeOwnerReference(browser, true);
                        s_treeOwnerReferenceByTree[tree] = ownerReference;
                        return TryBuildTreeOwnerInfo(ownerReference, out ownerInfo);
                    }

                    object folderTree = s_folderTreeField?.GetValue(browser);
                    if (folderTree != null && ReferenceEquals(tree, folderTree))
                    {
                        var ownerReference = new TreeOwnerReference(browser, false);
                        s_treeOwnerReferenceByTree[tree] = ownerReference;
                        return TryBuildTreeOwnerInfo(ownerReference, out ownerInfo);
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool TryGetTwoColumnRightPaneListMode(out bool isListMode)
        {
            isListMode = false;

            if (s_listAreaField == null)
                return false;

            try
            {
                object browser = GetCurrentProjectBrowser();
                if (browser == null)
                    return false;

                object listArea = s_listAreaField.GetValue(browser);
                if (listArea == null)
                    return false;

                if (s_objectListAreaIsListModeMethod != null)
                {
                    object value = s_objectListAreaIsListModeMethod.Invoke(listArea, null);
                    if (value != null)
                    {
                        isListMode = Convert.ToBoolean(value);
                        return true;
                    }
                }

                if (s_localAssetsField == null || s_localGroupListModeField == null)
                    return false;

                object localAssets = s_localAssetsField.GetValue(listArea);
                if (localAssets == null)
                    return false;
                isListMode = Convert.ToBoolean(s_localGroupListModeField.GetValue(localAssets));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetFolderTreeSelectedPath(out string selectedPath)
        {
            return TryGetTreeSelectedPath(s_folderTreeField, out selectedPath);
        }

        private static bool QueryIsTwoColumn()
        {
            if (s_projectBrowserType == null)
                return false;

            try
            {
                object browser = GetCurrentProjectBrowser();
                if (browser == null)
                    return false;

                object viewMode = s_viewModeField?.GetValue(browser) ?? s_viewModeProperty?.GetValue(browser);
                return IsTwoColumnViewMode(viewMode);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTwoColumnViewMode(object viewMode)
        {
            if (viewMode == null)
                return false;

            if (viewMode.GetType().IsEnum)
                return string.Equals(viewMode.ToString(), "TwoColumns", StringComparison.OrdinalIgnoreCase);

            return Convert.ToInt32(viewMode) != 0;
        }

        private static bool TryBuildTreeOwnerInfo(TreeOwnerReference ownerReference, out TreeOwnerInfo ownerInfo)
        {
            ownerInfo = default;

            if (ownerReference?.Browser == null)
                return false;

            try
            {
                object viewMode = s_viewModeField?.GetValue(ownerReference.Browser) ?? s_viewModeProperty?.GetValue(ownerReference.Browser);
                ownerInfo = new TreeOwnerInfo(ownerReference.IsAssetTree, IsTwoColumnViewMode(viewMode));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetCurrentProjectBrowser()
        {
            if (TryGetCurrentClipRect(out Rect clipRect) && TryFindProjectBrowserByClipRect(clipRect, out object clipBrowser))
                return clipBrowser;

            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null && s_projectBrowserType.IsInstanceOfType(focusedWindow))
                return focusedWindow;

            object lastInteractedBrowser = s_lastInteractedProjectBrowserField?.GetValue(null);
            if (lastInteractedBrowser != null && s_projectBrowserType.IsInstanceOfType(lastInteractedBrowser))
                return lastInteractedBrowser;

            UnityEngine.Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(s_projectBrowserType);
            return projectBrowsers != null && projectBrowsers.Length > 0 ? projectBrowsers[0] : null;
        }

        private static bool TryFindProjectBrowserByClipRect(Rect clipRect, out object browser)
        {
            browser = null;

            if (s_projectBrowserType == null || clipRect.width <= 0f || clipRect.height <= 0f)
                return false;

            try
            {
                UnityEngine.Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(s_projectBrowserType);
                if (projectBrowsers == null || projectBrowsers.Length == 0)
                    return false;

                float bestScore = 0f;
                for (int i = 0; i < projectBrowsers.Length; i++)
                {
                    object candidate = projectBrowsers[i];
                    if (candidate == null)
                        continue;

                    float score = GetProjectBrowserClipOverlapScore(candidate, clipRect);
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    browser = candidate;
                }

                return browser != null;
            }
            catch
            {
                browser = null;
                return false;
            }
        }

        private static float GetProjectBrowserClipOverlapScore(object browser, Rect clipRect)
        {
            if (browser == null)
                return 0f;

            float bestScore = 0f;

            if (s_treeViewRectField?.GetValue(browser) is Rect treeRect)
                bestScore = Mathf.Max(bestScore, GetRectOverlapArea(clipRect, treeRect));

            if (s_listAreaRectField?.GetValue(browser) is Rect listRect)
                bestScore = Mathf.Max(bestScore, GetRectOverlapArea(clipRect, listRect));

            return bestScore;
        }

        private static float GetRectOverlapArea(Rect a, Rect b)
        {
            float minX = Mathf.Max(a.xMin, b.xMin);
            float maxX = Mathf.Min(a.xMax, b.xMax);
            float minY = Mathf.Max(a.yMin, b.yMin);
            float maxY = Mathf.Min(a.yMax, b.yMax);
            float width = Mathf.Max(0f, maxX - minX);
            float height = Mathf.Max(0f, maxY - minY);
            return width * height;
        }

        private static bool TryGetTreeRowInfo(FieldInfo treeField, string assetPath, out FolderTreeRowInfo rowInfo)
        {
            rowInfo = default;

            if (!TryGetTreeDataSource(treeField, out object dataSource))
                return false;

            int assetInstanceId = GetAssetInstanceId(assetPath);
            if (assetInstanceId == 0)
                return false;

            if (!TryGetFolderTreeRowIndex(dataSource, assetInstanceId, out int rowIndex))
                return false;

            if (!TryGetFolderTreeRows(dataSource, out IList rows))
                return false;

            if (rowIndex < 0 || rowIndex >= rows.Count)
                return false;

            if (!TryGetTreeViewItemDepth(rows[rowIndex], out int depth))
                return false;

            int nextRowDepth = -1;
            if (rowIndex + 1 < rows.Count && !TryGetTreeViewItemDepth(rows[rowIndex + 1], out nextRowDepth))
                return false;

            rowInfo = new FolderTreeRowInfo(rowIndex, depth, nextRowDepth);
            return true;
        }

        private static bool TryGetTreeSelectedPath(FieldInfo treeField, out string selectedPath)
        {
            selectedPath = string.Empty;

            if (!TryGetTree(treeField, out object tree))
                return false;

            try
            {
                MethodInfo getSelectionMethod = tree.GetType().GetMethod(
                    "GetSelection",
                    InstanceBindingFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getSelectionMethod == null)
                    return false;

                if (getSelectionMethod.Invoke(tree, null) is not int[] selectedIds || selectedIds.Length == 0)
                    return false;

                string path = AssetDatabase.GetAssetPath(selectedIds[0]);
                if (string.IsNullOrEmpty(path))
                    return false;

                selectedPath = path;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetTreeDataSource(FieldInfo treeField, out object dataSource)
        {
            dataSource = null;

            if (!TryGetTree(treeField, out object tree))
                return false;

            PropertyInfo dataProperty = tree.GetType().GetProperty("data", InstanceBindingFlags);
            dataSource = dataProperty?.GetValue(tree);
            return dataSource != null;
        }

        private static bool TryGetTree(FieldInfo treeField, out object tree)
        {
            tree = null;

            if (treeField == null)
                return false;

            object browser = GetCurrentProjectBrowser();
            if (browser == null)
                return false;

            tree = treeField.GetValue(browser);
            return tree != null;
        }

        private static bool TryGetFolderTreeRowIndex(object dataSource, int assetInstanceId, out int rowIndex)
        {
            rowIndex = -1;

            MethodInfo getRowMethod = dataSource.GetType().GetMethod(
                "GetRow",
                InstanceBindingFlags,
                null,
                new[] { typeof(int) },
                null);
            if (getRowMethod == null)
                return false;

            if (getRowMethod.Invoke(dataSource, new object[] { assetInstanceId }) is not int visibleRowIndex)
                return false;

            if (visibleRowIndex < 0)
                return false;

            rowIndex = visibleRowIndex;
            return true;
        }

        private static bool TryGetFolderTreeRows(object dataSource, out IList rows)
        {
            rows = null;

            MethodInfo getRowsMethod = dataSource.GetType().GetMethod(
                "GetRows",
                InstanceBindingFlags,
                null,
                Type.EmptyTypes,
                null);
            if (getRowsMethod == null)
                return false;

            rows = getRowsMethod.Invoke(dataSource, null) as IList;
            return rows != null;
        }

        private static bool TryGetTreeViewItemDepth(object item, out int depth)
        {
            depth = -1;
            if (item == null)
                return false;

            PropertyInfo depthProperty = item.GetType().GetProperty("depth", InstanceBindingFlags);
            if (depthProperty == null)
                return false;

            object value = depthProperty.GetValue(item);
            if (value == null)
                return false;

            depth = Convert.ToInt32(value);
            return true;
        }

        private static int GetAssetInstanceId(string assetPath)
        {
            if (s_assetInstanceIdByPath.TryGetValue(assetPath, out int cachedInstanceId))
                return cachedInstanceId;

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            int instanceId = asset != null ? asset.GetInstanceID() : 0;
            s_assetInstanceIdByPath[assetPath] = instanceId;
            return instanceId;
        }
    }
}
