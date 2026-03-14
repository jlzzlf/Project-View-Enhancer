using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal sealed class ProjectViewEnhancerTreeViewVisualPatch
    {
        private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const float DefaultTreeLineHeight = 16f;
        private const float DefaultTreeIndentWidth = 14f;
        private const float DefaultFoldoutStyleWidth = 14f;
        private const float DefaultBaseIndent = 2f;

        private static readonly Type s_treeViewGUIType = Type.GetType("UnityEditor.IMGUI.Controls.TreeViewGUI, UnityEditor");
        private static readonly Type s_treeViewControllerType = Type.GetType("UnityEditor.IMGUI.Controls.TreeViewController, UnityEditor");
        private static readonly Type s_assetsTreeViewGUIType = Type.GetType("UnityEditor.AssetsTreeViewGUI, UnityEditor");
        private static readonly Type s_projectBrowserColumnOneTreeViewGUIType = Type.GetType("UnityEditor.ProjectBrowserColumnOneTreeViewGUI, UnityEditor");
        private static readonly Type s_treeViewStylesType = s_treeViewGUIType?.GetNestedType("Styles", StaticBindingFlags);
        private static readonly Type s_gameObjectStylesType = Type.GetType("UnityEditor.GameObjectTreeViewGUI+GameObjectStyles, UnityEditor");

        private static readonly MethodInfo s_targetDrawItemBackgroundMethod = s_treeViewGUIType?.GetMethod(
            "DrawItemBackground",
            InstanceBindingFlags,
            null,
            new[] { typeof(Rect), typeof(int), typeof(TreeViewItem), typeof(bool), typeof(bool) },
            null);

        private static readonly MethodInfo s_targetOnContentGuiMethod = s_treeViewGUIType?.GetMethod(
            "OnContentGUI",
            InstanceBindingFlags,
            null,
            new[]
            {
                typeof(Rect),
                typeof(int),
                typeof(TreeViewItem),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)
            },
            null);
        private static readonly MethodInfo s_targetGetLineHeightMethod =
            s_treeViewGUIType?.GetMethod("get_k_LineHeight", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetTopRowMarginMethod =
            s_treeViewGUIType?.GetMethod("get_topRowMargin", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetBottomRowMarginMethod =
            s_treeViewGUIType?.GetMethod("get_bottomRowMargin", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetHalfDropBetweenHeightMethod =
            s_treeViewGUIType?.GetMethod("get_halfDropBetweenHeight", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetIndentWidthMethod =
            s_treeViewGUIType?.GetMethod("get_indentWidth", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetFoldoutStyleWidthMethod =
            s_treeViewGUIType?.GetMethod("get_foldoutStyleWidth", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetIconLeftPaddingMethod =
            s_treeViewGUIType?.GetMethod("get_iconLeftPadding", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetIconRightPaddingMethod =
            s_treeViewGUIType?.GetMethod("get_iconRightPadding", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetIconTotalPaddingMethod =
            s_treeViewGUIType?.GetMethod("get_iconTotalPadding", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetExtraSpaceBeforeIconAndLabelMethod =
            s_treeViewGUIType?.GetMethod("get_extraSpaceBeforeIconAndLabel", InstanceBindingFlags);
        private static readonly MethodInfo s_targetGetFoldoutIndentMethod = s_treeViewGUIType?.GetMethod(
            "GetFoldoutIndent",
            InstanceBindingFlags,
            null,
            new[] { typeof(TreeViewItem) },
            null);
        private static readonly MethodInfo s_targetGetTopPixelOfRowMethod = s_treeViewGUIType?.GetMethod(
            "GetTopPixelOfRow",
            InstanceBindingFlags,
            null,
            new[] { typeof(int) },
            null);
        private static readonly MethodInfo s_targetGetContentIndentMethod = s_treeViewGUIType?.GetMethod(
            "GetContentIndent",
            InstanceBindingFlags,
            null,
            new[] { typeof(TreeViewItem) },
            null);
        private static readonly MethodInfo s_targetGetRowRectMethod = s_treeViewGUIType?.GetMethod(
            "GetRowRect",
            InstanceBindingFlags,
            null,
            new[] { typeof(int), typeof(float) },
            null);
        private static readonly MethodInfo s_targetGetTotalSizeMethod =
            s_treeViewGUIType?.GetMethod("GetTotalSize", InstanceBindingFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo s_targetGetFirstAndLastRowVisibleMethod = s_treeViewGUIType?.GetMethod(
            "GetFirstAndLastRowVisible",
            InstanceBindingFlags,
            null,
            new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() },
            null);

        private static readonly MethodInfo s_replacementDrawItemBackgroundMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(DrawItemBackgroundReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementOnContentGuiMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(OnContentGUIReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetLineHeightMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetLineHeightReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetTopRowMarginMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetTopRowMarginReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetBottomRowMarginMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetBottomRowMarginReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetHalfDropBetweenHeightMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetHalfDropBetweenHeightReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetIndentWidthMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetIndentWidthReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetFoldoutStyleWidthMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetFoldoutStyleWidthReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetIconLeftPaddingMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetIconLeftPaddingReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetIconRightPaddingMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetIconRightPaddingReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetIconTotalPaddingMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetIconTotalPaddingReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetExtraSpaceBeforeIconAndLabelMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetExtraSpaceBeforeIconAndLabelReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetFoldoutIndentMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetFoldoutIndentReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetTopPixelOfRowMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetTopPixelOfRowReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetContentIndentMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetContentIndentReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetRowRectMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetRowRectReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetTotalSizeMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetTotalSizeReplacement), StaticBindingFlags);
        private static readonly MethodInfo s_replacementGetFirstAndLastRowVisibleMethod = typeof(ProjectViewEnhancerTreeViewVisualPatch)
            .GetMethod(nameof(GetFirstAndLastRowVisibleReplacement), StaticBindingFlags);

        private static readonly ProjectViewEnhancerMethodDetour s_drawItemBackgroundDetour =
            CreateDetour(s_targetDrawItemBackgroundMethod, s_replacementDrawItemBackgroundMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_onContentGuiDetour =
            CreateDetour(s_targetOnContentGuiMethod, s_replacementOnContentGuiMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getLineHeightDetour =
            CreateDetour(s_targetGetLineHeightMethod, s_replacementGetLineHeightMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getTopRowMarginDetour =
            CreateDetour(s_targetGetTopRowMarginMethod, s_replacementGetTopRowMarginMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getBottomRowMarginDetour =
            CreateDetour(s_targetGetBottomRowMarginMethod, s_replacementGetBottomRowMarginMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getHalfDropBetweenHeightDetour =
            CreateDetour(s_targetGetHalfDropBetweenHeightMethod, s_replacementGetHalfDropBetweenHeightMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getIndentWidthDetour =
            CreateDetour(s_targetGetIndentWidthMethod, s_replacementGetIndentWidthMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getFoldoutStyleWidthDetour =
            CreateDetour(s_targetGetFoldoutStyleWidthMethod, s_replacementGetFoldoutStyleWidthMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getIconLeftPaddingDetour =
            CreateDetour(s_targetGetIconLeftPaddingMethod, s_replacementGetIconLeftPaddingMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getIconRightPaddingDetour =
            CreateDetour(s_targetGetIconRightPaddingMethod, s_replacementGetIconRightPaddingMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getIconTotalPaddingDetour =
            CreateDetour(s_targetGetIconTotalPaddingMethod, s_replacementGetIconTotalPaddingMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getExtraSpaceBeforeIconAndLabelDetour =
            CreateDetour(s_targetGetExtraSpaceBeforeIconAndLabelMethod, s_replacementGetExtraSpaceBeforeIconAndLabelMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getFoldoutIndentDetour =
            CreateDetour(s_targetGetFoldoutIndentMethod, s_replacementGetFoldoutIndentMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getTopPixelOfRowDetour =
            CreateDetour(s_targetGetTopPixelOfRowMethod, s_replacementGetTopPixelOfRowMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getContentIndentDetour =
            CreateDetour(s_targetGetContentIndentMethod, s_replacementGetContentIndentMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getRowRectDetour =
            CreateDetour(s_targetGetRowRectMethod, s_replacementGetRowRectMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getTotalSizeDetour =
            CreateDetour(s_targetGetTotalSizeMethod, s_replacementGetTotalSizeMethod);
        private static readonly ProjectViewEnhancerMethodDetour s_getFirstAndLastRowVisibleDetour =
            CreateDetour(s_targetGetFirstAndLastRowVisibleMethod, s_replacementGetFirstAndLastRowVisibleMethod);
        private static readonly ProjectViewEnhancerMethodDetour[] s_layoutDetours =
        {
            s_getLineHeightDetour,
            s_getTopRowMarginDetour,
            s_getBottomRowMarginDetour,
            s_getHalfDropBetweenHeightDetour,
            s_getIndentWidthDetour,
            s_getFoldoutStyleWidthDetour,
            s_getIconLeftPaddingDetour,
            s_getIconRightPaddingDetour,
            s_getIconTotalPaddingDetour,
            s_getExtraSpaceBeforeIconAndLabelDetour,
            s_getFoldoutIndentDetour,
            s_getTopPixelOfRowDetour,
            s_getContentIndentDetour,
            s_getRowRectDetour,
            s_getTotalSizeDetour,
            s_getFirstAndLastRowVisibleDetour
        };

        private static readonly MethodInfo s_getContentIndentMethod =
            s_treeViewGUIType?.GetMethod("GetContentIndent", InstanceBindingFlags, null, new[] { typeof(TreeViewItem) }, null);
        private static readonly MethodInfo s_getEffectiveIconMethod =
            s_treeViewGUIType?.GetMethod("GetEffectiveIcon", InstanceBindingFlags, null, new[] { typeof(TreeViewItem), typeof(bool), typeof(bool) }, null);
        private static readonly MethodInfo s_setLineStyleMethod =
            s_treeViewGUIType?.GetMethod("set_lineStyle", InstanceBindingFlags);

        private static readonly PropertyInfo s_iconLeftPaddingProperty =
            GetCompatibleProperty(s_treeViewGUIType, "iconLeftPadding", InstanceBindingFlags);
        private static readonly PropertyInfo s_iconTotalPaddingProperty =
            GetCompatibleProperty(s_treeViewGUIType, "iconTotalPadding", InstanceBindingFlags);
        private static readonly PropertyInfo s_extraSpaceBeforeIconAndLabelProperty =
            GetCompatibleProperty(s_treeViewGUIType, "extraSpaceBeforeIconAndLabel", InstanceBindingFlags);
        private static readonly PropertyInfo s_iconOverlayGuiProperty =
            GetCompatibleProperty(s_treeViewGUIType, "iconOverlayGUI", InstanceBindingFlags, IsOverlayGuiProperty);
        private static readonly PropertyInfo s_labelOverlayGuiProperty =
            GetCompatibleProperty(s_treeViewGUIType, "labelOverlayGUI", InstanceBindingFlags, IsOverlayGuiProperty);
        private static readonly PropertyInfo s_hoveredItemProperty =
            GetCompatibleProperty(s_treeViewControllerType, "hoveredItem", InstanceBindingFlags);
        private static readonly PropertyInfo s_treeStateProperty =
            GetCompatibleProperty(s_treeViewControllerType, "state", InstanceBindingFlags);
        private static readonly PropertyInfo s_treeVisibleRectProperty =
            GetCompatibleProperty(s_treeViewControllerType, "visibleRect", InstanceBindingFlags);
        private static readonly PropertyInfo s_treeDataProperty =
            GetCompatibleProperty(s_treeViewControllerType, "data", InstanceBindingFlags);

        private static readonly FieldInfo s_treeViewField =
            s_treeViewGUIType?.GetField("m_TreeView", InstanceBindingFlags);
        private static readonly FieldInfo s_lineHeightBackingField =
            s_treeViewGUIType?.GetField("m_LineHeight", InstanceBindingFlags);
        private static readonly FieldInfo s_topRowMarginField =
            s_treeViewGUIType?.GetField("k_TopRowMargin", InstanceBindingFlags);
        private static readonly FieldInfo s_bottomRowMarginField =
            s_treeViewGUIType?.GetField("k_BottomRowMargin", InstanceBindingFlags);
        private static readonly FieldInfo s_halfDropBetweenHeightField =
            s_treeViewGUIType?.GetField("k_HalfDropBetweenHeight", InstanceBindingFlags);
        private static readonly FieldInfo s_baseIndentField =
            s_treeViewGUIType?.GetField("k_BaseIndent", InstanceBindingFlags);
        private static readonly FieldInfo s_indentWidthBackingField =
            s_treeViewGUIType?.GetField("k_IndentWidth", InstanceBindingFlags);
        private static readonly FieldInfo s_iconWidthField =
            s_treeViewGUIType?.GetField("k_IconWidth", InstanceBindingFlags);
        private static readonly FieldInfo s_spaceBetweenIconAndTextField =
            s_treeViewGUIType?.GetField("k_SpaceBetweenIconAndText", InstanceBindingFlags);
        private static readonly FieldInfo s_iconLeftPaddingBackingField =
            s_treeViewGUIType?.GetField("<iconLeftPadding>k__BackingField", InstanceBindingFlags);
        private static readonly FieldInfo s_iconRightPaddingBackingField =
            s_treeViewGUIType?.GetField("<iconRightPadding>k__BackingField", InstanceBindingFlags);
        private static readonly FieldInfo s_extraSpaceBeforeIconAndLabelBackingField =
            s_treeViewGUIType?.GetField("<extraSpaceBeforeIconAndLabel>k__BackingField", InstanceBindingFlags);
        private static readonly FieldInfo s_foldoutStyleField =
            s_treeViewGUIType?.GetField("m_FoldoutStyle", InstanceBindingFlags);
        private static readonly FieldInfo s_lineStyleField =
            s_treeViewStylesType?.GetField("lineStyle", StaticBindingFlags);
        private static readonly FieldInfo s_lineBoldStyleField =
            s_treeViewStylesType?.GetField("lineBoldStyle", StaticBindingFlags);
        private static readonly FieldInfo s_hoveredBackgroundColorField =
            s_gameObjectStylesType?.GetField("hoveredBackgroundColor", StaticBindingFlags);
        private static readonly FieldInfo s_hoveredItemBackgroundStyleField =
            s_gameObjectStylesType?.GetField("hoveredItemBackgroundStyle", StaticBindingFlags);
        private static readonly Type s_treeViewStateType = Type.GetType("UnityEditor.IMGUI.Controls.TreeViewState, UnityEditor");
        private static readonly FieldInfo s_treeScrollPosField =
            s_treeViewStateType?.GetField("scrollPos", InstanceBindingFlags);
        private static readonly Type s_treeViewDataSourceType = Type.GetType("UnityEditor.IMGUI.Controls.ITreeViewDataSource, UnityEditor");
        private static readonly PropertyInfo s_treeDataSourceRowCountProperty =
            GetCompatibleProperty(s_treeViewDataSourceType, "rowCount", InstanceBindingFlags);
        private static readonly MethodInfo s_treeDataSourceGetRowsMethod =
            s_treeViewDataSourceType?.GetMethod("GetRows", InstanceBindingFlags, null, Type.EmptyTypes, null);
        private static readonly MethodInfo s_getMaxWidthMethod =
            s_treeViewGUIType?.GetMethod("GetMaxWidth", InstanceBindingFlags, null, new[] { typeof(IList<TreeViewItem>) }, null);

        private static readonly Dictionary<StyleCacheKey, GUIStyle> s_coloredStyleCache = new();
        private static readonly Dictionary<object, TreeScaleCacheEntry> s_treeScaleCache = new();

        private static bool s_loggedRuntimeFailure;

        private static PropertyInfo GetCompatibleProperty(Type type, string name, BindingFlags bindingFlags, Predicate<PropertyInfo> predicate = null)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            PropertyInfo property = FindDeclaredCompatibleProperty(type, name, bindingFlags, predicate);
            if (property != null)
                return property;

            if (!type.IsInterface)
                return null;

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                property = FindDeclaredCompatibleProperty(interfaces[i], name, bindingFlags, predicate);
                if (property != null)
                    return property;
            }

            return null;
        }

        private static PropertyInfo FindDeclaredCompatibleProperty(Type type, string name, BindingFlags bindingFlags, Predicate<PropertyInfo> predicate)
        {
            BindingFlags declaredBindingFlags = bindingFlags | BindingFlags.DeclaredOnly;
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                PropertyInfo[] properties = currentType.GetProperties(declaredBindingFlags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!string.Equals(property.Name, name, StringComparison.Ordinal))
                        continue;

                    if (property.GetIndexParameters().Length != 0)
                        continue;

                    if (predicate != null && !predicate(property))
                        continue;

                    return property;
                }
            }

            return null;
        }

        private static bool IsOverlayGuiProperty(PropertyInfo property)
        {
            return property != null
                && typeof(Delegate).IsAssignableFrom(property.PropertyType)
                && HasDelegateSignature(property.PropertyType, typeof(void), typeof(TreeViewItem), typeof(Rect));
        }

        private static bool HasDelegateSignature(Type delegateType, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo invokeMethod = delegateType?.GetMethod("Invoke", InstanceBindingFlags);
            if (invokeMethod == null || invokeMethod.ReturnType != returnType)
                return false;

            ParameterInfo[] parameters = invokeMethod.GetParameters();
            if (parameters.Length != parameterTypes.Length)
                return false;

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i])
                    return false;
            }

            return true;
        }

        private static bool TryInvokeOverlayGui(PropertyInfo property, object instance, TreeViewItem item, Rect rect)
        {
            if (property == null || instance == null)
                return false;

            if (property.GetValue(instance, null) is not Delegate overlayDelegate)
                return false;

            if (!TryCreateOverlayAction(overlayDelegate, out Action<TreeViewItem, Rect> overlayAction))
                return false;

            overlayAction.Invoke(item, rect);
            return true;
        }

        private static bool TryCreateOverlayAction(Delegate sourceDelegate, out Action<TreeViewItem, Rect> action)
        {
            action = null;
            if (sourceDelegate == null)
                return false;

            if (sourceDelegate is Action<TreeViewItem, Rect> typedAction)
            {
                action = typedAction;
                return true;
            }

            Delegate[] invocationList = sourceDelegate.GetInvocationList();
            Action<TreeViewItem, Rect> combinedAction = null;
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (!TryCreateSingleOverlayAction(invocationList[i], out Action<TreeViewItem, Rect> invocationAction))
                    return false;

                combinedAction += invocationAction;
            }

            action = combinedAction;
            return action != null;
        }

        private static bool TryCreateSingleOverlayAction(Delegate sourceDelegate, out Action<TreeViewItem, Rect> action)
        {
            action = null;
            if (sourceDelegate == null || !HasDelegateSignature(sourceDelegate.GetType(), typeof(void), typeof(TreeViewItem), typeof(Rect)))
                return false;

            if (sourceDelegate is Action<TreeViewItem, Rect> typedAction)
            {
                action = typedAction;
                return true;
            }

            try
            {
                action = sourceDelegate.Target == null
                    ? (Action<TreeViewItem, Rect>)Delegate.CreateDelegate(typeof(Action<TreeViewItem, Rect>), sourceDelegate.Method, false)
                    : (Action<TreeViewItem, Rect>)Delegate.CreateDelegate(typeof(Action<TreeViewItem, Rect>), sourceDelegate.Target, sourceDelegate.Method, false);
            }
            catch (ArgumentException)
            {
                action = null;
            }

            return action != null;
        }

        public bool IsInstalled { get; private set; }

        private readonly struct ReflectionContext
        {
            public ReflectionContext(
                object instance,
                Rect rect,
                int row,
                TreeViewItem item,
                string label,
                bool selected,
                bool focused,
                bool useBoldFont,
                bool isPinging,
                string assetPath,
                bool isFolder)
            {
                Instance = instance;
                Rect = rect;
                Row = row;
                Item = item;
                Label = label;
                Selected = selected;
                Focused = focused;
                UseBoldFont = useBoldFont;
                IsPinging = isPinging;
                AssetPath = assetPath;
                IsFolder = isFolder;
            }

            public object Instance { get; }
            public Rect Rect { get; }
            public int Row { get; }
            public TreeViewItem Item { get; }
            public string Label { get; }
            public bool Selected { get; }
            public bool Focused { get; }
            public bool UseBoldFont { get; }
            public bool IsPinging { get; }
            public string AssetPath { get; }
            public bool IsFolder { get; }
        }

        private readonly struct StyleCacheKey : IEquatable<StyleCacheKey>
        {
            public StyleCacheKey(
                bool useBoldBaseStyle,
                bool useNameColor,
                Color nameColor,
                bool useNameFontStyle,
                FontStyle nameFontStyle,
                int fontSizeOverride)
            {
                UseBoldBaseStyle = useBoldBaseStyle;
                UseNameColor = useNameColor;
                NameColor = (Color32)nameColor;
                UseNameFontStyle = useNameFontStyle;
                NameFontStyle = nameFontStyle;
                FontSizeOverride = fontSizeOverride;
            }

            public bool UseBoldBaseStyle { get; }
            public bool UseNameColor { get; }
            public Color32 NameColor { get; }
            public bool UseNameFontStyle { get; }
            public FontStyle NameFontStyle { get; }
            public int FontSizeOverride { get; }

            public bool Equals(StyleCacheKey other)
            {
                return UseBoldBaseStyle == other.UseBoldBaseStyle
                    && UseNameColor == other.UseNameColor
                    && NameColor.Equals(other.NameColor)
                    && UseNameFontStyle == other.UseNameFontStyle
                    && NameFontStyle == other.NameFontStyle
                    && FontSizeOverride == other.FontSizeOverride;
            }

            public override bool Equals(object obj)
            {
                return obj is StyleCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = UseBoldBaseStyle ? 1 : 0;
                    hashCode = (hashCode * 397) ^ (UseNameColor ? 1 : 0);
                    hashCode = (hashCode * 397) ^ NameColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ (UseNameFontStyle ? 1 : 0);
                    hashCode = (hashCode * 397) ^ (int)NameFontStyle;
                    hashCode = (hashCode * 397) ^ FontSizeOverride;
                    return hashCode;
                }
            }
        }

        private readonly struct TreeScaleCacheEntry
        {
            public TreeScaleCacheEntry(int frame, bool applies, float scale)
            {
                Frame = frame;
                Applies = applies;
                Scale = scale;
            }

            public int Frame { get; }
            public bool Applies { get; }
            public float Scale { get; }
        }

        public bool TryInstall(out string error)
        {
            error = string.Empty;

            if (IsInstalled)
                return true;

            if (!TryValidateEnvironment(out error))
                return false;

            try
            {
                InstallDetours(s_layoutDetours);
                try
                {
                    s_drawItemBackgroundDetour.Install();
                    s_onContentGuiDetour.Install();
                }
                catch
                {
                    s_drawItemBackgroundDetour.Uninstall();
                    UninstallDetours(s_layoutDetours);
                    throw;
                }

                s_coloredStyleCache.Clear();
                s_treeScaleCache.Clear();
                s_loggedRuntimeFailure = false;
                IsInstalled = true;
                return true;
            }
            catch (Exception exception)
            {
                error = $"TreeView reflection patch install failed: {exception.Message}";
                Debug.LogException(exception);
                return false;
            }
        }

        public void Uninstall()
        {
            if (!IsInstalled)
                return;

            try
            {
                s_onContentGuiDetour?.Uninstall();
                s_drawItemBackgroundDetour?.Uninstall();
                UninstallDetours(s_layoutDetours);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                IsInstalled = false;
                s_coloredStyleCache.Clear();
                s_treeScaleCache.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DrawItemBackgroundReplacement(
            object instance,
            Rect rect,
            int row,
            TreeViewItem item,
            bool selected,
            bool focused)
        {
            try
            {
                DrawItemBackgroundCore(instance, rect, row, item, selected, focused);
            }
            catch (Exception exception)
            {
                HandleRuntimeFailure(exception);

                try
                {
                    DrawNativeHoverBackground(instance, rect, item);
                }
                catch
                {
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnContentGUIReplacement(
            object instance,
            Rect rect,
            int row,
            TreeViewItem item,
            string label,
            bool selected,
            bool focused,
            bool useBoldFont,
            bool isPinging)
        {
            try
            {
                DrawItemContent(instance, rect, row, item, label, selected, focused, useBoldFont, isPinging);
            }
            catch (Exception exception)
            {
                HandleRuntimeFailure(exception);

                try
                {
                    DrawItemContent(instance, rect, row, item, label, selected, focused, useBoldFont, isPinging, true);
                }
                catch
                {
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetLineHeightReplacement(object instance)
        {
            return GetScaledLineHeight(instance);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetTopRowMarginReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseTopRowMargin(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetBottomRowMarginReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseBottomRowMargin(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetHalfDropBetweenHeightReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseHalfDropBetweenHeight(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetIndentWidthReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseIndentWidth(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetFoldoutStyleWidthReplacement(object instance)
        {
            float baseWidth = DefaultFoldoutStyleWidth;
            if (s_foldoutStyleField?.GetValue(instance) is GUIStyle foldoutStyle)
                baseWidth = foldoutStyle.fixedWidth > 0f ? foldoutStyle.fixedWidth : DefaultFoldoutStyleWidth;

            if (!TryGetProjectTreeScale(instance, out float scale))
                return baseWidth;

            return baseWidth * scale;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetIconLeftPaddingReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseIconLeftPadding(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetIconRightPaddingReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseIconRightPadding(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetIconTotalPaddingReplacement(object instance)
        {
            return GetIconLeftPaddingReplacement(instance) + GetIconRightPaddingReplacement(instance);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetExtraSpaceBeforeIconAndLabelReplacement(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseExtraSpaceBeforeIconAndLabel(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetFoldoutIndentReplacement(object instance, TreeViewItem item)
        {
            float baseIndent = GetBaseIndent(instance);
            if (item == null)
                return baseIndent;

            return baseIndent + (Mathf.Max(0, item.depth) * GetIndentWidthReplacement(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetTopPixelOfRowReplacement(object instance, int row)
        {
            return GetTopRowMarginReplacement(instance) + (row * GetLineHeightReplacement(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetContentIndentReplacement(object instance, TreeViewItem item)
        {
            return GetFoldoutIndentReplacement(instance, item) + GetFoldoutStyleWidthReplacement(instance);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Rect GetRowRectReplacement(object instance, int row, float rowWidth)
        {
            return new Rect(0f, GetTopPixelOfRowReplacement(instance, row), rowWidth, GetLineHeightReplacement(instance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Vector2 GetTotalSizeReplacement(object instance)
        {
            try
            {
                object treeView = s_treeViewField.GetValue(instance);
                int rowCount = GetVisibleRowCount(treeView);
                float width = GetTreeContentWidth(instance, treeView);
                float height = GetTopRowMarginReplacement(instance)
                    + (rowCount * GetLineHeightReplacement(instance))
                    + GetBottomRowMarginReplacement(instance);
                return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            }
            catch (Exception exception)
            {
                HandleRuntimeFailure(exception);
                return new Vector2(1f, 1f);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetFirstAndLastRowVisibleReplacement(object instance, ref int firstRowVisible, ref int lastRowVisible)
        {
            try
            {
                object treeView = s_treeViewField.GetValue(instance);
                if (treeView == null)
                {
                    firstRowVisible = -1;
                    lastRowVisible = -1;
                    return;
                }

                int rowCount = GetVisibleRowCount(treeView);
                if (rowCount <= 0)
                {
                    firstRowVisible = -1;
                    lastRowVisible = -1;
                    return;
                }

                Rect visibleRect = s_treeVisibleRectProperty.GetValue(treeView, null) is Rect rect ? rect : default;
                Vector2 scrollPos = GetTreeScrollPosition(treeView);
                float topMargin = GetTopRowMarginReplacement(instance);
                float lineHeight = Mathf.Max(1f, GetLineHeightReplacement(instance));
                float topPixel = Mathf.Max(0f, scrollPos.y - topMargin);
                float bottomPixel = Mathf.Max(topPixel, topPixel + visibleRect.height);

                firstRowVisible = Mathf.Clamp(Mathf.FloorToInt(topPixel / lineHeight), 0, rowCount - 1);
                lastRowVisible = Mathf.Clamp(Mathf.CeilToInt(bottomPixel / lineHeight), firstRowVisible, rowCount - 1);
            }
            catch (Exception exception)
            {
                HandleRuntimeFailure(exception);
                firstRowVisible = -1;
                lastRowVisible = -1;
            }
        }

        private static bool TryValidateEnvironment(out string error)
        {
            error = string.Empty;

            if (s_treeViewGUIType == null
                || s_treeViewControllerType == null
                || s_treeViewStylesType == null
                || s_gameObjectStylesType == null
                || s_targetDrawItemBackgroundMethod == null
                || s_targetOnContentGuiMethod == null
                || s_targetGetLineHeightMethod == null
                || s_targetGetTopRowMarginMethod == null
                || s_targetGetBottomRowMarginMethod == null
                || s_targetGetHalfDropBetweenHeightMethod == null
                || s_targetGetIndentWidthMethod == null
                || s_targetGetFoldoutStyleWidthMethod == null
                || s_targetGetIconLeftPaddingMethod == null
                || s_targetGetIconRightPaddingMethod == null
                || s_targetGetIconTotalPaddingMethod == null
                || s_targetGetExtraSpaceBeforeIconAndLabelMethod == null
                || s_targetGetFoldoutIndentMethod == null
                || s_targetGetTopPixelOfRowMethod == null
                || s_targetGetContentIndentMethod == null
                || s_targetGetRowRectMethod == null
                || s_targetGetTotalSizeMethod == null
                || s_targetGetFirstAndLastRowVisibleMethod == null
                || s_replacementDrawItemBackgroundMethod == null
                || s_replacementOnContentGuiMethod == null
                || s_replacementGetLineHeightMethod == null
                || s_replacementGetTopRowMarginMethod == null
                || s_replacementGetBottomRowMarginMethod == null
                || s_replacementGetHalfDropBetweenHeightMethod == null
                || s_replacementGetIndentWidthMethod == null
                || s_replacementGetFoldoutStyleWidthMethod == null
                || s_replacementGetIconLeftPaddingMethod == null
                || s_replacementGetIconRightPaddingMethod == null
                || s_replacementGetIconTotalPaddingMethod == null
                || s_replacementGetExtraSpaceBeforeIconAndLabelMethod == null
                || s_replacementGetFoldoutIndentMethod == null
                || s_replacementGetTopPixelOfRowMethod == null
                || s_replacementGetContentIndentMethod == null
                || s_replacementGetRowRectMethod == null
                || s_replacementGetTotalSizeMethod == null
                || s_replacementGetFirstAndLastRowVisibleMethod == null
                || s_drawItemBackgroundDetour == null
                || s_onContentGuiDetour == null
                || s_getLineHeightDetour == null
                || s_getTopRowMarginDetour == null
                || s_getBottomRowMarginDetour == null
                || s_getHalfDropBetweenHeightDetour == null
                || s_getIndentWidthDetour == null
                || s_getFoldoutStyleWidthDetour == null
                || s_getIconLeftPaddingDetour == null
                || s_getIconRightPaddingDetour == null
                || s_getIconTotalPaddingDetour == null
                || s_getExtraSpaceBeforeIconAndLabelDetour == null
                || s_getFoldoutIndentDetour == null
                || s_getTopPixelOfRowDetour == null
                || s_getContentIndentDetour == null
                || s_getRowRectDetour == null
                || s_getTotalSizeDetour == null
                || s_getFirstAndLastRowVisibleDetour == null
                || s_getContentIndentMethod == null
                || s_getEffectiveIconMethod == null
                || s_getMaxWidthMethod == null
                || s_setLineStyleMethod == null
                || s_iconLeftPaddingProperty == null
                || s_iconTotalPaddingProperty == null
                || s_extraSpaceBeforeIconAndLabelProperty == null
                || s_iconOverlayGuiProperty == null
                || s_labelOverlayGuiProperty == null
                || s_hoveredItemProperty == null
                || s_treeStateProperty == null
                || s_treeVisibleRectProperty == null
                || s_treeDataProperty == null
                || s_treeViewField == null
                || s_lineHeightBackingField == null
                || s_topRowMarginField == null
                || s_bottomRowMarginField == null
                || s_halfDropBetweenHeightField == null
                || s_baseIndentField == null
                || s_indentWidthBackingField == null
                || s_iconWidthField == null
                || s_spaceBetweenIconAndTextField == null
                || s_iconLeftPaddingBackingField == null
                || s_iconRightPaddingBackingField == null
                || s_extraSpaceBeforeIconAndLabelBackingField == null
                || s_foldoutStyleField == null
                || s_lineStyleField == null
                || s_lineBoldStyleField == null
                || s_hoveredBackgroundColorField == null
                || s_hoveredItemBackgroundStyleField == null
                || s_treeViewStateType == null
                || s_treeScrollPosField == null
                || s_treeViewDataSourceType == null
                || s_treeDataSourceRowCountProperty == null
                || s_treeDataSourceGetRowsMethod == null)
            {
                error = "TreeViewGUI internals could not be resolved. One-column and two-column folder trees will stay native.";
                return false;
            }

            return true;
        }

        private static void DrawItemBackgroundCore(
            object instance,
            Rect rect,
            int row,
            TreeViewItem item,
            bool selected,
            bool focused)
        {
            if (TryCreateContext(instance, rect, row, item, string.Empty, selected, focused, false, false, out ReflectionContext context))
            {
                ProjectViewEnhancerVisualState visualState = ResolveVisualState(context);
                DrawCustomRowBackground(rect, visualState);
                DrawNativeHoverBackground(instance, rect, item);
                return;
            }

            DrawNativeHoverBackground(instance, rect, item);
        }

        private static void DrawTreeGuides(ReflectionContext context)
        {
            if (!TryGetTreeGuideInfo(
                    context.Instance,
                    context.Item,
                    context.Row,
                    out int depth,
                    out int nextRowDepth,
                    out float indentWidth,
                    out bool isAssetTree,
                    out bool isTwoColumn))
                return;

            float currentGuideColumnX = GetGuideColumnCenterX(context.Instance, context.Item);
            float currentJoinWidth = GetGuideHorizontalJoinWidth(context.Instance, context.Item, currentGuideColumnX);
            string selectedPath = ProjectViewEnhancerGuideDrawer.GetReflectionGuideSelectedPath(isAssetTree, isTwoColumn);

            ProjectViewEnhancerGuideDrawer.DrawReflectionTreeGuides(
                context.Rect,
                currentGuideColumnX,
                currentJoinWidth,
                context.AssetPath,
                selectedPath,
                depth,
                nextRowDepth,
                indentWidth);
        }

        private static float GetGuideColumnCenterX(object instance, TreeViewItem item)
        {
            float foldoutIndent = Mathf.Max(0f, GetFoldoutIndentReplacement(instance, item));
            float foldoutWidth = Mathf.Max(1f, GetFoldoutStyleWidthReplacement(instance));
            float arrowVisualOffset = GetGuideArrowVisualOffset(instance, foldoutWidth);
            float scaleCompensationOffset = GetGuideScaleCompensationOffset(instance, foldoutWidth);
            return foldoutIndent + (foldoutWidth * 0.5f) - arrowVisualOffset - scaleCompensationOffset;
        }

        private static float GetGuideHorizontalJoinWidth(object instance, TreeViewItem item, float guideColumnCenterX)
        {
            float contentIndent = GetContentIndentReplacement(instance, item);
            float extraSpaceBeforeIconAndLabel = GetExtraSpaceBeforeIconAndLabelReplacement(instance);
            float iconLeftPadding = GetIconLeftPaddingReplacement(instance);
            float joinEndX = contentIndent + extraSpaceBeforeIconAndLabel + iconLeftPadding;
            float indentWidth = Mathf.Max(1f, GetIndentWidthReplacement(instance));
            float rightTrim = Mathf.Max(1f, indentWidth * 1f);
            return Mathf.Clamp(joinEndX - guideColumnCenterX - rightTrim, 1f, indentWidth);
        }

        private static float GetGuideArrowVisualOffset(object instance, float foldoutWidth)
        {
            float visualOffset = foldoutWidth * 1.05f;

            if (s_foldoutStyleField?.GetValue(instance) is GUIStyle foldoutStyle)
            {
                float contentOffsetX = Mathf.Abs(foldoutStyle.contentOffset.x);
                float paddingBias = Mathf.Max(0f, foldoutStyle.padding.left - foldoutStyle.padding.right) * 0.25f;
                visualOffset += contentOffsetX + paddingBias;
            }

            return Mathf.Clamp(visualOffset, 1f, foldoutWidth * 1.05f);
        }

        private static float GetGuideScaleCompensationOffset(object instance, float scaledFoldoutWidth)
        {
            if (!TryGetProjectTreeScale(instance, out float scale) || scale <= 1.001f)
                return 0f;

            float baseFoldoutWidth = scaledFoldoutWidth / scale;
            float foldoutWidthGrowth = Mathf.Max(0f, scaledFoldoutWidth - baseFoldoutWidth);
            return foldoutWidthGrowth * 0.5f;
        }

        private static void DrawItemContent(
            object instance,
            Rect rect,
            int row,
            TreeViewItem item,
            string label,
            bool selected,
            bool focused,
            bool useBoldFont,
            bool isPinging,
            bool forceDefaultStyle = false)
        {
            if (instance == null || Event.current.rawType != EventType.Repaint)
                return;

            TryCreateContext(
                instance,
                rect,
                row,
                item,
                label,
                selected,
                focused,
                useBoldFont,
                isPinging,
                out ReflectionContext context);

            ProjectViewEnhancerVisualState visualState = ResolveVisualState(context);
            bool applyTreeScale = TryGetProjectTreeScale(instance, out float treeScale);

            float iconWidth = Convert.ToSingle(s_iconWidthField.GetValue(instance));
            float iconLeftPadding = Convert.ToSingle(s_iconLeftPaddingProperty.GetValue(instance, null));
            float iconTotalPadding = Convert.ToSingle(s_iconTotalPaddingProperty.GetValue(instance, null));
            float extraSpaceBeforeIconAndLabel = Convert.ToSingle(s_extraSpaceBeforeIconAndLabelProperty.GetValue(instance, null));
            float spaceBetweenIconAndText = Convert.ToSingle(s_spaceBetweenIconAndTextField.GetValue(instance));

            Rect labelRect = rect;
            if (!isPinging)
            {
                float contentIndent = Convert.ToSingle(s_getContentIndentMethod.Invoke(instance, new object[] { item }));
                labelRect.xMin += contentIndent + extraSpaceBeforeIconAndLabel;
            }

            GUIStyle baseStyle = GetBaseLineStyle(useBoldFont);
            int fontSizeOverride = applyTreeScale
                ? ResolveScaledFontSize(baseStyle, treeScale)
                : 0;
            GUIStyle styleToUse = forceDefaultStyle
                ? baseStyle
                : ResolveLineStyle(useBoldFont, baseStyle, visualState, fontSizeOverride);
            s_setLineStyleMethod.Invoke(instance, new object[] { styleToUse });

            Rect iconRect = labelRect;
            float iconScale = applyTreeScale
                ? treeScale
                : 1f;
            float iconDrawSize = iconWidth * iconScale;
            iconRect.width = iconDrawSize;
            iconRect.height = iconDrawSize;
            iconRect.x = labelRect.x + iconLeftPadding - ((iconDrawSize - iconWidth) * 0.5f);
            iconRect.y = labelRect.y + ((labelRect.height - iconDrawSize) * 0.5f);

            Texture icon = s_getEffectiveIconMethod.Invoke(instance, new object[] { item, selected, focused }) as Texture;
            if (icon != null)
            {
                Color iconTint = ResolveIconTint(visualState);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true, 0f, iconTint, 0f, 0f);
            }

            Rect overlayRect = labelRect;
            overlayRect.width = iconDrawSize + iconTotalPadding;
            TryInvokeOverlayGui(s_iconOverlayGuiProperty, instance, item, overlayRect);

            if (icon != null)
                labelRect.xMin += iconDrawSize + iconTotalPadding + spaceBetweenIconAndText;

            styleToUse.Draw(labelRect, label, false, false, selected, focused);

            TryInvokeOverlayGui(s_labelOverlayGuiProperty, instance, item, labelRect);

            DrawTreeGuides(context);
        }

        private static bool TryCreateContext(
            object instance,
            Rect rect,
            int row,
            TreeViewItem item,
            string label,
            bool selected,
            bool focused,
            bool useBoldFont,
            bool isPinging,
            out ReflectionContext context)
        {
            context = default;

            if (instance == null || item == null || !IsSupportedTreeViewInstance(instance))
                return false;

            string assetPath = AssetDatabase.GetAssetPath(item.id);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            assetPath = ProjectViewEnhancerVisualStyleResolver.NormalizeAssetPath(assetPath);
            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
                return false;

            context = new ReflectionContext(
                instance,
                rect,
                row,
                item,
                label,
                selected,
                focused,
                useBoldFont,
                isPinging,
                assetPath,
                AssetDatabase.IsValidFolder(assetPath));
            return true;
        }

        private static bool TryGetTreeGuideInfo(
            object instance,
            TreeViewItem item,
            int row,
            out int depth,
            out int nextRowDepth,
            out float indentWidth,
            out bool isAssetTree,
            out bool isTwoColumn)
        {
            depth = -1;
            nextRowDepth = -1;
            indentWidth = DefaultTreeIndentWidth;
            isAssetTree = false;
            isTwoColumn = false;

            if (instance == null || item == null || s_treeViewField == null)
                return false;

            object treeView = s_treeViewField.GetValue(instance);
            if (!ProjectViewEnhancerProjectBrowserMode.TryGetTreeOwnerInfo(treeView, out ProjectViewEnhancerProjectBrowserMode.TreeOwnerInfo ownerInfo))
                return false;

            bool isSupportedGuideTree = !ownerInfo.IsTwoColumn
                ? ownerInfo.IsAssetTree
                : !ownerInfo.IsAssetTree;
            if (!isSupportedGuideTree)
                return false;

            isAssetTree = ownerInfo.IsAssetTree;
            isTwoColumn = ownerInfo.IsTwoColumn;
            depth = Mathf.Max(0, item.depth);
            nextRowDepth = GetNextVisibleRowDepth(treeView, row);
            indentWidth = Mathf.Max(1f, GetIndentWidthReplacement(instance));
            return true;
        }

        private static int GetNextVisibleRowDepth(object treeView, int row)
        {
            if (treeView == null || row < 0 || s_treeDataProperty == null || s_treeDataSourceGetRowsMethod == null)
                return -1;

            try
            {
                object dataSource = s_treeDataProperty.GetValue(treeView, null);
                if (dataSource == null)
                    return -1;

                if (s_treeDataSourceGetRowsMethod.Invoke(dataSource, null) is not IList rows)
                    return -1;

                int nextRowIndex = row + 1;
                if (nextRowIndex < 0 || nextRowIndex >= rows.Count)
                    return -1;

                return rows[nextRowIndex] is TreeViewItem nextItem
                    ? Mathf.Max(-1, nextItem.depth)
                    : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static bool IsSupportedTreeViewInstance(object instance)
        {
            return (s_assetsTreeViewGUIType != null && s_assetsTreeViewGUIType.IsInstanceOfType(instance))
                || (s_projectBrowserColumnOneTreeViewGUIType != null && s_projectBrowserColumnOneTreeViewGUIType.IsInstanceOfType(instance));
        }

        private static ProjectViewEnhancerVisualState ResolveVisualState(ReflectionContext context)
        {
            return ProjectViewEnhancerVisualStyleResolver.Resolve(
                new ProjectViewEnhancerVisualContext(context.AssetPath, context.IsFolder, context.Row));
        }

        private static void DrawCustomRowBackground(Rect rect, ProjectViewEnhancerVisualState visualState)
        {
            if (visualState.UseAlternatingRowBackground)
                EditorGUI.DrawRect(rect, visualState.AlternatingRowBackgroundColor);

            if (visualState.UseBackgroundColor)
                EditorGUI.DrawRect(rect, visualState.BackgroundColor);
        }

        private static void DrawNativeHoverBackground(object instance, Rect rect, TreeViewItem item)
        {
            if (instance == null || item == null)
                return;

            object treeView = s_treeViewField.GetValue(instance);
            if (treeView == null)
                return;

            if (s_hoveredItemProperty.GetValue(treeView, null) is not TreeViewItem hoveredItem || hoveredItem != item)
                return;

            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = (Color)s_hoveredBackgroundColorField.GetValue(null);
            GUI.Label(rect, GUIContent.none, s_hoveredItemBackgroundStyleField.GetValue(null) as GUIStyle);
            GUI.backgroundColor = previousBackgroundColor;
        }

        private static GUIStyle ResolveLineStyle(
            bool useBoldBaseStyle,
            GUIStyle baseStyle,
            ProjectViewEnhancerVisualState visualState,
            int fontSizeOverride = 0)
        {
            if (baseStyle == null)
                return EditorStyles.label;

            if (!visualState.UseNameColor && !visualState.UseNameFontStyle && fontSizeOverride <= 0)
                return baseStyle;

            StyleCacheKey cacheKey = new(
                useBoldBaseStyle,
                visualState.UseNameColor,
                visualState.NameColor,
                visualState.UseNameFontStyle,
                visualState.NameFontStyle,
                fontSizeOverride);
            if (s_coloredStyleCache.TryGetValue(cacheKey, out GUIStyle cachedStyle))
                return cachedStyle;

            GUIStyle resolvedStyle = new(baseStyle);
            if (visualState.UseNameFontStyle)
                resolvedStyle.fontStyle = visualState.NameFontStyle;

            if (visualState.UseNameColor)
                ApplyTextColor(resolvedStyle, visualState.NameColor);

            if (fontSizeOverride > 0)
                resolvedStyle.fontSize = fontSizeOverride;

            s_coloredStyleCache[cacheKey] = resolvedStyle;
            return resolvedStyle;
        }

        private static GUIStyle GetBaseLineStyle(bool useBoldFont)
        {
            FieldInfo styleField = useBoldFont ? s_lineBoldStyleField : s_lineStyleField;
            return styleField?.GetValue(null) as GUIStyle ?? EditorStyles.label;
        }

        private static Color ResolveIconTint(ProjectViewEnhancerVisualState visualState)
        {
            Color iconTint = visualState.UseIconColor ? visualState.IconColor : GUI.color;
            if (visualState.UseIconColor)
                iconTint.a *= GUI.color.a;

            if (!GUI.enabled)
                iconTint.a *= 0.5f;

            return iconTint;
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

        private static bool TryGetProjectTreeScale(object instance, out float scale)
        {
            scale = 1f;

            if (instance == null || s_treeViewField == null)
                return false;

            int currentFrame = Time.frameCount;
            if (s_treeScaleCache.TryGetValue(instance, out TreeScaleCacheEntry cachedEntry) && cachedEntry.Frame == currentFrame)
            {
                scale = cachedEntry.Scale;
                return cachedEntry.Applies;
            }

            object treeView = s_treeViewField.GetValue(instance);
            if (!ProjectViewEnhancerProjectBrowserMode.TryGetTreeOwnerInfo(treeView, out ProjectViewEnhancerProjectBrowserMode.TreeOwnerInfo ownerInfo))
            {
                s_treeScaleCache[instance] = new TreeScaleCacheEntry(currentFrame, false, 1f);
                return false;
            }

            bool shouldScaleTree = !ownerInfo.IsTwoColumn
                ? ownerInfo.IsAssetTree
                : !ownerInfo.IsAssetTree;
            if (!shouldScaleTree)
            {
                s_treeScaleCache[instance] = new TreeScaleCacheEntry(currentFrame, false, 1f);
                return false;
            }

            scale = Mathf.Max(1f, ProjectViewEnhancerTreeScaleState.GetEffectiveScale());
            bool applies = scale > 1.001f;
            s_treeScaleCache[instance] = new TreeScaleCacheEntry(currentFrame, applies, scale);
            return applies;
        }

        private static float GetScaledTreeMetric(object instance, float baseValue)
        {
            if (!TryGetProjectTreeScale(instance, out float scale))
                return baseValue;

            return baseValue * scale;
        }

        private static float GetScaledLineHeight(object instance)
        {
            return GetScaledTreeMetric(instance, GetBaseLineHeight(instance));
        }

        private static float GetFloatFieldValue(FieldInfo fieldInfo, object instance, float fallback = 0f)
        {
            if (fieldInfo == null || instance == null)
                return fallback;

            object value = fieldInfo.GetValue(instance);
            return value == null ? fallback : Convert.ToSingle(value);
        }

        private static float GetBaseLineHeight(object instance)
        {
            float fieldValue = GetFloatFieldValue(s_lineHeightBackingField, instance);
            if (fieldValue > 0.001f)
                return fieldValue;

            if (GetBaseLineStyle(false) is GUIStyle style && style.fixedHeight > 0.001f)
                return style.fixedHeight;

            return Mathf.Max(DefaultTreeLineHeight, EditorGUIUtility.singleLineHeight);
        }

        private static float GetBaseTopRowMargin(object instance)
        {
            return Mathf.Max(0f, GetFloatFieldValue(s_topRowMarginField, instance));
        }

        private static float GetBaseBottomRowMargin(object instance)
        {
            return Mathf.Max(0f, GetFloatFieldValue(s_bottomRowMarginField, instance));
        }

        private static float GetBaseHalfDropBetweenHeight(object instance)
        {
            float fieldValue = GetFloatFieldValue(s_halfDropBetweenHeightField, instance);
            if (fieldValue > 0.001f)
                return fieldValue;

            return GetBaseLineHeight(instance) * 0.5f;
        }

        private static float GetBaseIndentWidth(object instance)
        {
            float fieldValue = GetFloatFieldValue(s_indentWidthBackingField, instance);
            return fieldValue > 0.001f ? fieldValue : DefaultTreeIndentWidth;
        }

        private static float GetBaseIndent(object instance)
        {
            float fieldValue = GetFloatFieldValue(s_baseIndentField, instance);
            return fieldValue > 0.001f ? fieldValue : DefaultBaseIndent;
        }

        private static float GetBaseIconLeftPadding(object instance)
        {
            return Mathf.Max(0f, GetFloatFieldValue(s_iconLeftPaddingBackingField, instance));
        }

        private static float GetBaseIconRightPadding(object instance)
        {
            return Mathf.Max(0f, GetFloatFieldValue(s_iconRightPaddingBackingField, instance));
        }

        private static float GetBaseExtraSpaceBeforeIconAndLabel(object instance)
        {
            return Mathf.Max(0f, GetFloatFieldValue(s_extraSpaceBeforeIconAndLabelBackingField, instance));
        }

        private static Vector2 GetTreeScrollPosition(object treeView)
        {
            if (treeView == null || s_treeStateProperty == null || s_treeScrollPosField == null)
                return Vector2.zero;

            object state = s_treeStateProperty.GetValue(treeView, null);
            if (state == null)
                return Vector2.zero;

            return s_treeScrollPosField.GetValue(state) is Vector2 scrollPos ? scrollPos : Vector2.zero;
        }

        private static int GetVisibleRowCount(object treeView)
        {
            if (treeView == null || s_treeDataProperty == null || s_treeDataSourceRowCountProperty == null)
                return 0;

            object dataSource = s_treeDataProperty.GetValue(treeView, null);
            if (dataSource == null)
                return 0;

            object rowCountValue = s_treeDataSourceRowCountProperty.GetValue(dataSource, null);
            return rowCountValue == null ? 0 : Mathf.Max(0, Convert.ToInt32(rowCountValue));
        }

        private static float GetTreeContentWidth(object instance, object treeView)
        {
            float width = 1f;

            if (instance == null || treeView == null || s_treeDataProperty == null || s_treeDataSourceGetRowsMethod == null || s_getMaxWidthMethod == null)
                return width;

            object dataSource = s_treeDataProperty.GetValue(treeView, null);
            if (dataSource == null)
                return width;

            object rows = s_treeDataSourceGetRowsMethod.Invoke(dataSource, null);
            if (rows == null)
                return width;

            object maxWidthValue = s_getMaxWidthMethod.Invoke(instance, new[] { rows });
            if (maxWidthValue != null)
                width = Mathf.Max(1f, Convert.ToSingle(maxWidthValue));

            if (TryGetProjectTreeScale(instance, out float scale))
                width *= scale;

            return width;
        }

        private static int ResolveScaledFontSize(GUIStyle baseStyle, float scale)
        {
            if (scale <= 1f)
                return 0;

            int baseFontSize = baseStyle != null && baseStyle.fontSize > 0
                ? baseStyle.fontSize
                : 12;
            return Mathf.Max(baseFontSize, Mathf.RoundToInt(baseFontSize * scale));
        }

        private static void InstallDetours(ProjectViewEnhancerMethodDetour[] detours)
        {
            if (detours == null)
                return;

            var installedDetours = new List<ProjectViewEnhancerMethodDetour>(detours.Length);
            try
            {
                for (int i = 0; i < detours.Length; i++)
                {
                    ProjectViewEnhancerMethodDetour detour = detours[i];
                    if (detour == null)
                        continue;

                    detour.Install();
                    installedDetours.Add(detour);
                }
            }
            catch
            {
                for (int i = installedDetours.Count - 1; i >= 0; i--)
                    installedDetours[i].Uninstall();

                throw;
            }
        }

        private static void UninstallDetours(ProjectViewEnhancerMethodDetour[] detours)
        {
            if (detours == null)
                return;

            for (int i = detours.Length - 1; i >= 0; i--)
                detours[i]?.Uninstall();
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
    }
}
