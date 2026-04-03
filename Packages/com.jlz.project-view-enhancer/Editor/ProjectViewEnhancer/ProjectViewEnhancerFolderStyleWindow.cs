using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal sealed class ProjectViewEnhancerFolderStyleWindow : EditorWindow
    {
        private const int PresetColumnCount = 2;
        private const int PresetRowCount = 4;
        private const float PresetButtonSize = 18f;
        private const float PresetButtonSpacing = 4f;

        private static readonly Color[] s_iconColorPresets =
        {
            new(1.00f, 0.82f, 0.23f, 1f),
            new(0.98f, 0.57f, 0.20f, 1f),
            new(0.93f, 0.35f, 0.35f, 1f),
            new(0.78f, 0.33f, 0.85f, 1f),
            new(0.34f, 0.53f, 0.93f, 1f),
            new(0.23f, 0.73f, 0.88f, 1f),
            new(0.20f, 0.75f, 0.54f, 1f),
            new(0.55f, 0.78f, 0.26f, 1f)
        };

        private static readonly Color[] s_nameColorPresets =
        {
            new(1.00f, 0.94f, 0.72f, 1f),
            new(1.00f, 0.83f, 0.66f, 1f),
            new(1.00f, 0.75f, 0.75f, 1f),
            new(0.93f, 0.77f, 1.00f, 1f),
            new(0.76f, 0.87f, 1.00f, 1f),
            new(0.72f, 0.95f, 1.00f, 1f),
            new(0.74f, 0.96f, 0.83f, 1f),
            new(0.86f, 0.96f, 0.69f, 1f)
        };

        private string _folderPath = string.Empty;

        private bool _useBackgroundColor;
        private Color _backgroundColor = new(0.58f, 0.19f, 0.19f, 0.45f);

        private bool _useIconColor;
        private Color _iconColor = new(1f, 0.82f, 0.23f, 1f);

        private bool _useNameColor;
        private Color _nameColor = new(1f, 0.92f, 0.62f, 1f);

        private bool _useNameFontStyle;
        private FontStyle _nameFontStyle = FontStyle.Bold;

        private bool _useRightPaneIconOverlay;
        private Texture2D _rightPaneIconOverlayTexture;

        public static void Open(string folderPath)
        {
            var window = CreateInstance<ProjectViewEnhancerFolderStyleWindow>();
            window.titleContent = new GUIContent("Folder Style");
            window.minSize = new Vector2(420f, 500f);
            window.maxSize = new Vector2(900f, 760f);
            window.Initialize(folderPath);
            window.ShowUtility();
        }

        private void Initialize(string folderPath)
        {
            _folderPath = folderPath;

            if (ProjectViewEnhancerSettings.instance.TryGetFolderVisualStyleOverride(folderPath, out ProjectViewEnhancerSettings.FolderVisualStyleOverride style))
            {
                _useBackgroundColor = style.useBackgroundColor;
                _backgroundColor = style.backgroundColor;
                _useIconColor = style.useIconColor;
                _iconColor = style.iconColor;
                _useNameColor = style.useNameColor;
                _nameColor = style.nameColor;
                _useNameFontStyle = style.useNameFontStyle;
                _nameFontStyle = style.nameFontStyle;
            }

            ProjectViewEnhancerSettings.instance.GetFolderRightPaneIconOverlaySettings(folderPath, out _useRightPaneIconOverlay, out string textureAssetPath);
            if (!string.IsNullOrEmpty(textureAssetPath))
                _rightPaneIconOverlayTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Target Folder", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_folderPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(8f);
            DrawReflectionStatus();

            _useBackgroundColor = EditorGUILayout.Toggle("Use Background Color", _useBackgroundColor);
            using (new EditorGUI.DisabledScope(!_useBackgroundColor))
                _backgroundColor = EditorGUILayout.ColorField("Background Color", _backgroundColor);

            EditorGUILayout.Space(4f);
            _useIconColor = EditorGUILayout.Toggle("Use Folder Color", _useIconColor);
            using (new EditorGUI.DisabledScope(!_useIconColor))
                _iconColor = DrawColorFieldWithPresets("Folder Color", _iconColor, s_iconColorPresets);

            EditorGUILayout.Space(4f);
            _useNameColor = EditorGUILayout.Toggle("Use Name Color", _useNameColor);
            using (new EditorGUI.DisabledScope(!_useNameColor))
                _nameColor = DrawColorFieldWithPresets("Name Color", _nameColor, s_nameColorPresets);

            EditorGUILayout.Space(4f);
            _useNameFontStyle = EditorGUILayout.Toggle("Use Name Font Style", _useNameFontStyle);
            using (new EditorGUI.DisabledScope(!_useNameFontStyle))
                _nameFontStyle = (FontStyle)EditorGUILayout.EnumPopup("Name Font Style", _nameFontStyle);

            EditorGUILayout.Space(8f);
            DrawRightPaneIconOverlaySettings();

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply"))
                {
                    if (ApplyStyle())
                        Close();
                }

                if (GUILayout.Button("Clear"))
                {
                    ProjectViewEnhancerSettings.instance.ClearFolderVisualStyleOverride(_folderPath);
                    Close();
                }

                if (GUILayout.Button("Cancel"))
                    Close();
            }
        }

        private bool ApplyStyle()
        {
            if (!TryGetRightPaneIconOverlayTexturePath(out string textureAssetPath, out string errorMessage))
            {
                EditorUtility.DisplayDialog("Invalid PNG", errorMessage, "OK");
                return false;
            }

            ProjectViewEnhancerSettings settings = ProjectViewEnhancerSettings.instance;
            settings.SetFolderVisualStyleOverride(
                _folderPath,
                _useBackgroundColor,
                _backgroundColor,
                _useIconColor,
                _iconColor,
                _useNameColor,
                _nameColor,
                _useNameFontStyle,
                _nameFontStyle);
            settings.SetFolderRightPaneIconOverlay(_folderPath, _useRightPaneIconOverlay, textureAssetPath);
            return true;
        }

        private void DrawRightPaneIconOverlaySettings()
        {
            _useRightPaneIconOverlay = EditorGUILayout.Toggle("Use Right Pane Icon Overlay", _useRightPaneIconOverlay);
            using (new EditorGUI.DisabledScope(!_useRightPaneIconOverlay))
            {
                _rightPaneIconOverlayTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Custom PNG Icon",
                    _rightPaneIconOverlayTexture,
                    typeof(Texture2D),
                    false);

                if (!TryGetRightPaneIconOverlayTexturePath(out string textureAssetPath, out string errorMessage))
                {
                    EditorGUILayout.HelpBox(errorMessage, MessageType.Warning);
                }
                else if (string.IsNullOrEmpty(textureAssetPath))
                {
                    EditorGUILayout.HelpBox(
                        "If no PNG is assigned, the right pane overlay uses the built-in icon mapped from the folder name.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The custom PNG is drawn as-is on the folder icon in the right pane.",
                        MessageType.None);
                }
            }
        }

        private bool TryGetRightPaneIconOverlayTexturePath(out string textureAssetPath, out string errorMessage)
        {
            textureAssetPath = string.Empty;
            errorMessage = string.Empty;

            if (!_useRightPaneIconOverlay || _rightPaneIconOverlayTexture == null)
                return true;

            textureAssetPath = AssetDatabase.GetAssetPath(_rightPaneIconOverlayTexture);
            textureAssetPath = ProjectViewEnhancerSettings.NormalizeAssetReferencePath(textureAssetPath);
            if (string.IsNullOrEmpty(textureAssetPath))
            {
                errorMessage = "The selected texture must be a project asset.";
                return false;
            }

            if (!textureAssetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Only PNG textures are supported for the custom icon.";
                return false;
            }

            return true;
        }

        private static Color DrawColorFieldWithPresets(string label, Color currentColor, Color[] presets)
        {
            currentColor = EditorGUILayout.ColorField(label, currentColor);
            EditorGUILayout.LabelField("Preset Colors", EditorStyles.miniLabel);

            for (int row = 0; row < PresetRowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < PresetColumnCount; col++)
                    {
                        int presetIndex = (row * PresetColumnCount) + col;
                        if (presetIndex >= presets.Length)
                            continue;

                        currentColor = DrawColorPresetButton(currentColor, presets[presetIndex]);

                        if (col < PresetColumnCount - 1)
                            GUILayout.Space(PresetButtonSpacing);
                    }
                }

                if (row < PresetRowCount - 1)
                    GUILayout.Space(PresetButtonSpacing);
            }

            return currentColor;
        }

        private static Color DrawColorPresetButton(Color currentColor, Color presetColor)
        {
            Rect rect = GUILayoutUtility.GetRect(
                PresetButtonSize,
                PresetButtonSize,
                GUILayout.Width(PresetButtonSize),
                GUILayout.Height(PresetButtonSize));

            Color borderColor = IsSameColor(currentColor, presetColor)
                ? (EditorGUIUtility.isProSkin ? Color.white : Color.black)
                : new Color(0f, 0f, 0f, 0.45f);

            EditorGUI.DrawRect(rect, borderColor);
            Rect innerRect = new(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            EditorGUI.DrawRect(innerRect, presetColor);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                return presetColor;

            return currentColor;
        }

        private static bool IsSameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f
                && Mathf.Abs(a.g - b.g) < 0.001f
                && Mathf.Abs(a.b - b.b) < 0.001f
                && Mathf.Abs(a.a - b.a) < 0.001f;
        }

        private static void DrawReflectionStatus()
        {
            if (ProjectViewEnhancerReflectionBootstrap.IsInstalled)
            {
                EditorGUILayout.HelpBox(
                    "Tree patches are active. The right pane overlay is drawn through a separate overlay pass.",
                    MessageType.Info);
                return;
            }

            string error = ProjectViewEnhancerReflectionBootstrap.LastError;
            if (string.IsNullOrEmpty(error))
                error = "Project window reflection patches are not installed yet.";

            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
