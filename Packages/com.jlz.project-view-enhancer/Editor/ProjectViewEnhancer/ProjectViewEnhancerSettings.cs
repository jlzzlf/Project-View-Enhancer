using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [FilePath("ProjectSettings/ProjectViewEnhancerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ProjectViewEnhancerSettings : ScriptableSingleton<ProjectViewEnhancerSettings>
    {
        internal const string PackageName = "com.jlz.project-view-enhancer";
        internal const string PackageRootPath = "Packages/" + PackageName;
        internal const string LegacyExtractedIconsRootPath = "Assets/Game/Art/Editor/ProjectViewEnhancer/ExtractedUnityIcons";
        internal const string PackageExtractedIconsRootPath = PackageRootPath + "/Editor/Icons/ExtractedUnityIcons";

        [Serializable]
        public sealed class FolderVisualStyleOverride
        {
            public string folderPath = string.Empty;

            public bool useBackgroundColor;
            public Color backgroundColor = new Color(0.58f, 0.19f, 0.19f, 0.45f);

            public bool useIconColor;
            public Color iconColor = new Color(1f, 0.82f, 0.23f, 1f);

            public bool useNameColor;
            public Color nameColor = new Color(1f, 0.92f, 0.62f, 1f);

            public bool useNameFontStyle;
            public FontStyle nameFontStyle = FontStyle.Normal;

            public bool useRightPaneIconOverlay;
            public string rightPaneIconOverlayTexturePath = string.Empty;
        }

        public bool enabled = true;
        public bool showIndentGuides = true;
        public bool showHorizontalJoin = true;

        public float guideThickness = 1f;
        public float defaultGuideAlpha = 0.18f;
        public float guideLeftShift = 14.98101f;
        public bool useCustomGuideColor = false;
        public Color guideColor = Color.white;

        public bool highlightActiveSelectionPath = true;
        public Color activeSelectionGuideColor = new Color(1f, 0.78f, 0.20f, 0.95f);

        public bool enableAlternatingRowBackground = true;
        public Color alternatingRowEvenColor = new Color(1f, 1f, 1f, 0.00f);
        public Color alternatingRowOddColor = new Color(1f, 1f, 1f, 0.04f);

        public List<FolderVisualStyleOverride> folderVisualStyleOverrides = new();
        public List<string> twoColumnFolderSymbolOverlayPaths = new();

        [NonSerialized]
        private Dictionary<string, FolderVisualStyleOverride> _folderVisualStyleOverrideByPath;

        [NonSerialized]
        private HashSet<string> _legacyRightPaneIconOverlayPathSet;

        [NonSerialized]
        private bool _hasRightPaneIconOverlayInStyleOverrides;

        [NonSerialized]
        private int _changeStamp;

        public int ChangeStamp => _changeStamp;

        public void SaveAndRepaint()
        {
            InvalidateLookupCaches();
            _changeStamp++;
            Save(true);
            EditorApplication.RepaintProjectWindow();
        }

        public bool TryGetFolderVisualStyleOverride(string folderPath, out FolderVisualStyleOverride styleOverride)
        {
            styleOverride = null;

            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrEmpty(folderPath))
                return false;

            EnsureStyleOverrideLookup();
            return _folderVisualStyleOverrideByPath.TryGetValue(folderPath, out styleOverride);
        }

        public void SetFolderVisualStyleOverride(
            string folderPath,
            bool useBackgroundColor,
            Color backgroundColor,
            bool useIconColor,
            Color iconColor,
            bool useNameColor,
            Color nameColor,
            bool useNameFontStyle,
            FontStyle nameFontStyle)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrEmpty(folderPath))
                return;

            EnsureStyleOverrides();
            FolderVisualStyleOverride style = FindFolderVisualStyleOverrideEntry(folderPath);
            bool hasAnyStyle = useBackgroundColor || useIconColor || useNameColor || useNameFontStyle;
            if (!hasAnyStyle)
            {
                if (style == null)
                    return;

                style.useBackgroundColor = false;
                style.useIconColor = false;
                style.useNameColor = false;
                style.useNameFontStyle = false;

                if (!HasSupportedVisualStyle(style))
                    folderVisualStyleOverrides.Remove(style);

                SaveAndRepaint();
                return;
            }

            if (style == null)
            {
                style = new FolderVisualStyleOverride
                {
                    folderPath = folderPath
                };
                folderVisualStyleOverrides.Add(style);
            }

            style.useBackgroundColor = useBackgroundColor;
            style.backgroundColor = backgroundColor;
            style.useIconColor = useIconColor;
            style.iconColor = iconColor;
            style.useNameColor = useNameColor;
            style.nameColor = nameColor;
            style.useNameFontStyle = useNameFontStyle;
            style.nameFontStyle = nameFontStyle;

            SaveAndRepaint();
        }

        public bool ClearFolderVisualStyleOverride(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrEmpty(folderPath))
                return false;

            EnsureStyleOverrides();
            bool removed = false;
            for (int i = folderVisualStyleOverrides.Count - 1; i >= 0; i--)
            {
                FolderVisualStyleOverride entry = folderVisualStyleOverrides[i];
                if (entry == null)
                    continue;

                if (!string.Equals(entry.folderPath, folderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                folderVisualStyleOverrides.RemoveAt(i);
                removed = true;
            }

            EnsureTwoColumnFolderSymbolOverlayPaths();
            int legacyIndex = FindTwoColumnFolderSymbolOverlayIndex(folderPath);
            if (legacyIndex >= 0)
            {
                twoColumnFolderSymbolOverlayPaths.RemoveAt(legacyIndex);
                removed = true;
            }

            if (removed)
                SaveAndRepaint();

            return removed;
        }

        public void GetFolderRightPaneIconOverlaySettings(string folderPath, out bool enabled, out string textureAssetPath)
        {
            enabled = false;
            textureAssetPath = string.Empty;

            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrEmpty(folderPath))
                return;

            EnsureStyleOverrides();
            FolderVisualStyleOverride style = FindFolderVisualStyleOverrideEntry(folderPath);
            if (style != null && style.useRightPaneIconOverlay)
            {
                enabled = true;
                textureAssetPath = NormalizeAssetReferencePath(style.rightPaneIconOverlayTexturePath);
                return;
            }

            EnsureLegacyRightPaneIconOverlayLookup();
            enabled = _legacyRightPaneIconOverlayPathSet.Contains(folderPath);
        }

        public void SetFolderRightPaneIconOverlay(string folderPath, bool enabled, string textureAssetPath)
        {
            folderPath = NormalizeAssetPath(folderPath);
            textureAssetPath = NormalizeAssetReferencePath(textureAssetPath);
            if (string.IsNullOrEmpty(folderPath))
                return;

            EnsureStyleOverrides();
            FolderVisualStyleOverride style = FindFolderVisualStyleOverrideEntry(folderPath);
            if (style == null && !enabled)
            {
                EnsureTwoColumnFolderSymbolOverlayPaths();
                int legacyIndex = FindTwoColumnFolderSymbolOverlayIndex(folderPath);
                if (legacyIndex >= 0)
                {
                    twoColumnFolderSymbolOverlayPaths.RemoveAt(legacyIndex);
                    SaveAndRepaint();
                }

                return;
            }

            if (style == null)
            {
                style = new FolderVisualStyleOverride
                {
                    folderPath = folderPath
                };
                folderVisualStyleOverrides.Add(style);
            }

            style.useRightPaneIconOverlay = enabled;
            style.rightPaneIconOverlayTexturePath = enabled ? textureAssetPath : string.Empty;

            EnsureTwoColumnFolderSymbolOverlayPaths();
            int existingLegacyIndex = FindTwoColumnFolderSymbolOverlayIndex(folderPath);
            if (existingLegacyIndex >= 0)
                twoColumnFolderSymbolOverlayPaths.RemoveAt(existingLegacyIndex);

            if (!HasSupportedVisualStyle(style))
                folderVisualStyleOverrides.Remove(style);

            SaveAndRepaint();
        }

        public bool IsTwoColumnFolderSymbolOverlayEnabled(string folderPath)
        {
            GetFolderRightPaneIconOverlaySettings(folderPath, out bool enabled, out _);
            return enabled;
        }

        public bool HasAnyRightPaneIconOverlay()
        {
            EnsureStyleOverrideLookup();
            if (_hasRightPaneIconOverlayInStyleOverrides)
                return true;

            EnsureLegacyRightPaneIconOverlayLookup();
            return _legacyRightPaneIconOverlayPathSet.Count > 0;
        }

        public string GetTwoColumnFolderSymbolOverlayTexturePath(string folderPath)
        {
            GetFolderRightPaneIconOverlaySettings(folderPath, out _, out string textureAssetPath);
            return textureAssetPath;
        }

        public void SetTwoColumnFolderSymbolOverlayEnabled(string folderPath, bool enabled)
        {
            string textureAssetPath = GetTwoColumnFolderSymbolOverlayTexturePath(folderPath);
            SetFolderRightPaneIconOverlay(folderPath, enabled, textureAssetPath);
        }

        public void ResetToDefaults()
        {
            enabled = true;
            showIndentGuides = true;
            showHorizontalJoin = true;

            guideThickness = 1f;
            defaultGuideAlpha = 0.18f;
            guideLeftShift = 14.98101f;
            useCustomGuideColor = false;
            guideColor = Color.white;

            highlightActiveSelectionPath = true;
            activeSelectionGuideColor = new Color(1f, 0.78f, 0.20f, 0.95f);

            enableAlternatingRowBackground = true;
            alternatingRowEvenColor = new Color(1f, 1f, 1f, 0.00f);
            alternatingRowOddColor = new Color(1f, 1f, 1f, 0.04f);
            folderVisualStyleOverrides.Clear();
            twoColumnFolderSymbolOverlayPaths.Clear();

            SaveAndRepaint();
        }

        private void EnsureStyleOverrides()
        {
            folderVisualStyleOverrides ??= new List<FolderVisualStyleOverride>();
        }

        private void EnsureStyleOverrideLookup()
        {
            if (_folderVisualStyleOverrideByPath != null)
                return;

            EnsureStyleOverrides();
            _folderVisualStyleOverrideByPath = new Dictionary<string, FolderVisualStyleOverride>(StringComparer.OrdinalIgnoreCase);
            _hasRightPaneIconOverlayInStyleOverrides = false;
            for (int i = 0; i < folderVisualStyleOverrides.Count; i++)
            {
                FolderVisualStyleOverride entry = folderVisualStyleOverrides[i];
                if (entry == null || !HasSupportedVisualStyle(entry))
                    continue;

                string normalizedFolderPath = NormalizeAssetPath(entry.folderPath);
                if (string.IsNullOrEmpty(normalizedFolderPath))
                    continue;

                entry.folderPath = normalizedFolderPath;
                _folderVisualStyleOverrideByPath[normalizedFolderPath] = entry;
                _hasRightPaneIconOverlayInStyleOverrides |= entry.useRightPaneIconOverlay;
            }
        }

        private FolderVisualStyleOverride FindFolderVisualStyleOverrideEntry(string folderPath)
        {
            EnsureStyleOverrideLookup();
            _folderVisualStyleOverrideByPath.TryGetValue(folderPath, out FolderVisualStyleOverride entry);
            return entry;
        }

        private void EnsureTwoColumnFolderSymbolOverlayPaths()
        {
            twoColumnFolderSymbolOverlayPaths ??= new List<string>();
        }

        private void EnsureLegacyRightPaneIconOverlayLookup()
        {
            if (_legacyRightPaneIconOverlayPathSet != null)
                return;

            EnsureTwoColumnFolderSymbolOverlayPaths();
            _legacyRightPaneIconOverlayPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < twoColumnFolderSymbolOverlayPaths.Count; i++)
            {
                string entry = NormalizeAssetPath(twoColumnFolderSymbolOverlayPaths[i]);
                if (string.IsNullOrEmpty(entry))
                    continue;

                _legacyRightPaneIconOverlayPathSet.Add(entry);
            }
        }

        private int FindTwoColumnFolderSymbolOverlayIndex(string folderPath)
        {
            for (int i = 0; i < twoColumnFolderSymbolOverlayPaths.Count; i++)
            {
                string entry = twoColumnFolderSymbolOverlayPaths[i];
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                if (string.Equals(NormalizeAssetPath(entry), folderPath, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void InvalidateLookupCaches()
        {
            _folderVisualStyleOverrideByPath = null;
            _legacyRightPaneIconOverlayPathSet = null;
            _hasRightPaneIconOverlayInStyleOverrides = false;
        }

        private static bool HasSupportedVisualStyle(FolderVisualStyleOverride styleOverride)
        {
            if (styleOverride == null)
                return false;

            return styleOverride.useBackgroundColor
                || styleOverride.useIconColor
                || styleOverride.useNameColor
                || styleOverride.useNameFontStyle
                || styleOverride.useRightPaneIconOverlay;
        }

        internal static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.Replace("\\", "/").TrimEnd('/');
        }

        internal static string NormalizeAssetReferencePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalizedPath = path.Replace("\\", "/").Trim();
            if (normalizedPath.StartsWith(LegacyExtractedIconsRootPath, StringComparison.OrdinalIgnoreCase))
            {
                return PackageExtractedIconsRootPath + normalizedPath[LegacyExtractedIconsRootPath.Length..];
            }

            return normalizedPath;
        }
    }
}
