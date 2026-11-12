using System;
using System.Collections.Generic;
using BrilliantSkies.Core;
using BrilliantSkies.Core.Id;
using BrilliantSkies.Ftd.Fleets.Forces.Management.Extrapolation;
using BrilliantSkies.Ftd.Rz.Transfer.SupplyGroups;
using HarmonyLib;
using NetInfrastructure;

namespace StrategicMapIcons
{
    public enum StrategicIconMode
    {
        DefaultEmblem = 0, // Uses the player/faction custom emblem icon (vanilla)
        Auto = 1,          // Automatically chooses vertex shape based on unit stats & supply role
        Air_Combat = 2,        // Combat air
        Water_Combat = 3,     // Combat ship
        Land_Combat = 4,      // Combat armor / tank / vehicle
        Orbital_Combat = 5,   // Combat orbital / satellite / space station
        Harvester = 6,     // Resource gatherer / creator vertex icon
        Cargo = 7,         // Logistics / cargo transport vertex icon
        Scout = 8,         // Radar / satellite / recon vertex icon
        Support = 9,        // Repair / tender / spawner vertex icon
        Fortress = 10,       // Bastion fortress vertex icon (for large stationary bases)
        Shapes_Square = 101,       // standard shape
        Shapes_Circle = 102,       // standard shape
        Shapes_Triangle = 103,    // standard shape
        Shapes_Pentagon = 104,    // standard shape
        Shapes_Hexagon = 105,     // standard shape
        Shapes_Octagon = 106,     // standard shape
        Shapes_Star = 107,        // standard shape
        Shapes_Cross = 108,       // standard shape
        Shapes_Diamond = 109,     // standard shape
        Shapes_Chevron = 110,     // standard shape
        Missile = 200,  // custom shape
    }

    public static class IconRoleManager
    {
        // Stored in ConstructableSpecialInfo.CSI[21] (obsolete QUAL_DamageAndAccuracyUnderFire slot)
        // Value = (int)StrategicIconMode + 100 so 0/-1 (uninitialized) defaults cleanly to Auto.
        private const int CsiSlot = 21;
        private const int CsiOffset = 100;

        private static INetworkIdentity? _identity;
        private static bool _isRegistered;
        private static readonly IconRoleSyncReceiver _receiver = new IconRoleSyncReceiver();

        public static void Init()
        {
            NetSys.Status.OnStatusChange.RegWithEvent(OnNetworkStatusChanged);
        }

        private static void OnNetworkStatusChanged(NetStatus status)
        {
            if (status == NetStatus.Server || status == NetStatus.Client)
            {
                GetIdentity();
            }
            else if (status == NetStatus.Disconnected)
            {
                _identity = null;
                _isRegistered = false;
            }
        }

        /// <summary>
        /// Gets the network identity for synchronizing force icon modes across clients and server.
        /// </summary>
        public static INetworkIdentity? GetIdentity()
        {
            if (_identity == null && NetSys.System?.NetworkIdentities != null)
            {
                _identity = NetSys.System.NetworkIdentities.MakeOrGetUniqueIdentity(73u);
                if (_identity != null && !_isRegistered)
                {
                    _identity.MonitorThis(_receiver);
                    _isRegistered = true;
                }
            }
            return _identity;
        }

        /// <summary>
        /// get force icon mode
        /// </summary>
        public static StrategicIconMode GetForceIconMode(Force? f)
        {
            if (f?.CSI?.CSI == null || f.CSI.CSI.Length <= CsiSlot)
            {
                return StrategicIconMode.Auto;
            }
            int raw = (int)Math.Round(f.CSI.CSI[CsiSlot]);
            int modeVal = raw - CsiOffset;
            if (Enum.IsDefined(typeof(StrategicIconMode), modeVal))
            {
                return (StrategicIconMode)modeVal;
            }
            return StrategicIconMode.Auto;
        }

        /// <summary>
        /// sets icon mode
        /// </summary
        public static void SetForceIconMode(Force? f, StrategicIconMode mode)
        {
            if (f?.CSI?.CSI == null || f.CSI.CSI.Length <= CsiSlot)
            {
                return;
            }
            int encoded = (int)mode + CsiOffset;
            if ((int)Math.Round(f.CSI.CSI[CsiSlot]) == encoded)
            {
                return;
            }
            f.CSI.CSI[CsiSlot] = encoded;

            if (Net.IsClient)
            {
                GetIdentity()?.RPCMimic("Server_SetForceIconMode", RPCMimicMode.Server, f.Id.Id, (int)mode);
            }
            else if (Net.IsServer)
            {
                GetIdentity()?.RPCMimic("Client_SetForceIconMode", RPCMimicMode.Others, f.Id.Id, (int)mode);
            }
        }

        public static void ApplyForceIconModeLocal(int forceId, int mode)
        {
            Force? f = ForceManager.Instance?.GetForceByID(new ObjectId(forceId));
            if (f?.CSI?.CSI != null && f.CSI.CSI.Length > CsiSlot)
            {
                f.CSI.CSI[CsiSlot] = mode + CsiOffset;
            }
        }

        /// <summary>
        /// Resolves the actual display role for a single Force (resolving Auto into a concrete role).
        /// </summary>
        public static StrategicIconMode ResolveForceRole(Force f)
        {
            StrategicIconMode assigned = GetForceIconMode(f);
            if (assigned != StrategicIconMode.Auto)
            {
                return assigned;
            }
            return ClassifyForceAuto(f);
        }

        /// <summary>
        /// Classifies a force according to its capabilities and automatically selects a strategic icon mode.
        /// Forces without combat system information are classified using combat-mode rules.
        /// Resource gathering, cargo, support, scouting, and combat capabilities are evaluated in priority
        /// order.</summary>
        /// <param name="f">The force to classify.</param>
        /// <returns>The strategic icon mode assigned to the force.</returns>
        public static StrategicIconMode ClassifyForceAuto(Force f)
        {
            var csi = f.CSI;
            if (csi == null)
            {
                return ClassifyCombatMode(f);
            }

            // easter egg icon check
            if (SMI_ModSettings.EasterEggsEnabled)
            {
                // missile / nuke icon
                if (f.Name.ToLower().Contains("missile") || f.Name.ToLower().Contains("nuke"))
                {
                    return StrategicIconMode.Missile;
                }
            }

            // 1. Harvester / Resource Creator
            if (csi.ResourceGatherers > 0f || csi.SupplyType == SupplyType.Creator)
            {
                return StrategicIconMode.Harvester;
            }

            // 2. Cargo / Logistics
            if (csi.SupplyType == SupplyType.Cargo)
            {
                return StrategicIconMode.Cargo;
            }

            // 3. Support / Repair Tender / Spawner (when low firepower or dedicated repairer)
            if ((csi.RepairOther > 0f || csi.BlueprintSpawner > 0f) && csi.TotalFirepower < SMI_ModSettings.SupportMaxFirepower)
            {
                return StrategicIconMode.Support;
            }

            // 4. Scout / Radar / Satellite (antenna or high altitude with minimal weapons)
            if (csi.TotalFirepower < SMI_ModSettings.ScoutMaxFirepower &&
                (csi.Antennas > 0f ||
                 csi.AntennaHeight > SMI_ModSettings.ScoutMinAntennaHeight ||
                 csi.MaxAltitude > SMI_ModSettings.ScoutMinAltitude ||
                 csi.TopSpeed > SMI_ModSettings.ScoutMinTopSpeed))
            {
                return StrategicIconMode.Scout;
            }

            // 5. Support if unarmed with repair/spawner
            if (csi.TotalFirepower <= SMI_ModSettings.UnarmedMaxFirepower && (csi.RepairOther > 0f || csi.BlueprintSpawner > 0f))
            {
                return StrategicIconMode.Support;
            }

            return ClassifyCombatMode(f);
        }

        /// <summary>
        /// Classifies the combat mode for a force.
        /// </summary>
        /// <param name="f">The force to classify, or `null`.</param>
        /// <returns>The strategic icon mode corresponding to the force's combat domain.</returns>
        public static StrategicIconMode ClassifyCombatMode(Force? f)
        {
            return VertexIconShapes.GetCombatDomain(f) switch
            {
                CombatDomain.Fortress => StrategicIconMode.Fortress,
                CombatDomain.Orbital => StrategicIconMode.Orbital_Combat,
                CombatDomain.Air => StrategicIconMode.Air_Combat,
                CombatDomain.Land => StrategicIconMode.Land_Combat,
                _ => StrategicIconMode.Water_Combat
            };
        }

        /// <summary>
        /// Resolves the effective icon mode for an entire Fleet on the strategic map.
        /// Priority:
        /// 1. If flagship has an explicit (non-Auto) mode set, use flagship's mode.
        /// 2. If any force in the fleet has an explicit (non-Auto) mode set, use that.
        /// 3. Otherwise, auto-classify the fleet as a whole (e.g., if fleet has significant combat power, it's Combat; else flagship's auto role).
        /// </summary>
        public static StrategicIconMode ResolveFleetRole(Fleet fleet, out Force? representativeForce)
        {
            representativeForce = fleet.flagShip ?? (fleet.forces.Count > 0 ? fleet.forces[0] : null);
            if (representativeForce == null)
            {
                return StrategicIconMode.Water_Combat;
            }

            // Check flagship explicit override first
            StrategicIconMode flagshipAssigned = GetForceIconMode(representativeForce);
            if (flagshipAssigned != StrategicIconMode.Auto)
            {
                return flagshipAssigned;
            }

            // Check if any other force has an explicit override, preferring the highest firepower override
            Force? bestExplicitForce = null;
            float bestExplicitFirepower = -1f;
            for (int i = 0; i < fleet.forces.Count; i++)
            {
                Force f = fleet.forces[i];
                StrategicIconMode mode = GetForceIconMode(f);
                if (mode != StrategicIconMode.Auto)
                {
                    float fp = f.CSI?.TotalFirepower ?? 0f;
                    if (bestExplicitForce == null || fp > bestExplicitFirepower)
                    {
                        bestExplicitForce = f;
                        bestExplicitFirepower = fp;
                    }
                }
            }

            // If an explicit override exists on an escort, only use it if the fleet isn't a major combat fleet
            // led by a different flagship with significantly higher firepower.
            float flagshipFirepower = representativeForce.CSI?.TotalFirepower ?? 0f;
            if (bestExplicitForce != null && (flagshipFirepower < SMI_ModSettings.CombatFleetMinFirepower || bestExplicitFirepower >= flagshipFirepower * 0.5f))
            {
                representativeForce = bestExplicitForce;
                return GetForceIconMode(bestExplicitForce);
            }

            // Auto-classify across the fleet:
            // If any force has Harvester/Creator, and fleet isn't a major combat fleet, it's Harvester
            float totalFirepower = 0f;
            bool hasHarvester = false;
            bool hasCargo = false;
            bool hasSupport = false;
            bool hasScout = false;
            Force? strongestCombatForce = null;
            float maxForceFirepower = -1f;

            // Evaluate each force in the fleet
            for (int i = 0; i < fleet.forces.Count; i++)
            {
                Force f = fleet.forces[i];
                if (f?.CSI == null) continue;
                float fp = f.CSI.TotalFirepower;
                totalFirepower += fp;
                if (fp > maxForceFirepower)
                {
                    maxForceFirepower = fp;
                    strongestCombatForce = f;
                }

                StrategicIconMode role = ClassifyForceAuto(f);
                switch (role)
                {
                    case StrategicIconMode.Harvester: hasHarvester = true; break;
                    case StrategicIconMode.Cargo: hasCargo = true; break;
                    case StrategicIconMode.Support: hasSupport = true; break;
                    case StrategicIconMode.Scout: hasScout = true; break;
                }
            }

            if (totalFirepower >= SMI_ModSettings.CombatFleetMinFirepower)
            {
                if (strongestCombatForce != null)
                {
                    representativeForce = strongestCombatForce;
                }
                return ClassifyCombatMode(representativeForce);
            }
            if (hasHarvester) return StrategicIconMode.Harvester;
            if (hasCargo) return StrategicIconMode.Cargo;
            if (hasSupport) return StrategicIconMode.Support;
            if (hasScout) return StrategicIconMode.Scout;

            return ClassifyForceAuto(representativeForce);
        }
    }

    /// <summary>
    /// Receives networked requests to synchronize force icon modes.
    /// </summary>
    public class IconRoleSyncReceiver
    {
        [NetworkMethod]
        public void Server_SetForceIconMode(int forceId, int mode)
        {
            if (!Net.IsServer) return;
            IconRoleManager.ApplyForceIconModeLocal(forceId, mode);
            IconRoleManager.GetIdentity()?.RPCMimic("Client_SetForceIconMode", RPCMimicMode.Others, forceId, mode);
        }

        [NetworkMethod]
        public void Client_SetForceIconMode(int forceId, int mode)
        {
            IconRoleManager.ApplyForceIconModeLocal(forceId, mode);
        }
    }

    /// <summary>
    /// Provides Harmony patches that preserve force-related CSI data when copying, retrofitting, and duplicatingconstructs.
    /// </summary>
    [HarmonyPatch(typeof(CsiAmmender))]
    public static class CsiAmmenderPatches
    {
        [HarmonyPatch(nameof(CsiAmmender.CopyOverNonStateStuff))]
        [HarmonyPrefix]
        public static void Prefix_CopyOverNonStateStuff(ConstructableSpecialInfo toTake, out double __state)
        {
            __state = (toTake?.CSI != null && toTake.CSI.Length > 21) ? toTake.CSI[21] : -1.0;
        }

        [HarmonyPatch(nameof(CsiAmmender.CopyOverNonStateStuff))]
        [HarmonyPostfix]
        public static void Postfix_CopyOverNonStateStuff(ConstructableSpecialInfo toTake, double __state)
        {
            if (__state >= 90.0 && toTake?.CSI != null && toTake.CSI.Length > 21)
            {
                toTake.CSI[21] = __state;
            }
        }

        [HarmonyPatch(nameof(CsiAmmender.CopyOverOnlyTheStateStuffWhenRetrofittingForce))]
        [HarmonyPostfix]
        public static void Postfix_RetrofitState(ConstructableSpecialInfo toTake, ConstructableSpecialInfo toGive)
        {
            if (toTake?.CSI != null && toGive?.CSI != null && toTake.CSI.Length > 21 && toGive.CSI.Length > 21)
            {
                toTake.CSI[21] = toGive.CSI[21];
            }
        }

        [HarmonyPatch(nameof(CsiAmmender.DuplicateForce))]
        [HarmonyPostfix]
        public static void Postfix_DuplicateForce(ConstructableSpecialInfo toDupe, ConstructableSpecialInfo dupe)
        {
            if (toDupe?.CSI != null && dupe?.CSI != null && toDupe.CSI.Length > 21 && dupe.CSI.Length > 21)
            {
                dupe.CSI[21] = toDupe.CSI[21];
            }
        }
    }
}

