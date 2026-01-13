using HarmonyLib;
using System;
using UnityEngine;
using ModAPI.Reflection;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Patches ExplorationManager.SaveLoad to ensure radio-related timers and transmission states are correctly persisted.
    /// This resolves issues where the radio state might become desynchronized after loading a save with an active expedition.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), "SaveLoad")]
    public static class ExplorationManager_RadioSavePatch
    {
        public static void Postfix(ExplorationManager __instance, SaveData data)
        {
            FPELog.Info("ExplorationManager_RadioSave: Processing persistence for radio state.");

            // Retrieve current timer values from private fields
            float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            
            // Execute Save/Load operations for the timers
            data.SaveLoad("radioWaitTimer", ref radioWaitTimer);
            data.SaveLoad("radioTimeoutTimer", ref radioTimeoutTimer);

            // Update internal state if loading
            if (data.isLoading)
            {
                Safe.SetField(__instance, "m_radioWaitTimer", radioWaitTimer);
                Safe.SetField(__instance, "m_radioTimeoutTimer", radioTimeoutTimer);
            }

            // Sync the incoming transmission flag from the shelter radio object
            bool incomingTransmission = false;
            Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);

            if (data.isSaving && (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null)
            {
                incomingTransmission = shelterRadio.incomingTransmission;
            }
            
            data.SaveLoad("incomingTransmission", ref incomingTransmission);

            if (data.isLoading && (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null)
            {
                shelterRadio.incomingTransmission = incomingTransmission;
            }

            FPELog.Info($"ExplorationManager_RadioSave: Timers [{radioWaitTimer}/{radioTimeoutTimer}], Incoming [{incomingTransmission}] synchronized.");
        }
    }
}
