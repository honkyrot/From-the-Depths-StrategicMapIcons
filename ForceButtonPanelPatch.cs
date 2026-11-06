using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BrilliantSkies.FromTheDepths.Game;
using BrilliantSkies.FromTheDepths.Planets.Map;
using BrilliantSkies.Ftd.Rz.Transfer.SupplyGroups;
using BrilliantSkies.PlayerProfiles;
using BrilliantSkies.Ui.Elements;
using BrilliantSkies.Ui.Tips;
using HarmonyLib;
using UnityEngine;

namespace StrategicMapIcons
{
    [HarmonyPatch(typeof(ForceButtonPanel))]
    public static class ForceButtonPanelPatch
    {
        private static readonly AccessTools.FieldRef<ForceButtonPanel, List<Force>> _forcesRef =
            AccessTools.FieldRefAccess<ForceButtonPanel, List<Force>>("_forces");

        private static readonly StrategicIconMode[] _vertexRoleOrder = new[]
        {
            StrategicIconMode.DefaultEmblem,
            StrategicIconMode.Auto,
            StrategicIconMode.Water_Combat,
            StrategicIconMode.Air_Combat,
            StrategicIconMode.Land_Combat,
            StrategicIconMode.Orbital_Combat,
            StrategicIconMode.Fortress,
            StrategicIconMode.Harvester,
            StrategicIconMode.Cargo,
            StrategicIconMode.Scout,
            StrategicIconMode.Support
        };

        private static readonly StrategicIconMode[] _simpleShapesOrder = new[]
        {
            StrategicIconMode.Shapes_Circle,
            StrategicIconMode.Shapes_Triangle,
            StrategicIconMode.Shapes_Square,
            StrategicIconMode.Shapes_Diamond,
            StrategicIconMode.Shapes_Pentagon,
            StrategicIconMode.Shapes_Hexagon,
            StrategicIconMode.Shapes_Octagon,
            StrategicIconMode.Shapes_Cross,
            StrategicIconMode.Shapes_Star,
            StrategicIconMode.Shapes_Chevron
        };

        private static readonly Dictionary<StrategicIconMode, UiDef> _uiDefs = new Dictionary<StrategicIconMode, UiDef>();
        private static UiDef? _toggleMenuUiDef;
        private static UiDef? _toggleLabelsUiDef;
        private static bool _isIconMenuExpanded;

        /// <summary>
        /// Controls whether non-critical resource/supply text overlays (creator, cargo, -filling up...-, etc.) are drawn on the map. Critical "low mats" / "no mats" warnings are always kept visible.
        /// </summary>
        public static bool ShowResourceLabels
        {
            get => SMI_ModSettings.ShowResourceLabels;
            set
            {
                SMI_ModSettings.ShowResourceLabels = value;
                var config = SMI_ModSettingsConfiguration.Get();
                if (config != null)
                {
                    config.ShowResourceLabels = value;
                }
            }
        }

        [HarmonyPatch(nameof(ForceButtonPanel.DisplayPanel))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler_DisplayPanel(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo displayButtonNewMethod = AccessTools.Method(typeof(ForceButtonPanel), "DisplayButtonNew");
            MethodInfo drawLabelBtnMethod = AccessTools.Method(typeof(ForceButtonPanelPatch), nameof(DrawLabelToggleButton));
            MethodInfo chainIndexMethod = AccessTools.Method(typeof(ForceButtonPanel), "DisplayChainIndexButtons");
            MethodInfo endVerticalMethod = AccessTools.Method(typeof(GUILayout), nameof(GUILayout.EndVertical), Type.EmptyTypes);
            MethodInfo drawRowMethod = AccessTools.Method(typeof(ForceButtonPanelPatch), nameof(DrawIconRoleRow));

            bool injectedIfBranch = false;
            bool injectedElseBranch = false;
            for (int i = 0; i < code.Count; i++)
            {
                // In the `else` branch (ViewSpecialResourceView == false), execution falls through to EndVertical.
                // Note: EndVertical carries the branch label targeted by the end of the `if` branch, so leaving
                // the label on EndVertical ensures the `if` branch jumps directly to EndVertical without double-drawing.
                if (!injectedElseBranch && endVerticalMethod != null && code[i].Calls(endVerticalMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, drawRowMethod);
                    injectedElseBranch = true;
                }

                yield return code[i];

                // In both the `if (options.ViewSpecialResourceView)` and `else` branches of DisplayPanel,
                // DisplayButtonNew is called specifically for FtdUiDefs.COMMODITIES_MAP (the Resource View button).
                // Inject DrawLabelToggleButton() immediately after it so the Label button sits right next to it in both states.
                if (displayButtonNewMethod != null && drawLabelBtnMethod != null && code[i].Calls(displayButtonNewMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, drawLabelBtnMethod);
                }

                // In the `if (options.ViewSpecialResourceView)` branch, DisplayChainIndexButtons() is the final call
                // before jumping to EndVertical. Inject right after it so the icon row appears below 0..5.
                if (!injectedIfBranch && chainIndexMethod != null && code[i].Calls(chainIndexMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, drawRowMethod);
                    injectedIfBranch = true;
                }
            }
        }

        public static void DrawLabelToggleButton()
        {
            UiDef labelDef = GetOrCreateLabelToggleUiDef();
            labelDef.ClearOverrides();
            labelDef.Width = 0.8f;
            labelDef.Height = 0.8f;
            labelDef.ForegroundTint = ShowResourceLabels ? Color.green : Color.white;

            string stateStr = ShowResourceLabels ? "<color=#80ff80>ON</color>" : "<color=#ff8080>OFF</color>";
            string tip = $"Resource & Supply Labels: {stateStr}\nClick to {(ShowResourceLabels ? "hide" : "show")} creator/cargo role & transfer status text overlays on the map.\n<i>(Critical 'low mats' / 'no mats' warnings are always kept visible.)</i>";
            if (labelDef.DisplayButton(new ToolTip(tip, 260f)) == InputType.LeftClick)
            {
                ShowResourceLabels = !ShowResourceLabels;
            }
        }

        /// <summary>
        /// Draws the icon role selector and, when expanded, the role and shape selection rows for the specified panel.
        /// </summary>
        public static void DrawIconRoleRow(ForceButtonPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            List<Force> forces = _forcesRef(panel);
            if (forces == null || forces.Count == 0)
            {
                return;
            }

            // 1. Dropdown Toggle Button ("icon button")
            GUILayout.BeginHorizontal();
            UiDef toggleDef = GetOrCreateToggleUiDef();
            toggleDef.SetTexture(VertexIconShapes.GetToggleButtonTexture());
            toggleDef.ClearOverrides();
            toggleDef.Width = 0.8f;
            toggleDef.Height = 0.8f;
            toggleDef.ForegroundTint = _isIconMenuExpanded ? Color.green : Color.white;

            StrategicIconMode currentPrimary = IconRoleManager.GetForceIconMode(forces[0]);
            string toggleTip = $"Strategic Map Icon Selector\nCurrent mode: <color=#80ffff>{currentPrimary}</color>\nClick to {(_isIconMenuExpanded ? "hide" : "show")} role & shape icon rows.";
            if (toggleDef.DisplayButton(new ToolTip(toggleTip, 260f)) == InputType.LeftClick)
            {
                _isIconMenuExpanded = !_isIconMenuExpanded;
            }
            GUILayout.EndHorizontal();

            if (!_isIconMenuExpanded)
            {
                return;
            }

            // 2. Row 1: Vertex Role Designs
            DrawModeButtonRow(_vertexRoleOrder, forces);

            // 3. Row 2: Simple Geometric Shapes
            DrawModeButtonRow(_simpleShapesOrder, forces);
        }

        /// <summary>
        /// Draws a horizontal row of buttons for the specified strategic icon modes and applies the selected mode toall forces.
        /// </summary>
        private static void DrawModeButtonRow(StrategicIconMode[] rowModes, List<Force> forces)
        {
            GUILayout.BeginHorizontal();

            for (int i = 0; i < rowModes.Length; i++)
            {
                StrategicIconMode mode = rowModes[i];
                UiDef def = GetOrCreateUiDef(mode);

                // Ensure procedural Texture2D is still alive (Unity can destroy non-asset textures on scene reload)
                Texture2D tex = VertexIconShapes.GetButtonTexture(mode);
                def.SetTexture(tex);

                int matchingCount = 0;
                for (int fIdx = 0; fIdx < forces.Count; fIdx++)
                {
                    if (IconRoleManager.GetForceIconMode(forces[fIdx]) == mode)
                    {
                        matchingCount++;
                    }
                }

                Color tint = Color.white;
                if (matchingCount == forces.Count)
                {
                    tint = Color.green;
                }
                else if (matchingCount > 0)
                {
                    tint = Color.cyan;
                }

                def.ClearOverrides();
                def.Width = 0.65f;
                def.Height = 0.65f;
                def.ForegroundTint = tint;

                string tipText = GetTooltipText(mode, forces);
                InputType input = def.DisplayButton(new ToolTip(tipText, 260f));
                if (input == InputType.LeftClick)
                {
                    for (int fIdx = 0; fIdx < forces.Count; fIdx++)
                    {
                        IconRoleManager.SetForceIconMode(forces[fIdx], mode);
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        private static UiDef GetOrCreateToggleUiDef()
        {
            if (_toggleMenuUiDef != null)
            {
                return _toggleMenuUiDef;
            }

            _toggleMenuUiDef = new UiDef("BSM_IconRole_ToggleMenu", Guid.Empty);
            _toggleMenuUiDef.SetTexture(VertexIconShapes.GetToggleButtonTexture());
            return _toggleMenuUiDef;
        }

        private static UiDef GetOrCreateLabelToggleUiDef()
        {
            if (_toggleLabelsUiDef != null)
            {
                return _toggleLabelsUiDef;
            }

            // Uses the native in-game Annotations tag icon asset GUID (FtdUiDefs.ANNOTATIONS)
            _toggleLabelsUiDef = new UiDef("BSM_ResourceLabels_Toggle", new Guid("1804f0f5-ba9a-4ceb-a6c5-a763942bf2d8"));
            return _toggleLabelsUiDef;
        }

        private static UiDef GetOrCreateUiDef(StrategicIconMode mode)
        {
            if (_uiDefs.TryGetValue(mode, out UiDef existing) && existing != null)
            {
                return existing;
            }

            string ttId = $"BSM_IconRole_{mode}";
            UiDef def = new UiDef(ttId, Guid.Empty);
            def.SetTexture(VertexIconShapes.GetButtonTexture(mode));
            _uiDefs[mode] = def;
            return def;
        }

        private static string GetTooltipText(StrategicIconMode mode, List<Force> forces)
        {
            switch (mode)
            {
                case StrategicIconMode.DefaultEmblem:
                    return "Strategic Map Icon: <color=#80ff80>Default Emblem</color>\nUses your standard player/faction custom emblem icon on the map.";
                case StrategicIconMode.Auto:
                {
                    string autoSummary = "";
                    if (forces.Count > 0 && forces[0] != null)
                    {
                        StrategicIconMode autoResolved = IconRoleManager.ClassifyForceAuto(forces[0]);
                        autoSummary = $"\nCurrently auto-detects as: <color=#80ffff>{autoResolved}</color>";
                    }
                    return "Strategic Map Icon: <color=#80ff80>Auto-Detect Role</color>\nAutomatically selects a vertex icon based on vehicle weapons, resource harvesters, supply group role, radar/speed, or repair equipment." + autoSummary;
                }
                case StrategicIconMode.Water_Combat:
                    return "Strategic Map Icon: <color=#80ff80>Water</color>\nDirectional naval warship icon.";
                case StrategicIconMode.Air_Combat:
                    return "Strategic Map Icon: <color=#80ff80>Air</color>\nDirectional combat aircraft icon.";
                case StrategicIconMode.Land_Combat:
                    return "Strategic Map Icon: <color=#80ff80>Land</color>\nDirectional armored vehicle icon.";
                case StrategicIconMode.Orbital_Combat:
                    return "Strategic Map Icon: <color=#80ff80>Orbital</color>\nOrbital station or satellite.";
                case StrategicIconMode.Fortress:
                    return "Strategic Map Icon: <color=#80ff80>Fortress</color>\nStationary star fortress icon.";
                case StrategicIconMode.Harvester:
                    return "Strategic Map Icon: <color=#80ff80>Resource Harvester / Creator</color>\nFor resource zone harvesters and refinery bases.";
                case StrategicIconMode.Cargo:
                    return "Strategic Map Icon: <color=#80ff80>Cargo / Logistics Transport</color>\nFor supply line transports.";
                case StrategicIconMode.Scout:
                    return "Strategic Map Icon: <color=#80ff80>Scout / Radar / Satellite</color>\nFor reconnaissance and surveillance purposes, or balloons.";
                case StrategicIconMode.Support:
                    return "Strategic Map Icon: <color=#80ff80>Support / Repair</color>\nStar shape.";
                case StrategicIconMode.Shapes_Circle:
                    return "Simple Shape Icon: <color=#80ff80>Circle</color>\nCircle.";
                case StrategicIconMode.Shapes_Triangle:
                    return "Simple Shape Icon: <color=#80ff80>Triangle (Directional)</color>\nTriangle.";
                case StrategicIconMode.Shapes_Square:
                    return "Simple Shape Icon: <color=#80ff80>Square</color>\nSquare.";
                case StrategicIconMode.Shapes_Diamond:
                    return "Simple Shape Icon: <color=#80ff80>Diamond</color>\n45 deg Square.";
                case StrategicIconMode.Shapes_Pentagon:
                    return "Simple Shape Icon: <color=#80ff80>Pentagon</color>\nPentagon.";
                case StrategicIconMode.Shapes_Hexagon:
                    return "Simple Shape Icon: <color=#80ff80>Hexagon</color>\nHexagon.";
                case StrategicIconMode.Shapes_Octagon:
                    return "Simple Shape Icon: <color=#80ff80>Octagon</color>\nOctagon.";
                case StrategicIconMode.Shapes_Cross:
                    return "Simple Shape Icon: <color=#80ff80>Cross / Plus</color>\nCross / Plus.";
                case StrategicIconMode.Shapes_Star:
                    return "Simple Shape Icon: <color=#80ff80>Star</color>\nStar.";
                case StrategicIconMode.Shapes_Chevron:
                    return "Simple Shape Icon: <color=#80ff80>Chevron (Directional)</color>\nChevron.";
                default:
                    return "Strategic Map Icon Mode";
            }
        }
    }

    /// <summary>
    /// Provides Harmony patches for resource shortage warning overlays.
    /// </summary>
    [HarmonyPatch(typeof(ResourceShortageWarningOverlay))]
    public static class ResourceShortageWarningOverlayPatch
    {
        private static readonly Action<ResourceShortageWarningOverlay, Force, bool>? _displayDelegate = CreateDisplayDelegate();

        // Creates a delegate for the <c>Display</c> method.A delegate for the <c>Display</c> method, or <see langword="null"/> if the method cannot be found.
        private static Action<ResourceShortageWarningOverlay, Force, bool>? CreateDisplayDelegate()
        {
            MethodInfo? method = AccessTools.Method(typeof(ResourceShortageWarningOverlay), "Display", new[] { typeof(Force), typeof(bool) });
            if (method == null)
            {
                return null;
            }
            return (Action<ResourceShortageWarningOverlay, Force, bool>)Delegate.CreateDelegate(
                typeof(Action<ResourceShortageWarningOverlay, Force, bool>), method);
        }

        [HarmonyPatch("DisplayAllInYourTeam")]
        [HarmonyPrefix]
        public static bool Prefix_DisplayAllInYourTeam(ResourceShortageWarningOverlay __instance, bool onMap)
        {
            if (_displayDelegate == null || ForceManager.Instance?.forces == null)
            {
                return true;
            }

            bool showAllLabels = ForceButtonPanelPatch.ShowResourceLabels;

            // Always allow SupplyType.User ("low mats" / "no mats") warnings so the player knows if a ship is low for materials; only draw Creator / Cargo role & status labels when toggled ON.
            float flashPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            Color flashRedColor = Color.Lerp(Color.white, new Color(1f, 0.15f, 0.15f, 1f), flashPulse);

            List<Force> forces = ForceManager.Instance.forces;
            for (int i = 0; i < forces.Count; i++)
            {
                Force force = forces[i];
                if (force != null && force.FactionId == GAME_STATE.MyTeam && force.IsVisible)
                {
                    if (force.SupplyGroup == SupplyType.User)
                    {
                        Color prevColor = GUI.color;
                        GUI.color = flashRedColor;
                        _displayDelegate(__instance, force, onMap);
                        GUI.color = prevColor;
                    }
                    else if (showAllLabels)
                    {
                        _displayDelegate(__instance, force, onMap);
                    }
                }
            }

            return false;
        }
    }
}
