using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    [InitializeOnLoad]
    internal static class ProjectViewEnhancerReflectionBootstrap
    {
        private static readonly ProjectViewEnhancerTreeViewVisualPatch s_treeViewPatch = new();
        private static readonly ProjectViewEnhancerObjectListAreaVisualPatch s_objectListAreaPatch = new();

        private static string s_lastError = string.Empty;
        private static bool s_environmentUnsupported;

        static ProjectViewEnhancerReflectionBootstrap()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= UninstallPatches;
            AssemblyReloadEvents.beforeAssemblyReload += UninstallPatches;

            EditorApplication.quitting -= UninstallPatches;
            EditorApplication.quitting += UninstallPatches;

            EditorApplication.projectWindowItemOnGUI -= TryInstallPatchesFromProjectWindow;
            EditorApplication.projectWindowItemOnGUI += TryInstallPatchesFromProjectWindow;

            SceneView.duringSceneGui -= TryInstallPatchesFromSceneView;
            SceneView.duringSceneGui += TryInstallPatchesFromSceneView;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= TryInstallPatchesFromInspector;
            UnityEditor.Editor.finishedDefaultHeaderGUI += TryInstallPatchesFromInspector;
        }

        internal static bool IsInstalled => s_treeViewPatch.IsInstalled || s_objectListAreaPatch.IsInstalled;
        internal static bool IsTreeViewInstalled => s_treeViewPatch.IsInstalled;
        internal static bool IsObjectListAreaInstalled => s_objectListAreaPatch.IsInstalled;
        internal static string LastError => s_lastError;

        private static void InstallPatches(bool hasGuiContext)
        {
            var errors = new List<string>();
            if (!TryValidateEnvironment(out string environmentError))
            {
                s_environmentUnsupported = true;
                s_lastError = environmentError;
                UnsubscribeInstallHooks();
                return;
            }

            if (hasGuiContext
                && !s_treeViewPatch.IsInstalled
                && !s_treeViewPatch.TryInstall(out string treeError)
                && !string.IsNullOrEmpty(treeError))
            {
                errors.Add(treeError);
            }

            if (!s_objectListAreaPatch.IsInstalled
                && !s_objectListAreaPatch.TryInstall(out string objectListError)
                && !string.IsNullOrEmpty(objectListError))
            {
                errors.Add(objectListError);
            }

            s_lastError = errors.Count == 0 ? string.Empty : string.Join("\n", errors);

            if (s_treeViewPatch.IsInstalled && s_objectListAreaPatch.IsInstalled)
                UnsubscribeInstallHooks();
        }

        private static void TryInstallPatchesFromProjectWindow(string guid, Rect selectionRect)
        {
            TryInstallPatchesIfNeeded(Event.current != null);
        }

        private static void TryInstallPatchesFromSceneView(SceneView sceneView)
        {
            TryInstallPatchesIfNeeded(Event.current != null);
        }

        private static void TryInstallPatchesFromInspector(UnityEditor.Editor editor)
        {
            TryInstallPatchesIfNeeded(Event.current != null);
        }

        private static void TryInstallPatchesIfNeeded(bool hasGuiContext)
        {
            if (s_environmentUnsupported)
            {
                UnsubscribeInstallHooks();
                return;
            }

            if (s_treeViewPatch.IsInstalled && s_objectListAreaPatch.IsInstalled)
            {
                UnsubscribeInstallHooks();
                return;
            }

            InstallPatches(hasGuiContext);
        }

        private static void UninstallPatches()
        {
            UnsubscribeInstallHooks();
            s_treeViewPatch.Uninstall();
            s_objectListAreaPatch.Uninstall();
            s_environmentUnsupported = false;
        }

        private static void UnsubscribeInstallHooks()
        {
            EditorApplication.projectWindowItemOnGUI -= TryInstallPatchesFromProjectWindow;
            SceneView.duringSceneGui -= TryInstallPatchesFromSceneView;
            UnityEditor.Editor.finishedDefaultHeaderGUI -= TryInstallPatchesFromInspector;
        }

        private static bool TryValidateEnvironment(out string error)
        {
            error = string.Empty;

            if (Application.platform != RuntimePlatform.WindowsEditor || IntPtr.Size != 8)
            {
                error = "Project View Enhancer reflection patch currently supports Windows x64 Unity Editor only.";
                return false;
            }

            return true;
        }
    }
}
