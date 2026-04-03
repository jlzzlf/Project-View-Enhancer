using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [InitializeOnLoad]
    internal static class ProjectViewEnhancerTwoColumnFolderSymbolOverlay
    {
        private readonly struct FolderItemInfo
        {
            public FolderItemInfo(bool isFolder, string assetPath, string normalizedPath, string folderName)
            {
                IsFolder = isFolder;
                AssetPath = assetPath;
                NormalizedPath = normalizedPath;
                FolderName = folderName;
            }

            public bool IsFolder { get; }
            public string AssetPath { get; }
            public string NormalizedPath { get; }
            public string FolderName { get; }
        }

        private readonly struct FolderOverlayInfo
        {
            public FolderOverlayInfo(bool overlayEnabled, Texture2D symbolTexture, bool usingCustomTexture)
            {
                OverlayEnabled = overlayEnabled;
                SymbolTexture = symbolTexture;
                UsingCustomTexture = usingCustomTexture;
            }

            public bool OverlayEnabled { get; }
            public Texture2D SymbolTexture { get; }
            public bool UsingCustomTexture { get; }
        }

        private const float ListIconSize = 16f;
        private static readonly Color s_symbolTint = new(0.06f, 0.06f, 0.06f, 0.92f);
        private static readonly Vector2 s_listSymbolOffset = new(1.25f, 1.0f);
        private static readonly Vector2 s_gridSymbolOffset = new(10.0f, 12f);

        private static readonly Dictionary<string, FolderItemInfo> s_folderItemInfoByGuid = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, FolderOverlayInfo> s_folderOverlayInfoByGuid = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> s_builtInSymbolTextureByFolderName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> s_customSymbolTextureByAssetPath = new(StringComparer.OrdinalIgnoreCase);

        private static int s_cachedOverlayContextFrame = -1;
        private static bool s_cachedShouldDrawRightPane;
        private static bool s_cachedHasResolvedListMode;
        private static bool s_cachedIsListMode;
        private static int s_cachedSettingsChangeStamp = -1;

        static ProjectViewEnhancerTwoColumnFolderSymbolOverlay()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;

            EditorApplication.projectChanged -= ClearCache;
            EditorApplication.projectChanged += ClearCache;

            Undo.undoRedoPerformed -= ClearCache;
            Undo.undoRedoPerformed += ClearCache;
        }

        private static void ClearCache()
        {
            s_folderItemInfoByGuid.Clear();
            s_folderOverlayInfoByGuid.Clear();
            s_builtInSymbolTextureByFolderName.Clear();
            s_customSymbolTextureByAssetPath.Clear();
            s_cachedOverlayContextFrame = -1;
            s_cachedShouldDrawRightPane = false;
            s_cachedHasResolvedListMode = false;
            s_cachedIsListMode = false;
            s_cachedSettingsChangeStamp = -1;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.Repaint)
                return;

            if (selectionRect.width <= 0f || selectionRect.height <= 0f)
                return;

            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;
            if (!TryGetOverlayContext(settings, out bool isRightPane, out bool hasResolvedListMode, out bool cachedIsListMode))
                return;

            if (!isRightPane)
                return;

            if (!TryGetFolderItemInfo(guid, out FolderItemInfo folderItemInfo))
                return;

            if (!TryGetFolderOverlayInfo(settings, guid, folderItemInfo, out FolderOverlayInfo overlayInfo))
                return;

            bool isListMode = hasResolvedListMode
                ? cachedIsListMode
                : selectionRect.height <= EditorGUIUtility.singleLineHeight * 1.75f;
            Rect symbolRect = GetSymbolRect(selectionRect, isListMode);
            if (symbolRect.width <= 1f || symbolRect.height <= 1f)
                return;

            Color previousColor = GUI.color;
            GUI.color = overlayInfo.UsingCustomTexture ? Color.white : s_symbolTint;
            GUI.DrawTexture(symbolRect, overlayInfo.SymbolTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private static bool TryGetOverlayContext(
            ProjectViewEnhancerSettings settings,
            out bool isRightPane,
            out bool hasResolvedListMode,
            out bool isListMode)
        {
            int frameCount = Time.frameCount;
            if (frameCount != s_cachedOverlayContextFrame)
            {
                s_cachedOverlayContextFrame = frameCount;
                s_cachedShouldDrawRightPane = false;
                s_cachedHasResolvedListMode = false;
                s_cachedIsListMode = false;

                if (settings != null && settings.enabled && settings.HasAnyRightPaneIconOverlay())
                {
                    s_cachedShouldDrawRightPane = IsRightPaneItemSlow();
                    if (s_cachedShouldDrawRightPane)
                        s_cachedHasResolvedListMode = ProjectViewEnhancerProjectBrowserMode.TryGetTwoColumnRightPaneListMode(out s_cachedIsListMode);
                }
            }

            isRightPane = s_cachedShouldDrawRightPane;
            hasResolvedListMode = s_cachedHasResolvedListMode;
            isListMode = s_cachedIsListMode;
            return true;
        }

        private static bool TryGetFolderOverlayInfo(
            ProjectViewEnhancerSettings settings,
            string guid,
            in FolderItemInfo folderItemInfo,
            out FolderOverlayInfo overlayInfo)
        {
            int currentSettingsChangeStamp = settings != null ? settings.ChangeStamp : -1;
            if (currentSettingsChangeStamp != s_cachedSettingsChangeStamp)
            {
                s_folderOverlayInfoByGuid.Clear();
                s_cachedSettingsChangeStamp = currentSettingsChangeStamp;
            }

            if (s_folderOverlayInfoByGuid.TryGetValue(guid, out overlayInfo))
                return overlayInfo.OverlayEnabled && overlayInfo.SymbolTexture != null;

            settings.GetFolderRightPaneIconOverlaySettings(folderItemInfo.NormalizedPath, out bool overlayEnabled, out string customTextureAssetPath);
            if (!overlayEnabled)
            {
                overlayInfo = new FolderOverlayInfo(false, null, false);
                s_folderOverlayInfoByGuid[guid] = overlayInfo;
                return false;
            }

            Texture2D symbolTexture = GetSymbolTexture(folderItemInfo.FolderName, customTextureAssetPath, out bool usingCustomTexture);
            overlayInfo = new FolderOverlayInfo(overlayEnabled, symbolTexture, usingCustomTexture);
            s_folderOverlayInfoByGuid[guid] = overlayInfo;
            return symbolTexture != null;
        }

        private static bool IsRightPaneItemSlow()
        {
            if (!ProjectViewEnhancerProjectBrowserMode.IsTwoColumn())
                return false;

            if (!ProjectViewEnhancerProjectBrowserMode.TryGetTwoColumnPaneRects(out Rect treeViewRect, out Rect listAreaRect))
                return false;

            if (!ProjectViewEnhancerProjectBrowserMode.TryGetCurrentClipRect(out Rect clipRect))
                return false;

            float listOverlap = GetHorizontalOverlap(clipRect, listAreaRect);
            if (listOverlap <= 0.5f)
                return false;

            float treeOverlap = GetHorizontalOverlap(clipRect, treeViewRect);
            return listOverlap >= treeOverlap;
        }

        private static bool TryGetFolderItemInfo(string guid, out FolderItemInfo folderItemInfo)
        {
            if (s_folderItemInfoByGuid.TryGetValue(guid, out folderItemInfo))
                return folderItemInfo.IsFolder;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
            {
                folderItemInfo = new FolderItemInfo(false, string.Empty, string.Empty, string.Empty);
                s_folderItemInfoByGuid[guid] = folderItemInfo;
                return false;
            }

            string normalizedPath = ProjectViewEnhancerVisualStyleResolver.NormalizeAssetPath(assetPath);
            string folderName = GetFolderName(assetPath);
            folderItemInfo = new FolderItemInfo(true, assetPath, normalizedPath, folderName);
            s_folderItemInfoByGuid[guid] = folderItemInfo;
            return true;
        }

        private static Rect GetSymbolRect(Rect selectionRect, bool isListMode)
        {
            if (isListMode)
            {
                float iconSize = Mathf.Min(ListIconSize, Mathf.Max(12f, selectionRect.height - 2f));
                Rect iconRect = new(
                    selectionRect.x + 1f,
                    selectionRect.y + (selectionRect.height - iconSize) * 0.5f,
                    iconSize,
                    iconSize);

                float symbolSize = Mathf.Clamp(iconRect.width * 0.88f, 10f, 14f);
                return new Rect(
                    iconRect.center.x - symbolSize * 0.5f + s_listSymbolOffset.x,
                    iconRect.center.y - symbolSize * 0.5f + s_listSymbolOffset.y,
                    symbolSize,
                    symbolSize);
            }

            float labelHeight = Mathf.Max(16f, EditorGUIUtility.singleLineHeight + 4f);
            float iconAreaHeight = Mathf.Max(24f, selectionRect.height - labelHeight);
            float iconAreaWidth = Mathf.Max(24f, selectionRect.width - 8f);
            float symbolSizeGrid = Mathf.Clamp(Mathf.Min(iconAreaWidth, iconAreaHeight) * 0.44f, 16f, 34f);
            float centerY = selectionRect.yMin + iconAreaHeight * 0.48f;
            return new Rect(
                selectionRect.center.x - symbolSizeGrid * 0.5f + s_gridSymbolOffset.x,
                centerY - symbolSizeGrid * 0.5f + s_gridSymbolOffset.y,
                symbolSizeGrid,
                symbolSizeGrid);
        }

        private static Texture2D GetSymbolTexture(string folderName, string customTextureAssetPath, out bool usingCustomTexture)
        {
            Texture2D customTexture = GetCustomTexture(customTextureAssetPath);
            if (customTexture != null)
            {
                usingCustomTexture = true;
                return customTexture;
            }

            usingCustomTexture = false;
            return GetBuiltInSymbolTexture(folderName);
        }

        private static Texture2D GetBuiltInSymbolTexture(string folderName)
        {
            if (s_builtInSymbolTextureByFolderName.TryGetValue(folderName, out Texture2D cachedTexture))
                return cachedTexture;

            Texture2D texture = LoadBuiltInSymbolTexture(folderName);
            s_builtInSymbolTextureByFolderName[folderName] = texture;
            return texture;
        }

        private static Texture2D GetCustomTexture(string customTextureAssetPath)
        {
            customTextureAssetPath = ProjectViewEnhancerSettings.NormalizeAssetReferencePath(customTextureAssetPath);
            if (string.IsNullOrEmpty(customTextureAssetPath) ||
                !customTextureAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (s_customSymbolTextureByAssetPath.TryGetValue(customTextureAssetPath, out Texture2D cachedTexture))
                return cachedTexture;

            Texture2D customTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(customTextureAssetPath);
            s_customSymbolTextureByAssetPath[customTextureAssetPath] = customTexture;
            return customTexture;
        }

        private static Texture2D LoadBuiltInSymbolTexture(string folderName)
        {
            string normalizedFolderName = folderName.Trim().ToLowerInvariant();
            switch (normalizedFolderName)
            {
                case "animation":
                case "animations":
                    return LoadBuiltInIcon(typeof(AnimationClip), "AnimationClip Icon", "d_AnimationClip Icon");
                case "audio":
                case "sound":
                case "sounds":
                    return LoadBuiltInIcon(typeof(AudioClip), "AudioSource Icon", "AudioClip Icon", "d_AudioSource Icon");
                case "font":
                case "fonts":
                    return LoadBuiltInIcon(typeof(Font), "Font Icon", "TextAsset Icon", "d_Font Icon");
                case "material":
                case "materials":
                    return LoadBuiltInIcon(typeof(Material), "Material Icon", "d_Material Icon");
                case "mixer":
                case "mixers":
                    return LoadBuiltInIcon(
                        Type.GetType("UnityEngine.Audio.AudioMixer, UnityEngine.AudioModule"),
                        "AudioMixerController Icon",
                        "AudioMixerSnapshot Icon",
                        "d_AudioMixerController Icon");
                case "nav":
                case "navigation":
                    return LoadBuiltInIcon(
                        Type.GetType("UnityEngine.AI.NavMeshData, UnityEngine.AIModule"),
                        "NavMeshData Icon",
                        "d_NavMeshData Icon",
                        "BuildSettings.Editor.Small");
                case "net":
                case "network":
                    return LoadBuiltInIcon(null, "NetworkView Icon", "BuildSettings.Web.Small");
                case "physics":
                    return LoadBuiltInIcon(typeof(PhysicMaterial), "PhysicMaterial Icon", "Rigidbody Icon", "d_Rigidbody Icon");
                case "prefab":
                case "prefabs":
                    return LoadBuiltInIcon(typeof(GameObject), "Prefab Icon", "PrefabModel Icon", "d_Prefab Icon");
                case "scriptable":
                case "scriptables":
                case "scriptableobjects":
                    return LoadBuiltInIcon(typeof(ScriptableObject), "ScriptableObject Icon", "cs Script Icon", "d_ScriptableObject Icon");
                case "scene":
                case "scenes":
                    return LoadBuiltInIcon(typeof(SceneAsset), "SceneAsset Icon", "d_SceneAsset Icon");
                case "script":
                case "scripts":
                case "code":
                    return LoadBuiltInIcon(typeof(MonoScript), "cs Script Icon", "d_cs Script Icon");
                case "shader":
                case "shaders":
                    return LoadBuiltInIcon(typeof(Shader), "Shader Icon", "d_Shader Icon");
                case "sprite":
                case "sprites":
                    return LoadBuiltInIcon(typeof(Sprite), "Sprite Icon", "d_Sprite Icon");
                case "terrain":
                case "terrains":
                    return LoadBuiltInIcon(typeof(TerrainData), "Terrain Icon", "d_Terrain Icon");
                case "tile":
                case "tiles":
                case "tilemap":
                case "tilemaps":
                    return LoadBuiltInIcon(
                        Type.GetType("UnityEngine.Tilemaps.Tilemap, UnityEngine.TilemapModule"),
                        "Tilemap Icon",
                        "Grid Icon",
                        "d_Tilemap Icon");
                case "transform":
                case "transforms":
                    return LoadBuiltInIcon(typeof(Transform), "Transform Icon", "d_Transform Icon");
                default:
                    return LoadBuiltInIcon(typeof(ScriptableObject), "ScriptableObject Icon", "d_ScriptableObject Icon", "cs Script Icon");
            }
        }

        private static Texture2D LoadBuiltInIcon(Type fallbackType, params string[] iconNames)
        {
            if (iconNames != null)
            {
                for (int i = 0; i < iconNames.Length; i++)
                {
                    string iconName = iconNames[i];
                    if (string.IsNullOrEmpty(iconName))
                        continue;

                    GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
                    if (iconContent?.image is Texture2D iconTexture)
                        return iconTexture;

                    Texture2D namedTexture = EditorGUIUtility.FindTexture(iconName);
                    if (namedTexture != null)
                        return namedTexture;
                }
            }

            return fallbackType == null ? null : AssetPreview.GetMiniTypeThumbnail(fallbackType);
        }

        private static string GetFolderName(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex >= 0 ? assetPath[(separatorIndex + 1)..] : assetPath;
        }

        private static float GetHorizontalOverlap(Rect a, Rect b)
        {
            float min = Mathf.Max(a.xMin, b.xMin);
            float max = Mathf.Min(a.xMax, b.xMax);
            return Mathf.Max(0f, max - min);
        }

    }
}
