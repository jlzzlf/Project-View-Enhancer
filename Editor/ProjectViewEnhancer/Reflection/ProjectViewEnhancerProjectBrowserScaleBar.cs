using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [InitializeOnLoad]
    internal static class ProjectViewEnhancerProjectBrowserScaleBar
    {
        private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ContainerName = "ProjectViewEnhancer-ProjectBrowserScaleBar";
        private const float FallbackBottomBarHeight = 20f;
        private const float SliderWidth = 96f;
        private const float RightPadding = 10f;
        private const float LeftPadding = 8f;

        private static readonly Type s_projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
        private static readonly FieldInfo s_bottomBarRectField =
            s_projectBrowserType?.GetField("m_BottomBarRect", InstanceBindingFlags);
        private static readonly FieldInfo s_treeViewRectField =
            s_projectBrowserType?.GetField("m_TreeViewRect", InstanceBindingFlags);
        private static readonly FieldInfo s_viewModeField =
            s_projectBrowserType?.GetField("m_ViewMode", InstanceBindingFlags);
        private static readonly FieldInfo s_selectedPathField =
            s_projectBrowserType?.GetField("m_SelectedPath", InstanceBindingFlags);
        private static readonly FieldInfo s_lastProjectPathField =
            s_projectBrowserType?.GetField("m_LastProjectPath", InstanceBindingFlags);
        private static readonly PropertyInfo s_bottomBarHeightProperty =
            s_projectBrowserType?.GetProperty("k_BottomBarHeight", StaticBindingFlags);
        private static readonly PropertyInfo s_viewModeProperty =
            s_projectBrowserType?.GetProperty("viewMode", InstanceBindingFlags);
        private static readonly MethodInfo s_getSelectedPathMethod =
            s_projectBrowserType?.GetMethod("GetSelectedPath", InstanceBindingFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo s_getActiveFolderPathMethod =
            s_projectBrowserType?.GetMethod("GetActiveFolderPath", InstanceBindingFlags, null, Type.EmptyTypes, null);

        private static readonly Dictionary<int, IMGUIContainer> s_containersByWindowId = new();
        private static readonly HashSet<int> s_scalePreviewWindowIds = new();
        private static GUIStyle s_pathLabelStyle;
        private static GUIStyle s_valueLabelStyle;

        static ProjectViewEnhancerProjectBrowserScaleBar()
        {
            EditorApplication.update -= EnsureAttached;
            EditorApplication.update += EnsureAttached;

            Selection.selectionChanged -= RepaintAllBars;
            Selection.selectionChanged += RepaintAllBars;

            EditorApplication.projectChanged -= RepaintAllBars;
            EditorApplication.projectChanged += RepaintAllBars;

            AssemblyReloadEvents.beforeAssemblyReload -= DetachAll;
            AssemblyReloadEvents.beforeAssemblyReload += DetachAll;

            EditorApplication.quitting -= DetachAll;
            EditorApplication.quitting += DetachAll;
        }

        private static void EnsureAttached()
        {
            if (s_projectBrowserType == null)
                return;

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(s_projectBrowserType);
            var aliveWindowIds = new HashSet<int>();

            if (windows != null)
            {
                for (int i = 0; i < windows.Length; i++)
                {
                    if (windows[i] is not EditorWindow projectBrowser)
                        continue;

                    int windowId = projectBrowser.GetInstanceID();
                    aliveWindowIds.Add(windowId);
                    EnsureAttached(projectBrowser, windowId);
                }
            }

            CleanupDeadWindows(aliveWindowIds);
        }

        private static void EnsureAttached(EditorWindow projectBrowser, int windowId)
        {
            VisualElement root = projectBrowser.rootVisualElement;
            if (root == null)
                return;

            if (!s_containersByWindowId.TryGetValue(windowId, out IMGUIContainer container) || container == null)
            {
                container = CreateContainer(projectBrowser, windowId);
                s_containersByWindowId[windowId] = container;
            }

            if (container.parent != root)
                root.Add(container);

            Rect layoutRect = GetScaleBarRect(projectBrowser, root);
            container.style.position = Position.Absolute;
            container.style.left = layoutRect.x;
            container.style.width = layoutRect.width;
            container.style.bottom = 0f;
            container.style.height = layoutRect.height;
            container.style.display = DisplayStyle.Flex;
            container.BringToFront();
        }

        private static IMGUIContainer CreateContainer(EditorWindow projectBrowser, int windowId)
        {
            var container = new IMGUIContainer(() => DrawScaleBar(projectBrowser, windowId))
            {
                name = ContainerName,
                pickingMode = PickingMode.Position
            };

            return container;
        }

        private static void DrawScaleBar(EditorWindow projectBrowser, int windowId)
        {
            if (projectBrowser == null)
                return;

            if (!s_containersByWindowId.TryGetValue(windowId, out IMGUIContainer container) || container == null)
                return;

            Rect rect = new(0f, 0f, container.contentRect.width, container.contentRect.height);
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            EnsureStyles();
            DrawBackground(rect);

            string path = GetDisplayPath(projectBrowser);
            DrawPath(rect, path);
            DrawScaleControls(rect, projectBrowser, container, windowId);
        }

        private static void EnsureStyles()
        {
            if (s_pathLabelStyle == null)
            {
                s_pathLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                Color pathColor = EditorGUIUtility.isProSkin
                    ? new Color(0.82f, 0.82f, 0.82f, 1f)
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
                s_pathLabelStyle.normal.textColor = pathColor;
            }

            if (s_valueLabelStyle == null)
            {
                s_valueLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                Color valueColor = EditorGUIUtility.isProSkin
                    ? new Color(0.72f, 0.72f, 0.72f, 1f)
                    : new Color(0.26f, 0.26f, 0.26f, 1f);
                s_valueLabelStyle.normal.textColor = valueColor;
            }
        }

        private static void DrawBackground(Rect rect)
        {
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.235f, 0.235f, 0.235f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0.32f, 0.32f, 0.32f, 1f)
                : new Color(0.58f, 0.58f, 0.58f, 1f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
        }

        private static void DrawPath(Rect rect, string path)
        {
            float controlsWidth = SliderWidth + RightPadding + 8f;
            Rect pathRect = new(
                rect.x + LeftPadding,
                rect.y,
                Mathf.Max(0f, rect.width - controlsWidth - LeftPadding),
                rect.height);

            GUI.Label(pathRect, path, s_pathLabelStyle);
        }

        private static void DrawScaleControls(Rect rect, EditorWindow projectBrowser, IMGUIContainer container, int windowId)
        {
            float currentScale = ProjectViewEnhancerTreeScaleState.GetUiScaleValue();
            float sliderHeight = 14f;
            float sliderY = rect.y + ((rect.height - sliderHeight) * 0.5f);
            float sliderX = rect.xMax - RightPadding - SliderWidth;

            Rect sliderRect = new(sliderX, sliderY, SliderWidth, sliderHeight);
            float newScale = GUI.HorizontalSlider(sliderRect, currentScale, 1f, 2f);
            Rect valueRect = new(sliderRect.x - 44f, rect.y, 40f, rect.height);
            GUI.Label(valueRect, FormatScaleValue(newScale), s_valueLabelStyle);

            Event currentEvent = Event.current;
            bool beginsPreview =
                currentEvent != null
                && (currentEvent.rawType == EventType.MouseDown || currentEvent.rawType == EventType.MouseDrag)
                && currentEvent.button == 0
                && sliderRect.Contains(currentEvent.mousePosition);
            if (beginsPreview)
                s_scalePreviewWindowIds.Add(windowId);

            if (Mathf.Abs(newScale - currentScale) > 0.0001f)
            {
                if (s_scalePreviewWindowIds.Contains(windowId))
                    ProjectViewEnhancerTreeScaleState.PreviewScaleFromUi(newScale);
                else
                    ProjectViewEnhancerTreeScaleState.SetScaleFromUi(newScale);

                ProjectViewEnhancerProjectBrowserMode.Invalidate();
                RequestImmediateProjectBrowserRefresh(projectBrowser, container);
            }

            bool endsPreview =
                currentEvent != null
                && (currentEvent.rawType == EventType.MouseUp || currentEvent.rawType == EventType.Ignore)
                && s_scalePreviewWindowIds.Contains(windowId);
            if (endsPreview)
            {
                s_scalePreviewWindowIds.Remove(windowId);
                ProjectViewEnhancerTreeScaleState.CommitPreviewScale();
                ProjectViewEnhancerProjectBrowserMode.Invalidate();
                RequestImmediateProjectBrowserRefresh(projectBrowser, container);
            }
            else if (s_scalePreviewWindowIds.Contains(windowId))
            {
                RequestImmediateProjectBrowserRefresh(projectBrowser, container);
            }
        }

        private static string FormatScaleValue(float scale)
        {
            return scale <= 1.001f ? "1.00x" : scale.ToString("0.00x");
        }

        private static string GetDisplayPath(EditorWindow projectBrowser)
        {
            string path = InvokeStringMethod(projectBrowser, s_getSelectedPathMethod);
            if (string.IsNullOrEmpty(path))
                path = InvokeStringMethod(projectBrowser, s_getActiveFolderPathMethod);
            if (string.IsNullOrEmpty(path))
                path = s_selectedPathField?.GetValue(projectBrowser) as string;
            if (string.IsNullOrEmpty(path))
                path = s_lastProjectPathField?.GetValue(projectBrowser) as string;

            if (string.IsNullOrEmpty(path))
                path = "Assets";

            return path.Replace("\\", "/");
        }

        private static string InvokeStringMethod(object instance, MethodInfo methodInfo)
        {
            if (instance == null || methodInfo == null)
                return string.Empty;

            try
            {
                return methodInfo.Invoke(instance, null) as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static float GetBottomBarHeight(EditorWindow projectBrowser)
        {
            if (projectBrowser == null)
                return FallbackBottomBarHeight;

            try
            {
                if (s_bottomBarRectField?.GetValue(projectBrowser) is Rect bottomBarRect && bottomBarRect.height > 0f)
                    return bottomBarRect.height;

                if (s_bottomBarHeightProperty?.GetValue(projectBrowser, null) is float bottomBarHeight && bottomBarHeight > 0f)
                    return bottomBarHeight;
            }
            catch
            {
            }

            return FallbackBottomBarHeight;
        }

        private static Rect GetScaleBarRect(EditorWindow projectBrowser, VisualElement root)
        {
            float bottomBarHeight = GetBottomBarHeight(projectBrowser);
            float width = root?.layout.width > 0f
                ? root.layout.width
                : projectBrowser.position.width;
            float x = 0f;

            if (TryGetTwoColumnLeftPaneRect(projectBrowser, out Rect treeViewRect))
            {
                x = Mathf.Max(0f, treeViewRect.xMin);
                width = Mathf.Max(180f, treeViewRect.width);
            }

            return new Rect(x, 0f, Mathf.Max(180f, width), bottomBarHeight);
        }

        private static bool TryGetTwoColumnLeftPaneRect(EditorWindow projectBrowser, out Rect treeViewRect)
        {
            treeViewRect = default;

            if (projectBrowser == null || s_treeViewRectField == null)
                return false;

            try
            {
                object viewMode = s_viewModeField?.GetValue(projectBrowser) ?? s_viewModeProperty?.GetValue(projectBrowser, null);
                if (!IsTwoColumnViewMode(viewMode))
                    return false;

                if (s_treeViewRectField.GetValue(projectBrowser) is not Rect rect || rect.width <= 0f)
                    return false;

                treeViewRect = rect;
                return true;
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

        private static void CleanupDeadWindows(HashSet<int> aliveWindowIds)
        {
            if (s_containersByWindowId.Count == 0)
                return;

            var deadWindowIds = new List<int>();
            foreach (KeyValuePair<int, IMGUIContainer> pair in s_containersByWindowId)
            {
                if (aliveWindowIds.Contains(pair.Key))
                    continue;

                if (pair.Value?.parent != null)
                    pair.Value.RemoveFromHierarchy();

                deadWindowIds.Add(pair.Key);
            }

            for (int i = 0; i < deadWindowIds.Count; i++)
                s_containersByWindowId.Remove(deadWindowIds[i]);
        }

        private static void RepaintAllBars()
        {
            foreach (IMGUIContainer container in s_containersByWindowId.Values)
                container?.MarkDirtyRepaint();
        }

        private static void RequestImmediateProjectBrowserRefresh(EditorWindow projectBrowser, IMGUIContainer container)
        {
            container?.MarkDirtyRepaint();
            projectBrowser?.Repaint();
            EditorApplication.RepaintProjectWindow();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void DetachAll()
        {
            EditorApplication.update -= EnsureAttached;
            Selection.selectionChanged -= RepaintAllBars;
            EditorApplication.projectChanged -= RepaintAllBars;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachAll;
            EditorApplication.quitting -= DetachAll;

            foreach (IMGUIContainer container in s_containersByWindowId.Values)
            {
                if (container?.parent != null)
                    container.RemoveFromHierarchy();
            }

            s_scalePreviewWindowIds.Clear();
            ProjectViewEnhancerTreeScaleState.ClearPreviewScale();
            s_containersByWindowId.Clear();
        }
    }
}
