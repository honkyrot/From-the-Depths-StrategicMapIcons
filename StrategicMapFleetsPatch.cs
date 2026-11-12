using System;
using System.Collections.Generic;
using System.Reflection;
using BrilliantSkies.Core.Cameras;
using BrilliantSkies.Core.Constants;
using BrilliantSkies.Core.Drawing;
using BrilliantSkies.Core.Types;
using BrilliantSkies.Core.UiSounds;
using BrilliantSkies.Core.Widgets;
using BrilliantSkies.FromTheDepths.Game;
using BrilliantSkies.FromTheDepths.Game.Battle;
using BrilliantSkies.FromTheDepths.Planets.Map;
using BrilliantSkies.Ftd;
using BrilliantSkies.Ftd.Game.FactionInfoUi;
using BrilliantSkies.Ftd.Planets.Factions;
using BrilliantSkies.ScriptableObjects;
using BrilliantSkies.Ui.Displayer;
using BrilliantSkies.Ui.Layouts;
using BrilliantSkies.Ui.Tips;
using HarmonyLib;
using UnityEngine;

namespace StrategicMapIcons
{
    public class FleetScreenEntry
    {
        public int FleetIndex;
        public Fleet Fleet = null!;
        public FactionSpecificationFaction Faction = null!;
        public StrategicIconMode ResolvedMode;
        public Force? RepresentativeForce;
        public Vector2 TrueGuiPos;
        public Vector2 DisplayGuiPos;
        public float ScreenDepthZ;
        public Vector2 ForwardScreenDir; // In GUI space (+X right, -Y up)
        public float MaterialCost;
        public int CostTier; // 0..4
        public float VisualRadiusPx;
        public float AnchorWeight;
        public bool IsSelected;
        public bool IsPlayerTeam;
        public bool IsStationary;
        public bool IsDeadBlueprint;
        public string TooltipId = "";
    }

    [HarmonyPatch(typeof(StrategicMapControlActiveGUI))]
    public static class StrategicMapFleetsPatch
    {
        private static readonly AccessTools.FieldRef<StrategicMapControlActiveGUI, SO_Map> _soRef =
            AccessTools.FieldRefAccess<StrategicMapControlActiveGUI, SO_Map>("SO");

        private static readonly AccessTools.FieldRef<StrategicMapControlActiveGUI, List<string>> _toolTipStringsRef =
            AccessTools.FieldRefAccess<StrategicMapControlActiveGUI, List<string>>("_toolTipStrings");

        private static readonly List<FleetScreenEntry> _frameEntries = new List<FleetScreenEntry>(128);
        private static readonly List<FleetScreenEntry> _entryPool = new List<FleetScreenEntry>(128);
        private static int _lastComputedFrame = -1;

        private static GUIStyle? _badgeStyle;
        private static GUIStyle? _emptyButtonStyle;

        [HarmonyPatch(nameof(StrategicMapControlActiveGUI.RegularUpdateEntry))]
        [HarmonyPostfix]
        public static void Postfix_RegularUpdateEntry(StrategicMapControlActiveGUI __instance)
        {
            if (!SMI_ModSettings.Enabled)
            {
                return;
            }

            try
            {
                EnsureLayoutComputed(__instance);
                DrawAllFleetVertexIcons();
            }
            catch (Exception ex)
            {
                BrilliantSkies.Core.Logger.AdvLogger.LogError($"[StrategicMapIcons] Error in Postfix_RegularUpdateEntry: {ex}", BrilliantSkies.Core.Logger.LogOptions.OnlyInDeveloperLog);
            }
        }

        [HarmonyPatch("GUI_Fleets")]
        [HarmonyPrefix]
        public static bool Prefix_GUI_Fleets(StrategicMapControlActiveGUI __instance)
        {
            if (!SMI_ModSettings.Enabled)
            {
                return true;
            }

            try
            {
                EnsureLayoutComputed(__instance);
                RenderFleetsGui(__instance);
                return false; // Replace vanilla GUI_Fleets
            }
            catch (Exception ex)
            {
                BrilliantSkies.Core.Logger.AdvLogger.LogError($"[StrategicMapIcons] Error in Prefix_GUI_Fleets: {ex}", BrilliantSkies.Core.Logger.LogOptions.OnlyInDeveloperLog);
                return true; // Fallback to vanilla on error
            }
        }

        private static void EnsureLayoutComputed(StrategicMapControlActiveGUI instance)
        {
            int frame = Time.frameCount;
            if (_lastComputedFrame == frame)
            {
                return;
            }
            _lastComputedFrame = frame;
            for (int e = 0; e < _frameEntries.Count; e++)
            {
                _entryPool.Add(_frameEntries[e]);
            }
            _frameEntries.Clear();

            if (FleetManager.Instance == null || FactionSpecifications.i == null)
            {
                return;
            }

            Camera? fgCam = CameraManager.GetSingleton()?.foregroundCamera;
            if (fgCam == null)
            {
                return;
            }

            List<string> toolTipStrings = _toolTipStringsRef(instance);
            int numFleets = FleetManager.Instance.GetNumberOfFleets();

            for (int ii = 0; ii < numFleets; ii++)
            {
                while (toolTipStrings.Count <= ii)
                {
                    toolTipStrings.Add("zzF" + toolTipStrings.Count);
                }

                Fleet F = FleetManager.Instance.GetFleet(ii);
                if (F == null || F.forces == null || F.forces.Count == 0)
                {
                    continue;
                }

                bool isPlayerTeam = F.FactionId == GAME_STATE.MyTeam;
                if (!isPlayerTeam && !F.Visible)
                {
                    continue;
                }

                Vector2i guiPt = MapToWorld.GamePositionToGui(F.CurrentPosition, out bool isOnScreen);
                if (!isOnScreen)
                {
                    continue;
                }

                FactionSpecificationFaction faction = FactionSpecifications.i.GetFaction(F.FactionId);
                if (faction == null)
                {
                    continue;
                }

                Vector3 mapPos = MapToWorld.GameToGameOnMap(F.CurrentPosition);
                Vector3 sp0 = fgCam.WorldToScreenPoint(mapPos);
                if (sp0.z <= 0.01f)
                {
                    continue;
                }

                // Compute 2D forward vector on screen taking camera orientation into account
                Vector3 worldFwd = F.CurrentRotation * Vector3.forward;
                Vector3 mapAhead = MapToWorld.GameToGameOnMap(F.CurrentPosition + worldFwd * 25f);
                Vector3 sp1 = fgCam.WorldToScreenPoint(mapAhead);
                Vector2 screenDelta = new Vector2(sp1.x - sp0.x, sp1.y - sp0.y);
                Vector2 fwdScreenDir = screenDelta.sqrMagnitude > 0.0001f ? screenDelta.normalized : new Vector2(0f, 1f);

                StrategicIconMode resolvedMode = IconRoleManager.ResolveFleetRole(F, out Force? repForce);
                float matCost = (float)F.CalculateMaterialCost();
                int costTier = ComputeCostTier(matCost, F.IsHq);
                bool isSelected = FFSelection.data.SelectedFleets.Contains(F);
                bool isStationary = F.GetFleetType() == enumFleetType.stationary;

                // acquire number and scale
                float baseRadiusPx = GetTierRadiusPx(costTier, resolvedMode, F.IsHq);
                baseRadiusPx *= SMI_ModSettings.IconScaleMultiplier;

                if (isSelected)
                {
                    baseRadiusPx += 3f;
                }

                // Higher anchor weight means the icon resists moving when overlapping smaller icons
                float anchorWeight = Mathf.Clamp(1f + Mathf.Log10(Mathf.Max(1000f, matCost)), 2f, 10f);
                if (isStationary || F.IsHq)
                {
                    anchorWeight *= 3.5f;
                }

                Vector2 trueGui = new Vector2(guiPt.x, guiPt.y);
                FleetScreenEntry entry;
                if (_entryPool.Count > 0)
                {
                    int lastIdx = _entryPool.Count - 1;
                    entry = _entryPool[lastIdx];
                    _entryPool.RemoveAt(lastIdx);
                }
                else
                {
                    entry = new FleetScreenEntry();
                }

                entry.FleetIndex = ii;
                entry.Fleet = F;
                entry.Faction = faction;
                entry.ResolvedMode = resolvedMode;
                entry.RepresentativeForce = repForce;
                entry.TrueGuiPos = trueGui;
                entry.DisplayGuiPos = trueGui;
                entry.ScreenDepthZ = sp0.z;
                entry.ForwardScreenDir = fwdScreenDir;
                entry.MaterialCost = matCost;
                entry.CostTier = costTier;
                entry.VisualRadiusPx = baseRadiusPx;
                entry.AnchorWeight = anchorWeight;
                entry.IsSelected = isSelected;
                entry.IsPlayerTeam = isPlayerTeam;
                entry.IsStationary = isStationary;
                entry.IsDeadBlueprint = F.IsDeadBlueprintFleet();
                entry.TooltipId = toolTipStrings[ii];

                _frameEntries.Add(entry);
            }

            if (SMI_ModSettings.SeparateOverlappingIcons)
            {
                ResolveScreenOverlaps(_frameEntries);
            }
        }

        private static int ComputeCostTier(float matCost, bool isHq)
        {
            if (isHq || matCost >= SMI_ModSettings.Tier5MinMaterialCost) return 5; // Tier 5
            if (matCost >= SMI_ModSettings.Tier4MinMaterialCost) return 4;          // Tier 4
            if (matCost >= SMI_ModSettings.Tier3MinMaterialCost) return 3;          // Tier 3
            if (matCost >= SMI_ModSettings.Tier2MinMaterialCost) return 2;           // Tier 2
            if (matCost >= SMI_ModSettings.Tier1MinMaterialCost) return 1;           // Tier 1
            return 0;                                  // < 10k Drone / Probe / Minor
        }

        private static float GetTierRadiusPx(int costTier, StrategicIconMode mode, bool isHq)
        {
            if (mode == StrategicIconMode.DefaultEmblem)
            {
                return isHq ? 28f : (14f + costTier * 3f);
            }

            // return a simple number based on cost tier, optionally scaled by material concentration if enabled
            if (!SMI_ModSettings.ScaleByMaterials)
            {
                return 20f;
            }

            // scale with mat cost and exponent
            float tierScale = 12f + (costTier * SMI_ModSettings.ScalingExponent);
            return Mathf.Clamp(tierScale * SMI_ModSettings.IconScaleMultiplier, 12f, 60f);

            //// Vertex icons scale clearly with fleet material concentration
            //return costTier switch
            //{
            //    0 => 12f, // 24px diameter
            //    1 => 15f, // 30px diameter
            //    2 => 18f, // 36px diameter
            //    3 => 22f, // 44px diameter
            //    4 => 26f, // 52px diameter
            //    5 => 30f, // 60px diameter
            //    _ => 15f
            //};
        }

        /// <summary>
        /// Separates overlapping fleet icons in screen space so small fleets next to large fleets
        /// are never occluded or unclickable. Heavier/stationary fleets stay anchored while lighter
        /// adjacent fleets fan out cleanly around them with a leader line back to their true position.
        /// </summary>
        private static void ResolveScreenOverlaps(List<FleetScreenEntry> entries)
        {
            int count = entries.Count;
            if (count <= 1)
            {
                return;
            }

            const int iterations = 8;
            const float paddingPx = 6f;
            const float maxDisplacementPx = 72f;

            for (int iter = 0; iter < iterations; iter++)
            {
                bool anyMoved = false;
                for (int i = 0; i < count; i++)
                {
                    FleetScreenEntry a = entries[i];
                    for (int j = i + 1; j < count; j++)
                    {
                        FleetScreenEntry b = entries[j];
                        float minDist = a.VisualRadiusPx + b.VisualRadiusPx + paddingPx;
                        Vector2 delta = b.DisplayGuiPos - a.DisplayGuiPos;
                        float distSq = delta.sqrMagnitude;

                        if (distSq < minDist * minDist)
                        {
                            float dist = Mathf.Sqrt(distSq);
                            Vector2 dir;
                            if (dist < 0.5f)
                            {
                                // Deterministic golden-angle fan-out when two fleets share identical coordinates
                                float angle = ((a.FleetIndex * 137.5f + b.FleetIndex * 53.1f) % 360f) * Mathf.Deg2Rad;
                                dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            }
                            else
                            {
                                dir = delta / dist;
                            }

                            float overlap = (minDist - dist) * 0.65f;
                            float totalWeight = a.AnchorWeight + b.AnchorWeight;
                            float moveShareA = b.AnchorWeight / totalWeight;
                            float moveShareB = a.AnchorWeight / totalWeight;

                            a.DisplayGuiPos -= dir * (overlap * moveShareA);
                            b.DisplayGuiPos += dir * (overlap * moveShareB);
                            anyMoved = true;
                        }
                    }
                }

                // Gentle spring pull toward true position and clamp max displacement
                for (int i = 0; i < count; i++)
                {
                    FleetScreenEntry e = entries[i];
                    Vector2 offset = e.DisplayGuiPos - e.TrueGuiPos;
                    float len = offset.magnitude;
                    if (len > maxDisplacementPx)
                    {
                        e.DisplayGuiPos = e.TrueGuiPos + (offset / len) * maxDisplacementPx;
                    }
                    e.DisplayGuiPos = new Vector2(
                        Mathf.Clamp(e.DisplayGuiPos.x, e.VisualRadiusPx + 4f, Screen.width - e.VisualRadiusPx - 4f),
                        Mathf.Clamp(e.DisplayGuiPos.y, e.VisualRadiusPx + 4f, Screen.height - e.VisualRadiusPx - 4f));
                }

                if (!anyMoved)
                {
                    break;
                }
            }
        }

        private static void DrawAllFleetVertexIcons()
        {
            Camera? fgCam = CameraManager.GetSingleton()?.foregroundCamera;
            if (fgCam == null || VectorLines.i?.Current == null)
            {
                return;
            }

            IVectorLineWrapper vl = VectorLines.i.Current;

            for (int i = 0; i < _frameEntries.Count; i++)
            {
                FleetScreenEntry entry = _frameEntries[i];
                Color primaryColor = GetFleetPrimaryColor(entry);

                // If icon was displaced by anti-overlap solver, draw leader line + anchor pin at TrueGuiPos
                Vector2 dispOffset = entry.DisplayGuiPos - entry.TrueGuiPos;
                float dispDist = dispOffset.magnitude;
                if (dispDist > entry.VisualRadiusPx * 0.45f && dispDist > 5f)
                {
                    Vector2 dir = dispOffset / dispDist;
                    Vector2 lineEndGui = entry.DisplayGuiPos - dir * (entry.VisualRadiusPx * 0.92f);
                    Color leaderColor = new(primaryColor.r, primaryColor.g, primaryColor.b, 0.75f);

                    DrawGuiLineIn3D(vl, fgCam, entry.TrueGuiPos, lineEndGui, entry.ScreenDepthZ, leaderColor, 1.5f);

                    // Small anchor diamond at TrueGuiPos
                    const float pinR = 3.2f;
                    Vector2 pTop = entry.TrueGuiPos + new Vector2(0f, -pinR);
                    Vector2 pRight = entry.TrueGuiPos + new Vector2(pinR, 0f);
                    Vector2 pBot = entry.TrueGuiPos + new Vector2(0f, pinR);
                    Vector2 pLeft = entry.TrueGuiPos + new Vector2(-pinR, 0f);
                    DrawGuiLineIn3D(vl, fgCam, pTop, pRight, entry.ScreenDepthZ, leaderColor, 1.5f);
                    DrawGuiLineIn3D(vl, fgCam, pRight, pBot, entry.ScreenDepthZ, leaderColor, 1.5f);
                    DrawGuiLineIn3D(vl, fgCam, pBot, pLeft, entry.ScreenDepthZ, leaderColor, 1.5f);
                    DrawGuiLineIn3D(vl, fgCam, pLeft, pTop, entry.ScreenDepthZ, leaderColor, 1.5f);
                }

                // Skip vertex icon body if player explicitly chose DefaultEmblem (GUI_Fleets draws the emblem texture)
                if (entry.ResolvedMode == StrategicIconMode.DefaultEmblem)
                {
                    continue;
                }

                LineSegment2D[] segments = VertexIconShapes.GetSegmentsForMap(
                    entry.ResolvedMode,
                    entry.RepresentativeForce,
                    out bool rotateWithHeading);

                Vector2 fwd2D = rotateWithHeading ? entry.ForwardScreenDir : new Vector2(0f, 1f);
                Vector2 right2D = new(fwd2D.y, -fwd2D.x);
                float radius = entry.VisualRadiusPx;
                float lineWidth = GetTierLineWidth(entry.CostTier, entry.IsSelected);

                // draw main pass
                for (int s = 0; s < segments.Length; s++)
                {
                    Vector2 aGui = TransformLocalToGui(segments[s].A, entry.DisplayGuiPos, right2D, fwd2D, radius);
                    Vector2 bGui = TransformLocalToGui(segments[s].B, entry.DisplayGuiPos, right2D, fwd2D, radius);
                    DrawGuiLineIn3D(vl, fgCam, aGui, bGui, entry.ScreenDepthZ, primaryColor, lineWidth);
                }

                // Draw Fleet Cost / Concentration Tier Chevrons & Outer Ring (for Tier 2..4)
                DrawCostTierIndicators(vl, fgCam, entry, right2D, fwd2D, primaryColor);

                // Draw Selection Brackets if selected
                if (entry.IsSelected)
                {
                    DrawSelectionBrackets(vl, fgCam, entry.DisplayGuiPos, radius + 4.5f, entry.ScreenDepthZ, new Color(0.45f, 1f, 0.45f, 0.95f), 2.2f);
                }
            }
        }

        private static void DrawCostTierIndicators(
            IVectorLineWrapper vl,
            Camera fgCam,
            FleetScreenEntry entry,
            Vector2 right2D,
            Vector2 fwd2D,
            Color primaryColor)
        {
            if (entry.CostTier < 2)
            {
                return;
            }

            float radius = entry.VisualRadiusPx;
            Color tierColor = Color.Lerp(primaryColor, Color.white, 0.35f);

            if (entry.CostTier >= 5)
            {
                // Draw the rank star behind/below the fleet marker.
                const int pointCount = 5;
                const float centerY = -1.5f;
                const float outerRadius = 0.46f;
                const float innerRadius = 0.20f;

                Vector2 previous = default;
                for (int i = 0; i <= pointCount * 2; i++)
                {
                    float angle = -Mathf.PI * 0.5f + i * Mathf.PI / pointCount;
                    float pointRadius = i % 2 == 0 ? outerRadius : innerRadius;

                    Vector2 point = new(
                        Mathf.Cos(angle) * pointRadius,
                        centerY + Mathf.Sin(angle) * pointRadius);

                    if (i > 0)
                    {
                        Vector2 guiPrevious = TransformLocalToGui(
                            previous, entry.DisplayGuiPos, right2D, fwd2D, radius);
                        Vector2 guiPoint = TransformLocalToGui(
                            point, entry.DisplayGuiPos, right2D, fwd2D, radius);

                        DrawGuiLineIn3D(
                            vl, fgCam, guiPrevious, guiPoint,
                            entry.ScreenDepthZ, tierColor, 1.8f);
                    }

                    previous = point;
                }
            }
            else
            {
                int chevronCount = entry.CostTier - 1; // Tier 2 -> 1, Tier 3 -> 2, Tier 4 -> 3

                // Draw aft/bottom rank chevrons.
                for (int c = 0; c < chevronCount; c++)
                {
                    float yBase = -1.06f - c * 0.18f;
                    Vector2 pLeft = new(-0.42f, yBase + 0.10f);
                    Vector2 pMid = new(0f, yBase - 0.04f);
                    Vector2 pRight = new(0.42f, yBase + 0.10f);

                    Vector2 guiL = TransformLocalToGui(
                        pLeft, entry.DisplayGuiPos, right2D, fwd2D, radius);
                    Vector2 guiM = TransformLocalToGui(
                        pMid, entry.DisplayGuiPos, right2D, fwd2D, radius);
                    Vector2 guiR = TransformLocalToGui(
                        pRight, entry.DisplayGuiPos, right2D, fwd2D, radius);

                    DrawGuiLineIn3D(
                        vl, fgCam, guiL, guiM,
                        entry.ScreenDepthZ, tierColor, 1.8f);
                    DrawGuiLineIn3D(
                        vl, fgCam, guiM, guiR,
                        entry.ScreenDepthZ, tierColor, 1.8f);
                }
            }
        }

        private static void DrawSelectionBrackets(
            IVectorLineWrapper vl,
            Camera fgCam,
            Vector2 centerGui,
            float halfSize,
            float depthZ,
            Color color,
            float width)
        {
            float leg = halfSize * 0.42f;
            // Top-Left
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(-halfSize, -halfSize + leg), centerGui + new Vector2(-halfSize, -halfSize), depthZ, color, width);
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(-halfSize, -halfSize), centerGui + new Vector2(-halfSize + leg, -halfSize), depthZ, color, width);
            // Top-Right
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(halfSize - leg, -halfSize), centerGui + new Vector2(halfSize, -halfSize), depthZ, color, width);
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(halfSize, -halfSize), centerGui + new Vector2(halfSize, -halfSize + leg), depthZ, color, width);
            // Bottom-Right
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(halfSize, halfSize - leg), centerGui + new Vector2(halfSize, halfSize), depthZ, color, width);
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(halfSize, halfSize), centerGui + new Vector2(halfSize - leg, halfSize), depthZ, color, width);
            // Bottom-Left
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(-halfSize + leg, halfSize), centerGui + new Vector2(-halfSize, halfSize), depthZ, color, width);
            DrawGuiLineIn3D(vl, fgCam, centerGui + new Vector2(-halfSize, halfSize), centerGui + new Vector2(-halfSize, halfSize - leg), depthZ, color, width);
        }

        private static Vector2 TransformLocalToGui(Vector2 localPt, Vector2 centerGui, Vector2 right2D, Vector2 fwd2D, float radiusPx)
        {
            // right2D and fwd2D are in Unity Screen coordinates (+X right, +Y up).
            // In GUI coordinates, +X is right and +Y is down, so we negate the Y component of the offset.
            Vector2 screenOffset = (right2D * localPt.x + fwd2D * localPt.y) * radiusPx;
            return new Vector2(centerGui.x + screenOffset.x, centerGui.y - screenOffset.y);
        }

        private static void DrawGuiLineIn3D(IVectorLineWrapper vl, Camera fgCam, Vector2 guiA, Vector2 guiB, float depthZ, Color color, float width)
        {
            Vector3 worldA = fgCam.ScreenToWorldPoint(new Vector3(guiA.x, Screen.height - guiA.y, depthZ));
            Vector3 worldB = fgCam.ScreenToWorldPoint(new Vector3(guiB.x, Screen.height - guiB.y, depthZ));
            vl.Line(worldA, worldB, color, width, false);
        }

        private static float GetTierLineWidth(int costTier, bool isSelected)
        {
            float w = costTier switch
            {
                0 => 1.8f,
                1 => 2.0f,
                2 => 2.2f,
                3 => 2.5f,
                4 => 2.8f,
                _ => 2.0f
            };
            return isSelected ? w + 0.5f : w;
        }

        private static Color GetFleetPrimaryColor(FleetScreenEntry entry)
        {
            return GetRoleOrFactionColor(
                entry.ResolvedMode,
                entry.IsPlayerTeam,
                entry.Faction,
                entry.IsSelected,
                entry.IsDeadBlueprint);
        }

        internal static Color GetRoleOrFactionColor(
            StrategicIconMode resolvedMode,
            bool isPlayerTeam,
            FactionSpecificationFaction? faction,
            bool isSelected,
            bool isDeadBlueprint)
        {
            if (isDeadBlueprint)
            {
                return new Color(0.55f, 0.55f, 0.55f, 0.9f);
            }

            if (!isPlayerTeam)
            {
                // Keep non-player factions clearly colored by their faction color
                if (faction != null)
                {
                    Color fc = faction.Color;
                    return new Color(
                        Mathf.Clamp01(fc.r * 0.85f + 0.15f),
                        Mathf.Clamp01(fc.g * 0.85f + 0.15f),
                        Mathf.Clamp01(fc.b * 0.85f + 0.15f),
                        1f);
                }

                return Color.red;
            }

            // player color
            Color roleColor = SMI_ModSettings.TeamColor;

            if (isSelected)
            {
                roleColor = Color.Lerp(roleColor, Color.white, 0.35f);
            }

            return roleColor;
        }

        private static void RenderFleetsGui(StrategicMapControlActiveGUI instance)
        {
            SO_Map so = _soRef(instance);
            EnsureStylesInitialized();

            Vector2 mousePos = Event.current.mousePosition;

            // Find the single closest fleet entry containing the mouse cursor (giving small fleets priority over large rects)
            int hoveredEntryIndex = FindBestHoveredEntryIndex(mousePos);

            using (RotatedUi rotatedUi = new RotatedUi(CameraManager.GetSingleton().CameraTransformer))
            {
                for (int i = 0; i < _frameEntries.Count; i++)
                {
                    FleetScreenEntry entry = _frameEntries[i];
                    Fleet F = entry.Fleet;
                    FactionSpecificationFaction faction = entry.Faction;

                    float boxSize = entry.VisualRadiusPx * 2f;
                    Rect rect = new Rect(
                        entry.DisplayGuiPos.x - boxSize * 0.5f,
                        entry.DisplayGuiPos.y - boxSize * 0.5f,
                        boxSize,
                        boxSize);

                    bool clicked = false;

                    if (entry.ResolvedMode == StrategicIconMode.DefaultEmblem)
                    {
                        // Render vanilla emblem texture at the de-cluttered DisplayGuiPos
                        GUI.backgroundColor = faction.Color;
                        if (entry.IsStationary)
                        {
                            int pad = 4;
                            Rect bgRect = new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2, rect.height + pad * 2);
                            GUI.backgroundColor = faction.ContrastingColor;
                            GUI.contentColor = faction.ContrastingColor;
                            clicked = rotatedUi.RotateGuiElement(bgRect, F.CurrentRotation.eulerAngles.y + 180f,
                                () => GUI.Button(bgRect, new GUIContent("", F.IsHq ? faction.Art.OverridenEmblemLarge : faction.Art.OverridenEmblemSmall, entry.TooltipId), so.STYLE_FleetIcons));
                            GUI.backgroundColor = Color.white;
                            GUI.contentColor = Color.white;
                        }
                        if (entry.IsDeadBlueprint)
                        {
                            GUI.backgroundColor = Color.grey;
                            GUI.contentColor = Color.grey;
                        }
                        clicked = clicked || rotatedUi.RotateGuiElement(rect, F.CurrentRotation.eulerAngles.y + 180f,
                            () => GUI.Button(rect, new GUIContent("", F.IsHq ? faction.Art.OverridenEmblemLarge : faction.Art.OverridenEmblemSmall, entry.TooltipId), so.STYLE_FleetIcons));
                        GUI.backgroundColor = Color.white;
                        GUI.contentColor = Color.white;
                    }
                    else
                    {
                        // Vertex icon is drawn via VectorLines; render invisible click/tooltip button over its de-cluttered rect
                        // Only allow click/tooltip if no other closer fleet owns the cursor position
                        if (hoveredEntryIndex == -1 || hoveredEntryIndex == i)
                        {
                            clicked = GUI.Button(rect, new GUIContent("", entry.TooltipId), _emptyButtonStyle!);
                        }
                    }

                    // Draw compact material/count badge below medium/heavy/multi-ship fleets so player sees force concentration
                    int forceCount = F.forces.Count;
                    if (entry.CostTier >= 2 || forceCount > 1)
                    {
                        string badgeText = FormatFleetBadge(entry.MaterialCost, forceCount, entry.CostTier >= 2);
                        if (!string.IsNullOrEmpty(badgeText))
                        {
                            Rect badgeRect = new Rect(entry.DisplayGuiPos.x - 42f, entry.DisplayGuiPos.y + entry.VisualRadiusPx + 1f, 84f, 15f);
                            Color prevColor = GUI.color;
                            GUI.color = new Color(0f, 0f, 0f, 0.85f);
                            GUI.Label(new Rect(badgeRect.x + 1f, badgeRect.y + 1f, badgeRect.width, badgeRect.height), badgeText, _badgeStyle!);
                            GUI.color = GetFleetPrimaryColor(entry);
                            GUI.Label(badgeRect, badgeText, _badgeStyle!);
                            GUI.color = prevColor;
                        }
                    }

                    if (clicked && (hoveredEntryIndex == -1 || hoveredEntryIndex == i) && GuiDisplayBase.MouseOverGuiElement != MouseOverGuiElementStatus.MainSet)
                    {
                        if (DoubleClicker.Clicked(F))
                        {
                            new ForceFleetFactionInfoUi(new ForceFleetFaction(F)).ActivateGui(GuiActivateType.Stack);
                            GUISoundManager.GetSingleton().PlayBeep();
                        }
                        else if (entry.IsPlayerTeam)
                        {
                            FFSelection.data.FleetClickedMasterLogic(F);
                        }
                    }

                    GUI.backgroundColor = Color.white;
                    if ((hoveredEntryIndex == -1 || hoveredEntryIndex == i) && GUI.tooltip == entry.TooltipId)
                    {
                        instance.iPassive.MouseOverFleetTmp = F;
                        FFSelection.data.MouseOverFleet = F;
                        TipDisplayer.Instance.SetTip(F.GetToolTip());
                    }
                }
            }
        }

        private static int FindBestHoveredEntryIndex(Vector2 mousePos)
        {
            int bestIdx = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _frameEntries.Count; i++)
            {
                FleetScreenEntry e = _frameEntries[i];
                float r = e.VisualRadiusPx;
                float dx = Mathf.Abs(mousePos.x - e.DisplayGuiPos.x);
                float dy = Mathf.Abs(mousePos.y - e.DisplayGuiPos.y);
                if (dx <= r && dy <= r)
                {
                    float dist = Vector2.Distance(mousePos, e.DisplayGuiPos);
                    // Favor closer center and smaller radius so small icons are never overshadowed
                    float score = dist + r * 0.15f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIdx = i;
                    }
                }
            }

            return bestIdx;
        }

        private static string FormatFleetBadge(float matCost, int forceCount, bool showCost)
        {
            if (!SMI_ModSettings.ShowFleetCostTiers)
            {
                return forceCount > 1 ? $"[{forceCount}]" : "";
            }

            string costPart = "";
            if (showCost)
            {
                if (matCost >= 1000000f)
                {
                    costPart = $"{(matCost / 1000000f):0.0}M";
                }
                else if (matCost >= 1000f)
                {
                    costPart = $"{Mathf.RoundToInt(matCost / 1000f)}k";
                }
            }

            string countPart = forceCount > 1 ? $"[{forceCount}]" : "";
            if (costPart.Length > 0 && countPart.Length > 0)
            {
                return $"{costPart} {countPart}";
            }
            return costPart.Length > 0 ? costPart : countPart;
        }

        private static void EnsureStylesInitialized()
        {
            _emptyButtonStyle ??= new GUIStyle(GUIStyle.none);

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };
                _badgeStyle.normal.textColor = Color.white;
            }
        }
    }

    /// <summary>
    /// Draws custom vertex icons for individual craft on the Tactical Map view (when zoomed in on the WorldMap)
    /// when <see cref="SMI_ModSettings.EnableOnZoom"/> is enabled.
    /// </summary>
    [HarmonyPatch(typeof(TacticalMapControlActiveGUI))]
    public static class TacticalMapForcesPatch
    {
        private static readonly AccessTools.FieldRef<TacticalMapControlActiveGUI, SymbolDrawer> _symbolRef =
            AccessTools.FieldRefAccess<TacticalMapControlActiveGUI, SymbolDrawer>("_symbol");

        private static readonly Action<TacticalMapControlActiveGUI, Fleet>? _drawFleetOrdersDelegate =
            CreateMethodDelegate<Action<TacticalMapControlActiveGUI, Fleet>>("DrawFleetOrders", typeof(Fleet));

        private static readonly Action<TacticalMapControlActiveGUI, Force>? _drawForcePatrolWaypointsDelegate =
            CreateMethodDelegate<Action<TacticalMapControlActiveGUI, Force>>("DrawForcePatrolWaypoints", typeof(Force));

        private static readonly Action<TacticalMapControlActiveGUI, Fleet>? _drawFleetToForceConnectionLinesDelegate =
            CreateMethodDelegate<Action<TacticalMapControlActiveGUI, Fleet>>("DrawFleetToForceConnectionLines", typeof(Fleet));

        private static TDelegate? CreateMethodDelegate<TDelegate>(string methodName, Type paramType)
            where TDelegate : Delegate
        {
            MethodInfo? method = AccessTools.Method(typeof(TacticalMapControlActiveGUI), methodName, new[] { paramType });
            if (method == null)
            {
                return null;
            }
            return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }

        [HarmonyPatch("Draw_ShowForces")]
        [HarmonyPrefix]
        public static bool Prefix_Draw_ShowForces(TacticalMapControlActiveGUI __instance)
        {
            if (!SMI_ModSettings.Enabled || !SMI_ModSettings.EnableOnZoom)
            {
                return true;
            }

            if (ForceManager.Instance == null)
            {
                return true;
            }

            try
            {
                SymbolDrawer symbol = _symbolRef(__instance);
                if (symbol == null)
                {
                    return true;
                }

                bool battleCheck = !BattleOrchestrator.Active;
                int numForces = ForceManager.Instance.GetNumberOfForces();

                for (int i = 0; i < numForces; i++)
                {
                    Force f = ForceManager.Instance.GetForce(i);
                    if (f == null || !f.Visible || !f.BattleInfo.VisibleOnMap())
                    {
                        continue;
                    }

                    StrategicIconMode resolvedMode = IconRoleManager.ResolveForceRole(f);
                    bool isPlayerTeam = f.FactionId == GAME_STATE.MyTeam;
                    bool isSelected = FFSelection.data.SelectedForces.Contains(f);
                    bool isDeadBlueprint = f.State == enumForceState.Blueprint;
                    FactionSpecificationFaction? faction = FactionSpecifications.i?.GetFaction(f.FactionId);

                    Color color = StrategicMapFleetsPatch.GetRoleOrFactionColor(
                        resolvedMode,
                        isPlayerTeam,
                        faction,
                        isSelected,
                        isDeadBlueprint);

                    Vector3 gamePositionOfForce = f.GetGamePositionOfForce();
                    if (MapToWorld.IsGamePositionOnGui(gamePositionOfForce, 400f))
                    {
                        SymbolDrawerExtraMarkings extra = isSelected
                            ? SymbolDrawerExtraMarkings.BoundingBox
                            : SymbolDrawerExtraMarkings.None;

                        float lineWidth = 2f;
                        if (!f.IsInPlay)
                        {
                            const float dim = 0.7f;
                            color.r *= dim;
                            color.g *= dim;
                            color.b *= dim;
                            color.a = 0.5f;
                            lineWidth = 1f;
                        }

                        if (f == __instance.MouseOverForce)
                        {
                            lineWidth += 1f;
                        }

                        if (isDeadBlueprint)
                        {
                            color = LineColors.DeadBlueprintForce(color);
                        }

                        SymbolRequest symbolRequest = AddCustomOrVanillaForceMarker(
                            symbol,
                            f,
                            resolvedMode,
                            gamePositionOfForce,
                            f.GetGameRotationOfForce(),
                            color,
                            f.GetSize(),
                            extra,
                            lineWidth);

                        Force capturedForce = f;
                        symbolRequest.OnGUICallback = () => __instance.MouseIsOverForce(capturedForce);
                        symbolRequest.ButtonPressedCallback = () => __instance.ButtonPressedOnForce(capturedForce);
                        symbolRequest.ToolTipStringToUse = "zzf" + f.Id.ToString();

                        symbol.AddVerticalStand(gamePositionOfForce, color, 1f);
                        if (FFSelection.data.IsForceBeingTargettedBySelectedForces(f) ||
                            (__instance.MouseOverForce != null && __instance.MouseOverForce.AreYouTargettingThisForce(f)))
                        {
                            symbol.AddTargettingCircle(gamePositionOfForce, Color.red);
                        }
                    }

                    if (isPlayerTeam)
                    {
                        ForceWaypointSystem waypointSystem = f.GetWaypointSystem();
                        if (f.IsFlagship && waypointSystem == ForceWaypointSystem.FleetMove && battleCheck)
                        {
                            _drawFleetOrdersDelegate?.Invoke(__instance, f.Fleet);
                        }
                        else if (waypointSystem == ForceWaypointSystem.FleetMove && battleCheck)
                        {
                            symbol.AddArrow(gamePositionOfForce, f.GetGameIdealPositionOfForce(), color);
                            symbol.AddVerticalStand(f.GetGameIdealPositionOfForce(), color, 1f);
                        }
                        else if (waypointSystem == ForceWaypointSystem.Patrol)
                        {
                            _drawForcePatrolWaypointsDelegate?.Invoke(__instance, f);
                        }

                        if (__instance.MouseOverForce == f && battleCheck)
                        {
                            _drawFleetToForceConnectionLinesDelegate?.Invoke(__instance, f.Fleet);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                BrilliantSkies.Core.Logger.AdvLogger.LogError($"[StrategicMapIcons] Error in Prefix_Draw_ShowForces: {ex}", BrilliantSkies.Core.Logger.LogOptions.OnlyInDeveloperLog);
                return true;
            }
        }

        private static SymbolRequest AddCustomOrVanillaForceMarker(
            SymbolDrawer symbol,
            Force f,
            StrategicIconMode resolvedMode,
            Vector3 gamePos,
            Quaternion gameRot,
            Color color,
            Vector3 rawSize,
            SymbolDrawerExtraMarkings extra,
            float lineWidth)
        {
            ForceMarkerType vanillaMarkerType = ResolveVanillaForceMarkerType(f);
            SymbolRequest request = symbol.AddForceMarker(gamePos, gameRot, vanillaMarkerType, color, rawSize, extra, lineWidth);

            if (resolvedMode == StrategicIconMode.DefaultEmblem)
            {
                return request;
            }

            LineSegment2D[] segments = VertexIconShapes.GetSegmentsForMap(resolvedMode, f, out bool rotateWithHeading);
            Vector3 mapPos = MapToWorld.GameToGameOnMap(gamePos);
            float horizontalSpanMeters = Mathf.Max(SMI_ModSettings.MinTacticalIconWorldSpanMeters, Mathf.Max(rawSize.x, rawSize.z));
            float mapRadius = horizontalSpanMeters * 0.5f * MapToWorld.ScaleGameToGameOnMap();
            Quaternion mapRot = rotateWithHeading ? Quaternion.Euler(0f, gameRot.eulerAngles.y, 0f) : Quaternion.identity;

            if (request.CreationFunctions != null && request.CreationFunctions.Count > 0)
            {
                request.CreationFunctions[0] = () => BuildMapVertexSegments(mapPos, mapRot, segments, mapRadius);
            }

            return request;
        }

        private static Vector3[] BuildMapVertexSegments(Vector3 mapPos, Quaternion mapRot, LineSegment2D[] segments, float mapRadius)
        {
            Vector3[] verts = new Vector3[segments.Length * 2];
            for (int s = 0; s < segments.Length; s++)
            {
                LineSegment2D seg = segments[s];
                verts[s * 2] = mapPos + mapRot * new Vector3(seg.A.x * mapRadius, 0f, seg.A.y * mapRadius);
                verts[s * 2 + 1] = mapPos + mapRot * new Vector3(seg.B.x * mapRadius, 0f, seg.B.y * mapRadius);
            }
            return verts;
        }

        private static ForceMarkerType ResolveVanillaForceMarkerType(Force f)
        {
            ForceMarkerType forceMarkerType = ForceMarkerType.Ship;
            if (f.CSI != null)
            {
                if (f.CSI.TravelRestrictions == ForceTravelRestrictions.Air)
                {
                    forceMarkerType = ForceMarkerType.Plane;
                }
                else if (f.CSI.TravelRestrictions == ForceTravelRestrictions.AllTerrain)
                {
                    forceMarkerType = ForceMarkerType.AllTerrain;
                }
                else if (f.CSI.TravelRestrictions == ForceTravelRestrictions.Land)
                {
                    forceMarkerType = ForceMarkerType.Land;
                }
            }

            enumConstructableTypes constructType = f.GetConstructableType();
            if (constructType == enumConstructableTypes.structure)
            {
                forceMarkerType = ForceMarkerType.Structure;
            }
            else if (constructType == enumConstructableTypes.fortress)
            {
                forceMarkerType = ForceMarkerType.Fortress;
            }

            if (f.State == enumForceState.Blueprint)
            {
                forceMarkerType = ForceMarkerType.Blueprint;
            }

            if (f.IsFlagship)
            {
                forceMarkerType = forceMarkerType switch
                {
                    ForceMarkerType.Ship => ForceMarkerType.FlagShip,
                    ForceMarkerType.Plane => ForceMarkerType.FlagPlane,
                    ForceMarkerType.Structure => ForceMarkerType.FlagStructure,
                    ForceMarkerType.Fortress => ForceMarkerType.FlagFortress,
                    ForceMarkerType.AllTerrain => ForceMarkerType.FlagAllTerrain,
                    ForceMarkerType.Land => ForceMarkerType.FlagLand,
                    _ => forceMarkerType
                };
            }

            return forceMarkerType;
        }
    }
}
