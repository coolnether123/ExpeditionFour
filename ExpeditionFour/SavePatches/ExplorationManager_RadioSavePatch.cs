using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FourPersonExpeditions.SavePatches
{
    [HarmonyPatch(typeof(ExplorationManager), "SaveLoad")]
    public static class ExplorationManager_RadioSavePatch
    {
        public static void Postfix(ExplorationManager __instance, SaveData data)
        {
            FPELog.Info("[FPE/SavePatch] ExplorationManager_RadioSavePatch Postfix running.");

            // Access private fields using Traverse
            var tr = Traverse.Create(__instance);
            float radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            float radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            
            // Save/Load radio timers
            data.SaveLoad("radioWaitTimer", ref radioWaitTimer);
            data.SaveLoad("radioTimeoutTimer", ref radioTimeoutTimer);

            // Update instance fields after loading
            if (data.isLoading)
            {
                tr.Field("m_radioWaitTimer").SetValue(radioWaitTimer);
                tr.Field("m_radioTimeoutTimer").SetValue(radioTimeoutTimer);
            }

            // Handle incomingTransmission flag from Obj_Radio
            bool incomingTransmission = false;
            Obj_Radio shelterRadio = __instance.GetShelterRadio(); // GetShelterRadio is public

            if (data.isSaving && (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null)
            {
                incomingTransmission = shelterRadio.incomingTransmission;
            }
            data.SaveLoad("incomingTransmission", ref incomingTransmission);

            if (data.isLoading && (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null)
            {
                shelterRadio.incomingTransmission = incomingTransmission;
            }

            FPELog.Info($"[FPE/SavePatch] Radio state saved/loaded: Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
        }
    }
}
