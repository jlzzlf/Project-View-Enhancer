using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal sealed class ProjectViewEnhancerObjectListAreaVisualPatch
    {
        private readonly struct FolderTextColorInfo
        {
            public FolderTextColorInfo(bool isFolder, bool useNameColor, Color nameColor)
            {
                IsFolder = isFolder;
                UseNameColor = useNameColor;
                NameColor = nameColor;
            }

            public bool IsFolder { get; }
            public bool UseNameColor { get; }
            public Color NameColor { get; }
        }

        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type s_projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
        private static readonly Type s_assetClipboardUtilityType = Type.GetType("UnityEditor.AssetClipboardUtility, UnityEditor");
        private static readonly Type s_assetReferenceType = Type.GetType("UnityEditorInternal.InternalEditorUtility+AssetReference, UnityEditor");

        private static readonly MethodInfo s_targetGetAssetItemColorMethod = s_projectBrowserType?.GetMethod(
            "GetAssetItemColor",
            StaticBindingFlags,
            null,
            new[] { typeof(int) },
            null);

        private static readonly MethodInfo s_replacementGetAssetItemColorMethod = typeof(ProjectViewEnhancerObjectListAreaVisualPatch)
            .GetMethod(nameof(GetAssetItemColorReplacement), StaticBindingFlags);

        private static readonly MethodInfo s_isInSafeModeGetter = typeof(EditorUtility).GetMethod(
            "get_isInSafeMode",
            StaticBindingFlags,
            null,
            Type.EmptyTypes,
            null);

        private static readonly MethodInfo s_hasCutAssetMethod = s_assetClipboardUtilityType?.GetMethod(
            "HasCutAsset",
            StaticBindingFlags,
            null,
            new[] { typeof(int) },
            null);

        private static readonly MethodInfo s_isAssetImportedMethod = s_assetReferenceType?.GetMethod(
            "IsAssetImported",
            StaticBindingFlags,
            null,
            new[] { typeof(int) },
            null);

        private static readonly Func<bool> s_isInSafeModeDelegate = CreateStaticDelegate<Func<bool>>(s_isInSafeModeGetter);
        private static readonly Func<int, bool> s_hasCutAssetDelegate = CreateStaticDelegate<Func<int, bool>>(s_hasCutAssetMethod);
        private static readonly Func<int, bool> s_isAssetImportedDelegate = CreateStaticDelegate<Func<int, bool>>(s_isAssetImportedMethod);

        private static readonly FieldInfo s_fadedOutAssetsColorField =
            s_projectBrowserType?.GetField("kFadedOutAssetsColor", StaticBindingFlags);

        private static readonly ProjectViewEnhancerMethodDetour s_getAssetItemColorDetour =
            CreateDetour(s_targetGetAssetItemColorMethod, s_replacementGetAssetItemColorMethod);

        private static readonly System.Collections.Generic.Dictionary<int, FolderTextColorInfo> s_folderTextColorInfoByInstanceId = new();

        private static bool s_loggedRuntimeFailure;
        private static int s_cachedSettingsChangeStamp = -1;
        private static int s_cachedRightPaneContextFrame = -1;
        private static Rect s_cachedRightPaneContextClipRect;
        private static bool s_cachedRightPaneContextResolved;
        private static bool s_cachedRightPaneContextResult;
        private static string s_cachedRightPaneContextRejectReason = string.Empty;

        static ProjectViewEnhancerObjectListAreaVisualPatch()
        {
            EditorApplication.projectChanged -= ClearCaches;
            EditorApplication.projectChanged += ClearCaches;

            Undo.undoRedoPerformed -= ClearCaches;
            Undo.undoRedoPerformed += ClearCaches;
        }

        public bool IsInstalled => s_getAssetItemColorDetour?.IsInstalled ?? false;

        public bool TryInstall(out string error)
        {
            error = string.Empty;

            if (s_targetGetAssetItemColorMethod == null)
            {
                error = "Failed to locate UnityEditor.ProjectBrowser.GetAssetItemColor.";
                return false;
            }

            if (s_getAssetItemColorDetour == null)
            {
                error = "Failed to prepare ProjectBrowser.GetAssetItemColor detour.";
                return false;
            }

            try
            {
                s_getAssetItemColorDetour.Install();
                s_loggedRuntimeFailure = false;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to install ProjectBrowser.GetAssetItemColor patch: {exception.Message}";
                return false;
            }
        }

        public void Uninstall()
        {
            s_getAssetItemColorDetour?.Uninstall();
            s_loggedRuntimeFailure = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Color GetAssetItemColorReplacement(int instanceId)
        {
            try
            {
                Color defaultColor = GetDefaultAssetItemColor(instanceId);
                if (!TryGetCustomFolderTextColor(instanceId, defaultColor, out Color customColor))
                    return defaultColor;

                return customColor;
            }
            catch (Exception exception)
            {
                HandleRuntimeFailure(exception);
                return GetDefaultAssetItemColor(instanceId);
            }
        }

        private static bool TryGetCustomFolderTextColor(int instanceId, Color defaultColor, out Color customColor)
        {
            customColor = defaultColor;

            if (instanceId == 0)
                return false;

            if (!TryIsRightPaneListContext(out _))
                return false;

            if (!TryGetFolderTextColorInfo(instanceId, out FolderTextColorInfo colorInfo))
                return false;

            Color tintMultiplier = GetTintMultiplier(defaultColor, GUI.color);
            customColor = MultiplyColor(colorInfo.NameColor, tintMultiplier);
            return true;
        }

        private static bool TryIsRightPaneListContext(out string rejectReason)
        {
            rejectReason = string.Empty;

            if (!ProjectViewEnhancerProjectBrowserMode.TryGetCurrentClipRect(out Rect clipRect))
            {
                rejectReason = "clip-rect-unavailable";
                return false;
            }

            int frameCount = Time.frameCount;
            if (frameCount == s_cachedRightPaneContextFrame &&
                s_cachedRightPaneContextResolved &&
                AreRectsApproximatelyEqual(clipRect, s_cachedRightPaneContextClipRect))
            {
                rejectReason = s_cachedRightPaneContextRejectReason;
                return s_cachedRightPaneContextResult;
            }

            s_cachedRightPaneContextFrame = frameCount;
            s_cachedRightPaneContextClipRect = clipRect;
            s_cachedRightPaneContextResolved = true;
            s_cachedRightPaneContextResult = false;
            s_cachedRightPaneContextRejectReason = string.Empty;

            if (!ProjectViewEnhancerProjectBrowserMode.IsTwoColumn())
            {
                rejectReason = "not-two-column";
                s_cachedRightPaneContextRejectReason = rejectReason;
                return false;
            }

            if (!ProjectViewEnhancerProjectBrowserMode.TryGetTwoColumnPaneRects(out Rect treeViewRect, out Rect listAreaRect))
            {
                rejectReason = "pane-rects-unavailable";
                s_cachedRightPaneContextRejectReason = rejectReason;
                return false;
            }

            float listOverlap = GetHorizontalOverlap(clipRect, listAreaRect);
            if (listOverlap <= 0.5f)
            {
                rejectReason = $"list-overlap-too-small:{listOverlap:F2}";
                s_cachedRightPaneContextRejectReason = rejectReason;
                return false;
            }

            float treeOverlap = GetHorizontalOverlap(clipRect, treeViewRect);
            if (listOverlap < treeOverlap)
            {
                rejectReason = $"tree-overlap-dominates:list={listOverlap:F2},tree={treeOverlap:F2}";
                s_cachedRightPaneContextRejectReason = rejectReason;
                return false;
            }

            s_cachedRightPaneContextResult = true;
            return true;
        }

        private static bool TryGetFolderTextColorInfo(int instanceId, out FolderTextColorInfo colorInfo)
        {
            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;
            int changeStamp = settings != null ? settings.ChangeStamp : -1;
            if (changeStamp != s_cachedSettingsChangeStamp)
            {
                s_folderTextColorInfoByInstanceId.Clear();
                s_cachedSettingsChangeStamp = changeStamp;
            }

            if (s_folderTextColorInfoByInstanceId.TryGetValue(instanceId, out colorInfo))
                return colorInfo.IsFolder && colorInfo.UseNameColor;

            string assetPath = AssetDatabase.GetAssetPath(instanceId);
            if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
            {
                colorInfo = new FolderTextColorInfo(false, false, default);
                s_folderTextColorInfoByInstanceId[instanceId] = colorInfo;
                return false;
            }

            string normalizedPath = ProjectViewEnhancerVisualStyleResolver.NormalizeAssetPath(assetPath);
            if (settings == null ||
                !settings.TryGetFolderVisualStyleOverride(normalizedPath, out ProjectViewEnhancerSettings.FolderVisualStyleOverride styleOverride) ||
                !styleOverride.useNameColor)
            {
                colorInfo = new FolderTextColorInfo(true, false, default);
                s_folderTextColorInfoByInstanceId[instanceId] = colorInfo;
                return false;
            }

            colorInfo = new FolderTextColorInfo(true, true, styleOverride.nameColor);
            s_folderTextColorInfoByInstanceId[instanceId] = colorInfo;
            return true;
        }

        private static float GetHorizontalOverlap(Rect a, Rect b)
        {
            float min = Mathf.Max(a.xMin, b.xMin);
            float max = Mathf.Min(a.xMax, b.xMax);
            return Mathf.Max(0f, max - min);
        }

        private static Color GetDefaultAssetItemColor(int instanceId)
        {
            bool isInSafeMode = s_isInSafeModeDelegate != null
                ? s_isInSafeModeDelegate()
                : s_isInSafeModeGetter != null && Convert.ToBoolean(s_isInSafeModeGetter.Invoke(null, null));
            bool isAssetImported = s_isAssetImportedDelegate != null
                ? s_isAssetImportedDelegate(instanceId)
                : s_isAssetImportedMethod != null && Convert.ToBoolean(s_isAssetImportedMethod.Invoke(null, new object[] { instanceId }));
            bool hasCutAsset = s_hasCutAssetDelegate != null
                ? s_hasCutAssetDelegate(instanceId)
                : s_hasCutAssetMethod != null && Convert.ToBoolean(s_hasCutAssetMethod.Invoke(null, new object[] { instanceId }));

            if ((!isInSafeMode || isAssetImported) && !hasCutAsset)
                return GUI.color;

            Color fadedColor = s_fadedOutAssetsColorField?.GetValue(null) is Color color
                ? color
                : Color.white;
            return MultiplyColor(GUI.color, fadedColor);
        }

        private static Color GetTintMultiplier(Color resolvedColor, Color guiColor)
        {
            return new Color(
                DivideColorChannel(resolvedColor.r, guiColor.r),
                DivideColorChannel(resolvedColor.g, guiColor.g),
                DivideColorChannel(resolvedColor.b, guiColor.b),
                DivideColorChannel(resolvedColor.a, guiColor.a));
        }

        private static float DivideColorChannel(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= 0.0001f ? value : value / divisor;
        }

        private static Color MultiplyColor(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }

        private static void HandleRuntimeFailure(Exception exception)
        {
            if (s_loggedRuntimeFailure)
                return;

            s_loggedRuntimeFailure = true;
            Debug.LogException(exception);
        }

        private static ProjectViewEnhancerMethodDetour CreateDetour(MethodInfo targetMethod, MethodInfo replacementMethod)
        {
            return targetMethod == null || replacementMethod == null
                ? null
                : new ProjectViewEnhancerMethodDetour(targetMethod, replacementMethod);
        }

        private static void ClearCaches()
        {
            s_folderTextColorInfoByInstanceId.Clear();
            s_cachedSettingsChangeStamp = -1;
            s_cachedRightPaneContextFrame = -1;
            s_cachedRightPaneContextClipRect = default;
            s_cachedRightPaneContextResolved = false;
            s_cachedRightPaneContextResult = false;
            s_cachedRightPaneContextRejectReason = string.Empty;
        }

        private static bool AreRectsApproximatelyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f
                && Mathf.Abs(a.y - b.y) < 0.01f
                && Mathf.Abs(a.width - b.width) < 0.01f
                && Mathf.Abs(a.height - b.height) < 0.01f;
        }

        private static TDelegate CreateStaticDelegate<TDelegate>(MethodInfo methodInfo)
            where TDelegate : class
        {
            if (methodInfo == null)
                return null;

            try
            {
                return Delegate.CreateDelegate(typeof(TDelegate), methodInfo, false) as TDelegate;
            }
            catch
            {
                return null;
            }
        }
    }
}
