using UnityEditor;
using UnityEngine;

namespace JLZ.Editor.ProjectViewEnhancer
{
    internal static class ProjectViewEnhancerTreeScaleState
    {
        private const string PrefPrefix = "ProjectViewEnhancer.ProjectTreeScale.";
        private const string LegacyPrefPrefix = "ProjectViewEnhancer.ProjectTreeScalePrototype.";
        private const string OlderLegacyPrefPrefix = "ProjectViewEnhancer.SingleColumnTreeScalePrototype.";
        private const string EnabledKey = PrefPrefix + "Enabled";
        private const string ScaleKey = PrefPrefix + "Scale";
        private const string ScaleDefaultsMigrationKey = PrefPrefix + "DefaultsMigratedTo2x";
        private const string LegacyEnabledKey = LegacyPrefPrefix + "Enabled";
        private const string LegacyScaleKey = LegacyPrefPrefix + "Scale";
        private const string OlderLegacyEnabledKey = OlderLegacyPrefPrefix + "Enabled";
        private const string OlderLegacyScaleKey = OlderLegacyPrefPrefix + "Scale";
        private const bool LegacyDefaultEnabled = false;
        private const float LegacyDefaultScale = 1.2f;
        private const bool DefaultEnabled = true;
        private const float MinScale = 1f;
        private const float MaxScale = 2f;
        private const float DefaultScale = 2f;

        private static bool s_loaded;
        private static bool s_enabled;
        private static float s_scale;
        private static bool s_hasPreviewScale;
        private static float s_previewScale;

        internal static bool Enabled
        {
            get
            {
                EnsureLoaded();
                return s_enabled;
            }
            set
            {
                EnsureLoaded();
                s_enabled = value;
                EditorPrefs.SetBool(EnabledKey, value);
            }
        }

        internal static float Scale
        {
            get
            {
                EnsureLoaded();
                return s_scale;
            }
            set
            {
                EnsureLoaded();
                s_scale = Mathf.Clamp(value, MinScale, MaxScale);
                EditorPrefs.SetFloat(ScaleKey, s_scale);
            }
        }

        internal static float GetUiScaleValue()
        {
            EnsureLoaded();
            return GetEffectiveScale();
        }

        internal static void SetScaleFromUi(float scale)
        {
            EnsureLoaded();
            ApplyPersistedScale(scale);
        }

        internal static float GetEffectiveScale()
        {
            EnsureLoaded();

            if (s_hasPreviewScale)
                return Mathf.Max(MinScale, s_previewScale);

            return s_enabled ? Mathf.Max(MinScale, s_scale) : MinScale;
        }

        internal static bool HasPreviewScale()
        {
            EnsureLoaded();
            return s_hasPreviewScale;
        }

        internal static void PreviewScaleFromUi(float scale)
        {
            EnsureLoaded();
            s_previewScale = Mathf.Clamp(scale, MinScale, MaxScale);
            s_hasPreviewScale = true;
        }

        internal static void CommitPreviewScale()
        {
            EnsureLoaded();
            if (!s_hasPreviewScale)
                return;

            ApplyPersistedScale(s_previewScale);
            s_hasPreviewScale = false;
        }

        internal static void ClearPreviewScale()
        {
            EnsureLoaded();
            s_hasPreviewScale = false;
        }

        private static void ApplyPersistedScale(float scale)
        {
            float clampedScale = Mathf.Clamp(scale, MinScale, MaxScale);
            s_enabled = clampedScale > 1.001f;
            s_scale = clampedScale;
            EditorPrefs.SetBool(EnabledKey, s_enabled);
            EditorPrefs.SetFloat(ScaleKey, s_scale);
        }

        private static void EnsureLoaded()
        {
            if (s_loaded)
                return;

            bool hasCurrentEnabled = EditorPrefs.HasKey(EnabledKey);
            bool hasCurrentScale = EditorPrefs.HasKey(ScaleKey);
            bool hasLegacyEnabled = EditorPrefs.HasKey(LegacyEnabledKey);
            bool hasLegacyScale = EditorPrefs.HasKey(LegacyScaleKey);
            bool hasOlderLegacyEnabled = EditorPrefs.HasKey(OlderLegacyEnabledKey);
            bool hasOlderLegacyScale = EditorPrefs.HasKey(OlderLegacyScaleKey);

            s_enabled = hasCurrentEnabled
                ? EditorPrefs.GetBool(EnabledKey, DefaultEnabled)
                : hasLegacyEnabled
                    ? EditorPrefs.GetBool(LegacyEnabledKey, DefaultEnabled)
                    : hasOlderLegacyEnabled
                        ? EditorPrefs.GetBool(OlderLegacyEnabledKey, DefaultEnabled)
                        : DefaultEnabled;
            s_scale = Mathf.Clamp(
                hasCurrentScale
                    ? EditorPrefs.GetFloat(ScaleKey, DefaultScale)
                    : hasLegacyScale
                        ? EditorPrefs.GetFloat(LegacyScaleKey, DefaultScale)
                        : hasOlderLegacyScale
                            ? EditorPrefs.GetFloat(OlderLegacyScaleKey, DefaultScale)
                            : DefaultScale,
                MinScale,
                MaxScale);

            TryMigrateLegacyDefaultScale(hasCurrentEnabled, hasCurrentScale);

            if (!hasCurrentEnabled)
                EditorPrefs.SetBool(EnabledKey, s_enabled);

            if (!hasCurrentScale)
                EditorPrefs.SetFloat(ScaleKey, s_scale);

            s_loaded = true;
        }

        private static void TryMigrateLegacyDefaultScale(bool hasCurrentEnabled, bool hasCurrentScale)
        {
            if (EditorPrefs.GetBool(ScaleDefaultsMigrationKey, false))
                return;

            if (hasCurrentEnabled && hasCurrentScale)
            {
                bool currentEnabled = EditorPrefs.GetBool(EnabledKey, DefaultEnabled);
                float currentScale = Mathf.Clamp(EditorPrefs.GetFloat(ScaleKey, DefaultScale), MinScale, MaxScale);
                bool matchesLegacyDefaults = !currentEnabled && Mathf.Abs(currentScale - LegacyDefaultScale) < 0.0001f;

                if (matchesLegacyDefaults)
                {
                    s_enabled = DefaultEnabled;
                    s_scale = DefaultScale;
                    EditorPrefs.SetBool(EnabledKey, s_enabled);
                    EditorPrefs.SetFloat(ScaleKey, s_scale);
                }
            }

            EditorPrefs.SetBool(ScaleDefaultsMigrationKey, true);
        }
    }
}
