using System;
using System.Reflection;
using BrilliantSkies.Core.Logger;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Modding;
using HarmonyLib;

namespace StrategicMapIcons
{
    public class Init : GamePlugin, GamePlugin_PostLoad
    {
        public string name => "StrategicMapIcons";

        public Version version => new(1, 0, 1);

        public void OnLoad()
        {
            GameEvents.StartEvent.RegWithEvent(OnStart);
            GameEvents.ProfileChange.RegWithEvent(OnProfileChanged);

            try
            {
                new Harmony("com.strategicmapicons").PatchAll(Assembly.GetExecutingAssembly());
                AdvLogger.LogEvent("[StrategicMapIcons] Harmony patches registered.");
            }
            catch (Exception ex)
            {
                AdvLogger.LogError($"[StrategicMapIcons] Error in OnLoad Harmony: {ex}", LogOptions._AlertDevAndCustomerInGame);
            }

            try
            {
                IconRoleManager.Init();
            }
            catch (Exception ex)
            {
                AdvLogger.LogError($"[StrategicMapIcons] Error in IconRoleManager.Init: {ex}", LogOptions._AlertDevAndCustomerInGame);
            }
        }

        private void OnProfileChanged()
        {
            try
            {
                SMI_ModSettingsConfiguration.Get()?.ApplyToSettings();
            }
            catch { }
        }

        public void OnStart()
        {
            GameEvents.StartEvent.UnregWithEvent(OnStart);
            try
            {
                SMI_ModSettingsConfiguration.Get()?.ApplyToSettings();
            }
            catch (Exception ex)
            {
                AdvLogger.LogError($"[StrategicMapIcons] Error loading settings config: {ex}", LogOptions.None);
            }

            try
            {
                ModInformation.VersionConfirmation();
            }
            catch (Exception ex)
            {
                AdvLogger.LogError($"[StrategicMapIcons] Error in VersionConfirmation: {ex}", LogOptions._AlertDevAndCustomerInGame);
            }
        }

        public void OnSave() { }

        public bool AfterAllPluginsLoaded()
        {
            try
            {
                SMI_ModSettingsConfiguration.Get()?.ApplyToSettings();
            }
            catch { }
            return true;
        }
    }
}
