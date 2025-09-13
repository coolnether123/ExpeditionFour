using HarmonyLib;
using System;
using UnityEngine;

namespace FourPersonExpeditions.DebugPatches
{
    // Patch for ShowRadioDialog
    [HarmonyPatch(typeof(ExplorationManager), "ShowRadioDialog")]
    public static class ShowRadioDialog_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance, ExplorationManager.RadioDialogParams radioParams)
        {
            FPELog.Info($"[RadioDebug] ShowRadioDialog called. Question: {radioParams.questionTextId}, Caller: {radioParams.caller?.firstName}, Receiver: {radioParams.receiver?.firstName}");
            var tr = Traverse.Create(__instance);
            float m_radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] Before setup: m_radioWaitTimer={m_radioWaitTimer}, m_radioTimeoutTimer={m_radioTimeoutTimer}");

            Obj_Radio shelterRadio = Traverse.Create(__instance).Method("GetShelterRadio").GetValue<Obj_Radio>();
            if (shelterRadio != null)
            {
                FPELog.Info($"Shelter Radio exists. IncomingTransmission: {shelterRadio.incomingTransmission}");
            }
        }

        public static void Postfix(ExplorationManager __instance, ExplorationManager.RadioDialogParams radioParams, bool __result)
        {
            var tr = Traverse.Create(__instance);
            float m_radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            Obj_Radio shelterRadio = Traverse.Create(__instance).Method("GetShelterRadio").GetValue<Obj_Radio>();
            bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

            FPELog.Info($"[RadioDebug] After setup: m_radioWaitTimer={m_radioWaitTimer}, m_radioTimeoutTimer={m_radioTimeoutTimer}, ShelterRadio incomingTransmission: {incomingTransmission}. Result: {__result}");
        }
    }

    // Patch for OnTransmissionAcceptedButton
    [HarmonyPatch(typeof(ExplorationManager), "OnTransmissionAcceptedButton")]
    public static class OnTransmissionAcceptedButton_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance)
        {
            var tr = Traverse.Create(__instance);
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] OnTransmissionAcceptedButton called. m_radioTimeoutTimer before: {m_radioTimeoutTimer}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            var tr = Traverse.Create(__instance);
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] OnTransmissionAcceptedButton finished. m_radioTimeoutTimer after: {m_radioTimeoutTimer}");
        }
    }

    // Patch for OnTransmissionRejectedButton
    [HarmonyPatch(typeof(ExplorationManager), "OnTransmissionRejectedButton")]
    public static class OnTransmissionRejectedButton_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance)
        {
            var tr = Traverse.Create(__instance);
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            float m_radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] OnTransmissionRejectedButton called. m_radioTimeoutTimer before: {m_radioTimeoutTimer}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            var tr = Traverse.Create(__instance);
            float m_radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            float m_radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] OnTransmissionRejectedButton finished. m_radioTimeoutTimer after: {m_radioTimeoutTimer}, m_radioWaitTimer after: {m_radioWaitTimer}");
        }
    }

    // Patch for RadioDialogClosed
    [HarmonyPatch(typeof(ExplorationManager), "RadioDialogClosed")]
    public static class RadioDialogClosed_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance, RadioDialogPanel.RadioResponse response)
        {
            var tr = Traverse.Create(__instance);
            ExplorationManager.RadioDialogCallback m_radioCallback = tr.Field("m_radioCallback").GetValue<ExplorationManager.RadioDialogCallback>();
            FPELog.Info($"[RadioDebug] RadioDialogClosed called with response: {response}. m_radioCallback before: {m_radioCallback != null}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            var tr = Traverse.Create(__instance);
            ExplorationManager.RadioDialogCallback m_radioCallback = tr.Field("m_radioCallback").GetValue<ExplorationManager.RadioDialogCallback>();
            float m_radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            FPELog.Info($"[RadioDebug] RadioDialogClosed finished. m_radioCallback after: {m_radioCallback != null}, m_radioWaitTimer after: {m_radioWaitTimer}");
        }
    }
}