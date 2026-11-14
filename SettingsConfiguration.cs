using BrilliantSkies.Core.Collections;
using BrilliantSkies.PlayerProfiles;
using BrilliantSkies.Ui.Consoles;
using BrilliantSkies.Ui.Consoles.Examples;
using BrilliantSkies.Ui.Consoles.Getters;
using BrilliantSkies.Ui.Consoles.Interpretters;
using BrilliantSkies.Ui.Consoles.Interpretters.Simple;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Choices;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Numbers;
using BrilliantSkies.Ui.Consoles.Segments;
using BrilliantSkies.Ui.Consoles.Styles;
using BrilliantSkies.Ui.Layouts.DropDowns;
using BrilliantSkies.Ui.Tips;
using HarmonyLib;
using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace StrategicMapIcons
{
    /// <summary>
    /// Persistent profile module for Strategic Map Icons configuration.
    /// </summary>
    public class SMI_ModSettingsConfiguration : ProfileModule<SMI_ModSettingsConfiguration.InternalData>
    {
        public class InternalData
        {
            public bool Enabled { get; set; } = true;
            public bool ShowResourceLabels { get; set; } = true;
            public bool EnableOnZoom { get; set; } = false;
            public bool SeparateOverlappingIcons { get; set; } = false;
            public Color TeamColor { get; set; } = new Color(0.00f, 1.00f, 1.00f, 1.00f); // cyan blue
            public bool ShowFleetCostTiers { get; set; } = true;
            public bool ScaleByMaterials { get; set; } = true;
            public float ScalingExponent { get; set; } = 2f;
            public float IconScaleMultiplier { get; set; } = 1.0f;
            public bool OutlineIcons { get; set; } = true;
            public float OutlineWidth { get; set; } = 2.0f;
            public bool ExpandUiPanels { get; set; } = false;
            public bool ExpandFavouritesBar { get; set; } = false;
        }

        public override ModuleType ModuleType => ModuleType.Options;
        protected override string FilenameAndExtension => "profile.StrategicMapIconsConfig";

        public static SMI_ModSettingsConfiguration? Get()
        {
            try
            {
                return ProfileManager.Instance?.GetModule<SMI_ModSettingsConfiguration>();
            }
            catch
            {
                return null;
            }
        }

        public override void PostLoad(LoadStatus status)
        {
            base.PostLoad(status);
            ApplyToSettings();
        }

        public void ApplyToSettings()
        {
            SMI_ModSettings.Enabled = Enabled;
            SMI_ModSettings.ShowResourceLabels = ShowResourceLabels;
            SMI_ModSettings.EnableOnZoom = EnableOnZoom;
            SMI_ModSettings.SeparateOverlappingIcons = SeparateOverlappingIcons;
            SMI_ModSettings.TeamColor = TeamColor;
            SMI_ModSettings.ShowFleetCostTiers = ShowFleetCostTiers;
            SMI_ModSettings.ScaleByMaterials = ScaleByMaterials;
            SMI_ModSettings.ScalingExponent = ScalingExponent;
            SMI_ModSettings.IconScaleMultiplier = IconScaleMultiplier;
            SMI_ModSettings.OutlineIcons = OutlineIcons;
            SMI_ModSettings.OutlineWidth = OutlineWidth;
            SMI_ModSettings.ExpandUiPanels = ExpandUiPanels;
            SMI_ModSettings.ExpandFavouritesBar = ExpandFavouritesBar;
        }

        #region Properties

        public bool Enabled
        {
            get => Internal.Enabled;
            set { Internal.Enabled = value; SMI_ModSettings.Enabled = value; }
        }

        public bool ShowResourceLabels
        {
            get => Internal.ShowResourceLabels;
            set { Internal.ShowResourceLabels = value; SMI_ModSettings.ShowResourceLabels = value; }
        }
        public bool EnableOnZoom
        {
            get => Internal.EnableOnZoom;
            set { Internal.EnableOnZoom = value; SMI_ModSettings.EnableOnZoom = value; }
        }
        public bool SeparateOverlappingIcons
        {
            get => Internal.SeparateOverlappingIcons;
            set { Internal.SeparateOverlappingIcons = value; SMI_ModSettings.SeparateOverlappingIcons = value; }
        }
        public Color TeamColor
        {
            get => Internal.TeamColor;
            set { Internal.TeamColor = value; SMI_ModSettings.TeamColor = value; }
        }

        public bool ShowFleetCostTiers
        {
            get => Internal.ShowFleetCostTiers;
            set { Internal.ShowFleetCostTiers = value; SMI_ModSettings.ShowFleetCostTiers = value; }
        }
        public bool ScaleByMaterials
        {
            get => Internal.ScaleByMaterials;
            set { Internal.ScaleByMaterials = value; SMI_ModSettings.ScaleByMaterials = value; }
        }
        public float ScalingExponent
        {
            get => Internal.ScalingExponent;
            set { Internal.ScalingExponent = value; SMI_ModSettings.ScalingExponent = value; }
        }
        public float IconScaleMultiplier
        {
            get => Internal.IconScaleMultiplier;
            set { Internal.IconScaleMultiplier = value; SMI_ModSettings.IconScaleMultiplier = value; }
        }
        public bool OutlineIcons
        {
            get => Internal.OutlineIcons;
            set { Internal.OutlineIcons = value; SMI_ModSettings.OutlineIcons = value; }
        }
        public float OutlineWidth
        {
            get => Internal.OutlineWidth;
            set { Internal.OutlineWidth = value; SMI_ModSettings.OutlineWidth = value; }
        }
        public bool ExpandUiPanels
        {
            get => Internal.ExpandUiPanels;
            set { Internal.ExpandUiPanels = value; SMI_ModSettings.ExpandUiPanels = value; }
        }
        public bool ExpandFavouritesBar
        {
            get => Internal.ExpandFavouritesBar;
            set { Internal.ExpandFavouritesBar = value; SMI_ModSettings.ExpandFavouritesBar = value; }
        }
        #endregion
    }

    /// <summary>
    /// UI helper methods for generating console interpreters (toggles, sliders).
    /// </summary>
    public static class UIHelper
    {
        public static SubjectiveToggle<T> Bool<T>(
            T focus,
            string label,
            string tooltip,
            Expression<Func<T, bool>> property,
            Action<bool>? onValueChanged = null)
        {
            var member = (MemberExpression)property.Body;
            var propInfo = (PropertyInfo)member.Member;

            return SubjectiveToggle<T>.Quick(
                focus,
                label,
                new ToolTip(tooltip),
                (obj, value) =>
                {
                    propInfo.SetValue(obj, value);
                    onValueChanged?.Invoke(value);
                },
                obj => (bool)propInfo.GetValue(obj)
            );
        }

        public static SubjectiveFloatClampedWithBarFromMiddle<T> FloatSlider<T>(
            T focus,
            float min,
            float max,
            float inc,
            float middle,
            string display,
            Expression<Func<T, float>> property,
            string tooltip,
            Action<float>? onValueChanged = null)
        {
            var member = (MemberExpression)property.Body;
            var propInfo = (PropertyInfo)member.Member;
            var getter = property.Compile();

            return SubjectiveFloatClampedWithBarFromMiddle<T>.Quick(
                focus,
                min,
                max,
                inc,
                middle,
                M.m((T obj) => getter(obj)),
                display,
                (obj, value) =>
                {
                    propInfo.SetValue(obj, value);
                    onValueChanged?.Invoke(value);
                },
                new ToolTip(tooltip)
            );
        }

        public static SubjectiveFloatClampedWithBarFromMiddle<T> IntSlider<T>(
            T focus,
            int min,
            int max,
            int inc,
            int middle,
            string display,
            Expression<Func<T, int>> property,
            string tooltip,
            Action<int>? onValueChanged = null)
        {
            var member = (MemberExpression)property.Body;
            var propInfo = (PropertyInfo)member.Member;
            var getter = property.Compile();

            return SubjectiveFloatClampedWithBarFromMiddle<T>.Quick(
                focus,
                min,
                max,
                inc,
                middle,
                M.m((T obj) => (float)getter(obj)),
                display,
                (obj, value) =>
                {
                    int intVal = Mathf.RoundToInt(value);
                    propInfo.SetValue(obj, intVal);
                    onValueChanged?.Invoke(intVal);
                },
                new ToolTip(tooltip)
            );
        }

        public static SubjectiveColorChanger<T> ColorPicker<T>(
            T focus,
            string label,
            string tooltip,
            Expression<Func<T, Color>> property,
            Action<Color>? onValueChanged = null,
            bool displayAlpha = false)
        {
            var member = (MemberExpression)property.Body;
            var propInfo = (PropertyInfo)member.Member;
            var getter = property.Compile();

            return new SubjectiveColorChanger<T>(
                focus,
                M.m<T>(label),
                M.m<T>(new ToolTip(tooltip)),
                M.m<T>((T obj) => getter(obj)),
                (obj, value) =>
                {
                    propInfo.SetValue(obj, value);
                    onValueChanged?.Invoke(value);
                },
                Array.Empty<string>()
            )
            {
                DisplayAlpha = displayAlpha
            };
        }
    }

    /// <summary>
    /// Mod settings tab integrated into From The Depths options menu.
    /// </summary>
    public class SMI_ModSettingsTab(ConsoleWindow window, SMI_ModSettingsConfiguration config)
        : SuperScreen<SMI_ModSettingsConfiguration>(window, config)
    {
        private const float Spacing = 20f;

        public override Content Name => new(
            "Strategic Map Icons",
            new ToolTip("Change some settings for the Strategic Map Icons mod.")
        );

        public override void Build()
        {
            GeneralSection();
            VisualSection();
        }

        private void GeneralSection()
        {
            ScreenSegmentTable table = CreateTableSegment(2, 6);
            table.SqueezeTable = false;
            table.SpaceBelow = Spacing;
            table.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            table.NameWhereApplicable = "General Settings";

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Enable Strategic Map Icons",
                "Turn off or on the display of custom vertex fleet icons on the Strategic Map (default: true).",
                I => I.Enabled
            ), 0, 0);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Separate Overlapping Icons",
                "Shift nearby fleet icons apart with leader lines so overlapping fleets remain visible and clickable (default: false).",
                I => I.SeparateOverlappingIcons
            ), 0, 1);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Enable Icons When Zoomed In",
                "Allow the Strategic Map icons to be drawn when zoomed in past the strategic map view (default: false).",
                I => I.EnableOnZoom
            ), 1, 0);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Expand Fleet & Force Panels",
                "Force resize top-left fleet and force list panels on the Strategic & Tactical Maps to fit long craft names without text wrapping (default: false).",
                I => I.ExpandUiPanels
            ), 1, 1);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Expand Build / Favourites Bar",
                "Dynamically widen the top-right Build / Favourites menu when opened so blueprint names and costs are not clipped (default: false).",
                I => I.ExpandFavouritesBar
            ), 2, 0);
        }

        private void VisualSection()
        {
            ScreenSegmentTable table = CreateTableSegment(2, 6);
            table.SqueezeTable = false;
            table.SpaceBelow = Spacing;
            table.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            table.NameWhereApplicable = "Visual Settings";

            table.AddInterpretter(UIHelper.ColorPicker(
               _focus,
               "Team Icon Color",
               "Color used for player fleet and craft icons on the strategic map (default: Cyan). Click to open the color picker.",
               I => I.TeamColor
           ), 0, 0);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Show Resource & Supply Labels",
                "Display non-critical creator/cargo role and status text overlays on the map. Critical 'low mats' / 'no mats' warnings are always shown (default: true).",
                I => I.ShowResourceLabels
            ), 0, 1);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Show Fleet Cost",
                "Show fleet cost tier indicators (chevrons/star) and material cost below each fleet (default: true).",
                I => I.ShowFleetCostTiers
            ), 1, 0);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Scale Icons by Materials",
                "Scale fleet icons based on their total material cost (default: true).",
                I => I.ScaleByMaterials
            ), 1, 1);

            table.AddInterpretter(UIHelper.Bool(
                _focus,
                "Icon Black Outline",
                "Draw a black outline behind vector fleet icons for maximum contrast on bright maps and terrain (default: true).",
                I => I.OutlineIcons
            ), 2, 0);

            table.AddInterpretter(UIHelper.FloatSlider(
                _focus,
                0.5f,
                6.0f,
                0.25f,
                2.0f,
                "Outline Width",
                I => I.OutlineWidth,
                "Extra width in pixels added to vector lines to create the black outline (default: 2.0)."
            ), 2, 1);

            table.AddInterpretter(UIHelper.FloatSlider(
                _focus,
                0.1f,
                5.0f,
                0.05f,
                1.0f,
                "Icon Scale Multiplier",
                I => I.IconScaleMultiplier,
                "Global multiplier for the size of all strategic map icons (default: 1.0)."
            ), 3, 0);

            table.AddInterpretter(UIHelper.FloatSlider(
                _focus,
                0.1f,
                10.0f,
                0.1f,
                2f,
                "Scaling Exponent",
                I => I.ScalingExponent,
                "Exponent used to scale fleet icons by their total material cost (default: 2.0)."
            ), 3, 1);
        }

    }

    /// <summary>
    /// Harmony patch adding the Strategic Map Icons settings tab to the in-game Options Menu (F1 -> Options).
    /// </summary>
    [HarmonyPatch(typeof(OptionsMenuUi), "BuildInterface")]
    public static class SMI_ModSettingsUIPatch
    {
        private static void Postfix(ref ConsoleWindow __result)
        {
            try
            {
                var config = SMI_ModSettingsConfiguration.Get();
                if (config != null)
                {
                    __result.AllScreens.Add(new SMI_ModSettingsTab(__result, config));
                }
            }
            catch (Exception ex)
            {
                BrilliantSkies.Core.Logger.AdvLogger.LogError($"[StrategicMapIcons] Failed to add SettingsTab to OptionsMenu: {ex}", BrilliantSkies.Core.Logger.LogOptions.None);
            }
        }
    }
}