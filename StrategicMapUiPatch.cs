using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BrilliantSkies.FromTheDepths.Game;
using BrilliantSkies.FromTheDepths.Game.Battle;
using BrilliantSkies.FromTheDepths.Game.UserInterfaces;
using BrilliantSkies.FromTheDepths.Planets.Map;
using BrilliantSkies.PlayerProfiles;
using BrilliantSkies.PlayerProfiles.BuildingBarStuff;
using BrilliantSkies.ScriptableObjects;
using BrilliantSkies.Ui.Consoles.Styles;
using BrilliantSkies.Ui.Layouts;
using BrilliantSkies.Ui.Special.InfoStore;
using HarmonyLib;
using UnityEngine;

namespace StrategicMapIcons
{
    /// <summary>
    /// Phase 3 Harmony patches for Strategic & Tactical Map UI panels:
    /// 1. Dynamically widens the top-left Fleet/Force list panel to fit long fleet and force names plus row action buttons.
    /// 2. Enforces single-line non-wrapping text on Fleet/Force row styles so text never wraps and overflows row backgrounds vertically.
    /// 3. Expands the vertical height cap of the Strategic Map Fleet/Force list panel so large fleets (e.g. [+36]) use available screen height.
    /// 4. Adjusts InfoStore notification offset on the WorldMap so popups do not overlap an expanded Fleet/Force list.
    /// 5. Dynamically widens the top-right Build / Favourites bar when open so blueprint names and costs are not clipped or crowded.
    /// </summary>
    public static class StrategicMapUiPatch
    {
        private static readonly GUIContent _scratchContent = new GUIContent();

        /// <summary>
        /// Tracks the bottom Y screen coordinate (minus top border) of the rendered Strategic Map fleet/force panel
        /// so InfoStore popups can be pushed below it when a large fleet is selected.
        /// </summary>
        internal static float LastStrategicPanelOccupiedHeight { get; set; }

        /// <summary>
        /// Ensures all row button styles in <see cref="SO_ForceList"/> do not word-wrap onto multiple lines and overflow their background boxes.
        /// </summary>
        internal static void EnsureSingleLineForceListStyles(SO_ForceList? s)
        {
            if (s == null)
            {
                return;
            }

            ConfigureSingleLineStyle(s.STYLE_FleetName);
            ConfigureSingleLineStyle(s.STYLE_ForceName);
            ConfigureSingleLineStyle(s.STYLE_SelectedForceName);
            ConfigureSingleLineStyle(s.STYLE_ForceName_OutOfPlay);
            ConfigureSingleLineStyle(s.STYLE_SelectedForceName_OutOfPlay);
            ConfigureSingleLineStyle(s.STYLE_ForceName_DeadBlueprint);
            ConfigureSingleLineStyle(s.STYLE_SelectedForceName_DeadBlueprint);
        }

        private static void ConfigureSingleLineStyle(GUIStyle? style)
        {
            if (style == null)
            {
                return;
            }

            style.wordWrap = false;
            if (style.clipping == TextClipping.Overflow)
            {
                style.clipping = TextClipping.Clip;
            }
        }

        internal static float MeasureSingleLineWidth(GUIStyle? style, string? text)
        {
            if (style == null || string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            float prevFixedWidth = style.fixedWidth;
            bool prevWordWrap = style.wordWrap;
            style.fixedWidth = 0f;
            style.wordWrap = false;

            _scratchContent.text = text;
            _scratchContent.image = null;
            Vector2 size = style.CalcSize(_scratchContent);

            style.fixedWidth = prevFixedWidth;
            style.wordWrap = prevWordWrap;
            return size.x;
        }

        internal static int CountForceRowButtons(Force? force)
        {
            if (force == null)
            {
                return 0;
            }

            int count = 0;
            if (force.GetWarpOptions() != enumWarpOptions.none)
            {
                count++;
            }

            if (ForceManager.Instance != null)
            {
                enumForcePlayPermission playPerm = ForceManager.Instance.CanIPlayForce(force);
                if (playPerm != enumForcePlayPermission.wrongGameMode && playPerm != enumForcePlayPermission.alreadyInPlay)
                {
                    count++;
                }

                enumForcePullPermission pullPerm = ForceManager.Instance.CanIPullForce(force);
                if (pullPerm != enumForcePullPermission.wrongGameMode && pullPerm != enumForcePullPermission.notInPlay)
                {
                    count++;
                }
            }

            return count;
        }

        private static GUIStyle GetStyleForForce(SO_ForceList s, Force force, bool selected)
        {
            if (force.State == enumForceState.Blueprint)
            {
                return selected ? s.STYLE_SelectedForceName_DeadBlueprint : s.STYLE_ForceName_DeadBlueprint;
            }
            if (force.State == enumForceState.OutOfPlay)
            {
                return selected ? s.STYLE_SelectedForceName_OutOfPlay : s.STYLE_ForceName_OutOfPlay;
            }
            return selected ? s.STYLE_SelectedForceName : s.STYLE_ForceName;
        }

        /// <summary>
        /// Replacement for <c>ConsoleStyles.Instance.GetRect(RectPosition.TopLeft, x, height)</c> inside
        /// <see cref="StrategicMapControlActiveGUI.PanelAndForceButtons"/>.
        /// </summary>
        public static Rect GetStrategicMapTopLeftRect(IConsoleStyles styles, RectPosition position, float baseWidth, float vanillaHeight)
        {
            if (!SMI_ModSettings.ExpandUiPanels)
            {
                LastStrategicPanelOccupiedHeight = 0f;
                return styles.GetRect(position, baseWidth, vanillaHeight);
            }

            SO_ForceList? s = LazyLoader.ForceList.Get();
            EnsureSingleLineForceListStyles(s);

            float rowHeight = vanillaHeight / 8f;
            if (s?.STYLE_FleetName != null)
            {
                _scratchContent.text = " <b><i>[]</i>Force name</b>";
                _scratchContent.image = null;
                float calcH = s.STYLE_FleetName.CalcSize(_scratchContent).y;
                if (calcH > 10f)
                {
                    rowHeight = calcH;
                }
            }

            float maxHeight = Mathf.Max(
                vanillaHeight,
                Mathf.Min(rowHeight * SMI_ModSettings.MaxForcePanelRowMultiplier, (float)Screen.height * SMI_ModSettings.MaxForcePanelScreenHeightFraction));

            float requiredWidth = baseWidth;
            int totalRows = 0;

            if (s != null && FFSelection.data != null)
            {
                float panelPadding = (s.STYLE_Panel != null ? s.STYLE_Panel.padding.horizontal : 12) + 28f;
                float buttonSpan = rowHeight + 6f;

                int selectedFleetCount = FFSelection.data.SelectedFleetCount();
                if (selectedFleetCount == 1)
                {
                    Fleet? fleet = FFSelection.data.GetFirstSelectedFleet();
                    if (fleet != null)
                    {
                        totalRows = 1;
                        float fleetHeaderWidth = MeasureSingleLineWidth(s.STYLE_FleetName, fleet.GetNameWithStatus()) + 16f;
                        if (fleetHeaderWidth > requiredWidth)
                        {
                            requiredWidth = fleetHeaderWidth;
                        }

                        if (fleet.forces != null)
                        {
                            for (int i = 0; i < fleet.forces.Count; i++)
                            {
                                Force? force = fleet.forces[i];
                                if (force == null)
                                {
                                    continue;
                                }

                                totalRows++;
                                bool isSelected = FFSelection.data.SelectedForces != null && FFSelection.data.SelectedForces.Contains(force);
                                GUIStyle forceStyle = GetStyleForForce(s, force, isSelected);
                                float textWidth = MeasureSingleLineWidth(forceStyle, force.GetNameWithStatusColored());
                                int btnCount = CountForceRowButtons(force);
                                float rowWidth = textWidth + btnCount * buttonSpan + panelPadding;
                                if (rowWidth > requiredWidth)
                                {
                                    requiredWidth = rowWidth;
                                }
                            }
                        }
                    }
                }
                else if (selectedFleetCount > 1 && FFSelection.data.SelectedFleets != null)
                {
                    for (int i = 0; i < FFSelection.data.SelectedFleets.Count; i++)
                    {
                        Fleet? fleet = FFSelection.data.SelectedFleets[i];
                        if (fleet == null)
                        {
                            continue;
                        }

                        totalRows++;
                        float textWidth = MeasureSingleLineWidth(s.STYLE_FleetName, fleet.GetNameWithStatus());
                        float rowWidth = textWidth + buttonSpan + panelPadding;
                        if (rowWidth > requiredWidth)
                        {
                            requiredWidth = rowWidth;
                        }
                    }
                }
            }

            float maxAllowedWidth = Mathf.Max(baseWidth, Mathf.Min(SMI_ModSettings.MaxForcePanelWidth, (float)Screen.width * SMI_ModSettings.MaxForcePanelScreenWidthFraction));
            float finalWidth = Mathf.Clamp(requiredWidth, baseWidth, maxAllowedWidth);

            if (totalRows > 0)
            {
                float estimatedOccupied = Mathf.Min(maxHeight, totalRows * (rowHeight + 4f) + 20f);
                LastStrategicPanelOccupiedHeight = estimatedOccupied;
            }
            else
            {
                LastStrategicPanelOccupiedHeight = 0f;
            }

            return styles.GetRect(position, finalWidth, maxHeight);
        }

        /// <summary>
        /// Replacement for <c>ConsoleStyles.Instance.GetRect(RectPosition.TopLeft, x, height)</c> inside
        /// <see cref="FleetControlGUI.DisplayForceAndFleetSelectorAndButtonPanel"/> and <see cref="TacticalMapControlActiveGUI.GUIEntry"/>.
        /// </summary>
        public static Rect GetTacticalTopLeftRect(IConsoleStyles styles, RectPosition position, float baseWidth, float vanillaHeight)
        {
            if (!SMI_ModSettings.ExpandUiPanels)
            {
                return styles.GetRect(position, baseWidth, vanillaHeight);
            }

            SO_ForceList? s = LazyLoader.ForceList.Get();
            EnsureSingleLineForceListStyles(s);

            float rowHeight = vanillaHeight / 20f;
            if (s?.STYLE_FleetName != null)
            {
                _scratchContent.text = " <b><i>[]</i>Force name</b>";
                _scratchContent.image = null;
                float calcH = s.STYLE_FleetName.CalcSize(_scratchContent).y;
                if (calcH > 10f)
                {
                    rowHeight = calcH;
                }
            }

            float maxHeight = Mathf.Max(
                vanillaHeight,
                Mathf.Min(rowHeight * SMI_ModSettings.MaxForcePanelRowMultiplier, (float)Screen.height * SMI_ModSettings.MaxForcePanelScreenHeightFraction));

            float requiredWidth = baseWidth;

            if (s != null && FFSelection.data?.AllForces != null)
            {
                float panelPadding = (s.STYLE_Panel != null ? s.STYLE_Panel.padding.horizontal : 12) + 28f;
                float buttonSpan = rowHeight + 6f;
                Fleet? lastFleet = null;
                bool firstOutOfPlayDone = false;

                for (int i = 0; i < FFSelection.data.AllForces.Count; i++)
                {
                    Force? force = FFSelection.data.AllForces[i];
                    if (force == null || force.Deleted)
                    {
                        continue;
                    }

                    if (force.Fleet != null && lastFleet != force.Fleet && !force.Fleet.Deleted)
                    {
                        lastFleet = force.Fleet;
                        if (!BattleOrchestrator.Active)
                        {
                            int fleetBtns = lastFleet.ForceCount > 1 ? 2 : 0;
                            if (FFSelection.data.SelectedForces != null && FFSelection.data.SelectedForces.Count > 0)
                            {
                                fleetBtns++;
                            }
                            float fleetWidth = MeasureSingleLineWidth(s.STYLE_FleetName, lastFleet.GetNameWithStatus()) + fleetBtns * buttonSpan + panelPadding;
                            if (fleetWidth > requiredWidth)
                            {
                                requiredWidth = fleetWidth;
                            }
                        }
                    }

                    string extraText = "";
                    if (!firstOutOfPlayDone && BattleOrchestrator.Battling && !force.BattleInfo.Withdrawn && force.State == enumForceState.OutOfPlay)
                    {
                        float needed = Mathf.Round(force.CSI.AliveVolume - BattleOrchestrator.Instance.BattleForceManager.PlayerTeam.ToSpend());
                        extraText = $" [{needed} m3 needed]";
                        firstOutOfPlayDone = true;
                    }

                    bool isSelected = FFSelection.data.SelectedForces != null && FFSelection.data.SelectedForces.Contains(force);
                    GUIStyle forceStyle = GetStyleForForce(s, force, isSelected);
                    float textWidth = MeasureSingleLineWidth(forceStyle, force.GetNameWithStatusColored() + extraText);
                    int btnCount = CountForceRowButtons(force);
                    float rowWidth = textWidth + btnCount * buttonSpan + panelPadding;
                    if (rowWidth > requiredWidth)
                    {
                        requiredWidth = rowWidth;
                    }
                }
            }

            float maxAllowedWidth = Mathf.Max(baseWidth, Mathf.Min(SMI_ModSettings.MaxForcePanelWidth, (float)Screen.width * SMI_ModSettings.MaxForcePanelScreenWidthFraction));
            float finalWidth = Mathf.Clamp(requiredWidth, baseWidth, maxAllowedWidth);

            return styles.GetRect(position, finalWidth, maxHeight);
        }

        internal static IEnumerable<CodeInstruction> ReplaceGetRectCall(IEnumerable<CodeInstruction> instructions, MethodInfo replacementMethod)
        {
            MethodInfo? getRectMethod = AccessTools.Method(
                typeof(IConsoleStyles),
                nameof(IConsoleStyles.GetRect),
                new[] { typeof(RectPosition), typeof(float), typeof(float) });

            bool replaced = false;
            foreach (CodeInstruction instr in instructions)
            {
                if (!replaced && getRectMethod != null && replacementMethod != null && instr.Calls(getRectMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacementMethod);
                    replaced = true;
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ForceDisplayPanel))]
    public static class ForceDisplayPanelPatch
    {
        private static readonly AccessTools.FieldRef<ForceDisplayPanel, SO_ForceList> _sRef =
            AccessTools.FieldRefAccess<ForceDisplayPanel, SO_ForceList>("_s");

        [HarmonyPatch("CalcButtonSize")]
        [HarmonyPostfix]
        public static void Postfix_CalcButtonSize(ForceDisplayPanel __instance)
        {
            if (__instance != null && SMI_ModSettings.ExpandUiPanels)
            {
                StrategicMapUiPatch.EnsureSingleLineForceListStyles(_sRef(__instance));
            }
        }
    }

    [HarmonyPatch(typeof(StrategicMapControlActiveGUI))]
    public static class StrategicMapPanelLayoutPatch
    {
        [HarmonyPatch("PanelAndForceButtons")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler_PanelAndForceButtons(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(StrategicMapUiPatch), nameof(StrategicMapUiPatch.GetStrategicMapTopLeftRect));
            return StrategicMapUiPatch.ReplaceGetRectCall(instructions, replacement);
        }
    }

    [HarmonyPatch(typeof(FleetControlGUI))]
    public static class FleetControlGuiPanelLayoutPatch
    {
        [HarmonyPatch(nameof(FleetControlGUI.DisplayForceAndFleetSelectorAndButtonPanel))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler_DisplayForceAndFleetSelectorAndButtonPanel(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(StrategicMapUiPatch), nameof(StrategicMapUiPatch.GetTacticalTopLeftRect));
            return StrategicMapUiPatch.ReplaceGetRectCall(instructions, replacement);
        }
    }

    [HarmonyPatch(typeof(TacticalMapControlActiveGUI))]
    public static class TacticalMapPanelLayoutPatch
    {
        [HarmonyPatch(nameof(TacticalMapControlActiveGUI.GUIEntry))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler_GUIEntry(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo replacement = AccessTools.Method(typeof(StrategicMapUiPatch), nameof(StrategicMapUiPatch.GetTacticalTopLeftRect));
            return StrategicMapUiPatch.ReplaceGetRectCall(instructions, replacement);
        }
    }

    [HarmonyPatch(typeof(WorldMap))]
    public static class WorldMapInfoStoreOffsetPatch
    {
        [HarmonyPatch(nameof(WorldMap.OnGui))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler_OnGui(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo? displayInfoMethod = AccessTools.Method(
                typeof(InfoStore),
                nameof(InfoStore.DisplayInfo),
                new[] { typeof(float), typeof(float) });
            MethodInfo? replacementMethod = AccessTools.Method(
                typeof(WorldMapInfoStoreOffsetPatch),
                nameof(DisplayInfoWithDynamicOffset));

            foreach (CodeInstruction instr in instructions)
            {
                if (displayInfoMethod != null && replacementMethod != null && instr.Calls(displayInfoMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacementMethod);
                }
                else
                {
                    yield return instr;
                }
            }
        }

        public static void DisplayInfoWithDynamicOffset(float borderWidth, float extraDown)
        {
            float dynamicExtraDown = SMI_ModSettings.ExpandUiPanels
                ? Mathf.Max(extraDown, StrategicMapUiPatch.LastStrategicPanelOccupiedHeight + 8f)
                : extraDown;
            InfoStore.DisplayInfo(borderWidth, dynamicExtraDown);
        }
    }

    [HarmonyPatch(typeof(FavouritesBarGui))]
    public static class FavouritesBarGuiPatch
    {
        private static readonly FieldInfo? _stateField = AccessTools.Field(typeof(FavouritesBarGui), "_state");
        private static readonly AccessTools.FieldRef<FavouritesBarGui, SO_FavouritesBar> _sRef =
            AccessTools.FieldRefAccess<FavouritesBarGui, SO_FavouritesBar>("_s");

        private static bool IsOpen(FavouritesBarGui instance)
        {
            if (instance == null || _stateField == null)
            {
                return false;
            }

            object? val = _stateField.GetValue(instance);
            return val != null && Convert.ToInt32(val) == 1; // eState.Open == 1
        }

        private static float ComputeOpenWidth(FavouritesBarGui instance, float baseWidth)
        {
            float minOpenWidth = baseWidth * SMI_ModSettings.OpenFavouritesBarMinWidthMultiplier;
            float requiredWidth = minOpenWidth;

            SO_FavouritesBar? s = _sRef(instance);
            MFavourites_Ftd? module = ProfileManager.Instance?.GetModule<MFavourites_Ftd>();
            if (s != null && module?.Categories?.Categories != null)
            {
                var categories = module.Categories.Categories;
                for (int cIdx = 0; cIdx < categories.Count; cIdx++)
                {
                    BarCategory? category = categories[cIdx];
                    if (category == null || category.Favs == null || category.Favs.Count == 0 || category.Type != module.SelectedType)
                    {
                        continue;
                    }

                    float catWidth = StrategicMapUiPatch.MeasureSingleLineWidth(s.STYLE_UsageType, category.GetName()) + 40f;
                    if (catWidth > requiredWidth)
                    {
                        requiredWidth = catWidth;
                    }

                    if (category.Open)
                    {
                        for (int fIdx = 0; fIdx < category.Favs.Count; fIdx++)
                        {
                            FavouriteDesign? fav = category.Favs[fIdx];
                            if (fav == null)
                            {
                                continue;
                            }

                            string costStr = fav.HasResourceCostCalculated ? fav.CalcResourceCost().ToStringInK() : "";
                            string label = $"<color=cyan>{fav.AbrevName}</color>{costStr}";
                            float itemWidth = StrategicMapUiPatch.MeasureSingleLineWidth(s.STYLE_FavNameAndOneLiner, label) + 46f;
                            if (itemWidth > requiredWidth)
                            {
                                requiredWidth = itemWidth;
                            }
                        }
                    }
                }
            }

            float maxAllowedWidth = Mathf.Max(minOpenWidth, Mathf.Min(SMI_ModSettings.MaxFavouritesBarWidth, (float)Screen.width * SMI_ModSettings.MaxFavouritesBarScreenWidthFraction));
            return Mathf.Clamp(requiredWidth, minOpenWidth, maxAllowedWidth);
        }

        [HarmonyPatch(nameof(FavouritesBarGui.GetWidth))]
        [HarmonyPostfix]
        public static void Postfix_GetWidth(FavouritesBarGui __instance, ref float __result)
        {
            if (SMI_ModSettings.ExpandFavouritesBar && IsOpen(__instance))
            {
                __result = ComputeOpenWidth(__instance, __result);
            }
        }

        [HarmonyPatch("GetRect")]
        [HarmonyPostfix]
        public static void Postfix_GetRect(FavouritesBarGui __instance, ref Rect __result)
        {
            if (SMI_ModSettings.ExpandFavouritesBar && IsOpen(__instance))
            {
                float openWidth = ComputeOpenWidth(__instance, __result.width);
                Rect topRight = ConsoleStyles.Instance.GetRect(RectPosition.TopRight, openWidth, __result.height);
                __result.x = topRight.x;
                __result.width = topRight.width;
            }
        }
    }
}

