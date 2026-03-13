using System;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    public static class ProjectViewEnhancerFolderColorContextMenu
    {
        private const string MenuRoot = "Assets/Project View Enhancer/";
        private const string EditStyleMenuPath = MenuRoot + "Folder Style...";
        private const string ClearStyleMenuPath = MenuRoot + "Clear Folder Style";

        [MenuItem(EditStyleMenuPath, false, 2200)]
        private static void OpenStyleWindow()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
                return;

            ProjectViewEnhancerFolderStyleWindow.Open(folderPath);
        }

        [MenuItem(EditStyleMenuPath, true)]
        private static bool ValidateOpenStyleWindow()
        {
            return TryGetSelectedFolderPath(out _);
        }

        [MenuItem(ClearStyleMenuPath, false, 2201)]
        private static void ClearStyle()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
                return;

            ProjectViewEnhancerSettings.instance.ClearFolderVisualStyleOverride(folderPath);
        }

        [MenuItem(ClearStyleMenuPath, true)]
        private static bool ValidateClearStyle()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
                return false;

            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;
            return settings.TryGetFolderVisualStyleOverride(folderPath, out _)
                || settings.IsTwoColumnFolderSymbolOverlayEnabled(folderPath);
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = string.Empty;

            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length != 1)
                return false;

            string selectedPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
                return false;

            selectedPath = selectedPath.Replace("\\", "/").TrimEnd('/');
            if (!selectedPath.StartsWith("Assets", StringComparison.Ordinal))
                return false;

            folderPath = selectedPath;
            return true;
        }
    }
}
