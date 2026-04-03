using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal readonly struct ProjectViewEnhancerVisualContext
    {
        public ProjectViewEnhancerVisualContext(string assetPath, bool isFolder, int rowIndex)
        {
            AssetPath = assetPath;
            IsFolder = isFolder;
            RowIndex = rowIndex;
        }

        public string AssetPath { get; }
        public bool IsFolder { get; }
        public int RowIndex { get; }
    }

    internal struct ProjectViewEnhancerVisualState
    {
        public bool UseAlternatingRowBackground;
        public Color AlternatingRowBackgroundColor;
        public bool UseBackgroundColor;
        public Color BackgroundColor;
        public bool UseIconColor;
        public Color IconColor;
        public bool UseNameColor;
        public Color NameColor;
        public bool UseNameFontStyle;
        public FontStyle NameFontStyle;

        public bool HasBackgroundOverride => UseAlternatingRowBackground || UseBackgroundColor;
        public bool HasContentOverride => UseIconColor || UseNameColor || UseNameFontStyle;
    }

    internal static class ProjectViewEnhancerVisualStyleResolver
    {
        public static ProjectViewEnhancerVisualState Resolve(ProjectViewEnhancerVisualContext context)
        {
            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;
            if (!settings.enabled)
                return default;

            ProjectViewEnhancerVisualState visualState = default;

            if (settings.enableAlternatingRowBackground && context.RowIndex >= 0)
            {
                Color alternatingColor = (context.RowIndex & 1) == 0
                    ? settings.alternatingRowEvenColor
                    : settings.alternatingRowOddColor;

                if (alternatingColor.a > 0.001f)
                {
                    visualState.UseAlternatingRowBackground = true;
                    visualState.AlternatingRowBackgroundColor = alternatingColor;
                }
            }

            if (!context.IsFolder)
                return visualState;

            if (!settings.TryGetFolderVisualStyleOverride(context.AssetPath, out ProjectViewEnhancerSettings.FolderVisualStyleOverride styleOverride))
                return visualState;

            if (styleOverride.useBackgroundColor && styleOverride.backgroundColor.a > 0.001f)
            {
                visualState.UseBackgroundColor = true;
                visualState.BackgroundColor = styleOverride.backgroundColor;
            }

            if (styleOverride.useIconColor && styleOverride.iconColor.a > 0.001f)
            {
                visualState.UseIconColor = true;
                visualState.IconColor = styleOverride.iconColor;
            }

            if (styleOverride.useNameColor)
            {
                visualState.UseNameColor = true;
                visualState.NameColor = styleOverride.nameColor;
            }

            if (styleOverride.useNameFontStyle)
            {
                visualState.UseNameFontStyle = true;
                visualState.NameFontStyle = styleOverride.nameFontStyle;
            }

            return visualState;
        }

        public static string NormalizeAssetPath(string path)
        {
            return ProjectViewEnhancerSettings.NormalizeAssetPath(path);
        }
    }
}
