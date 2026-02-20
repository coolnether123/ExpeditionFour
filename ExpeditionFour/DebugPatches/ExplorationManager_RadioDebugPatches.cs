using HarmonyLib;
using System;
using UnityEngine;
using ModAPI.Reflection;

namespace FourPersonExpeditions.DebugPatches
{
    /// <summary>
    /// Debug patches for ExplorationManager radio operations.
    /// Monitors dialog display, button clicks, and state transitions to ensure reliable communication.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), "ShowRadioDialog")]
    public static class ShowRadioDialog_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance, ExplorationManager.RadioDialogParams radioParams)
        {
            FPELog.Debug($"Radio Debug: ShowRadioDialog requested. Msg={radioParams.questionTextId}, Caller={radioParams.caller?.firstName}");
            
            float waitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            float timeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            FPELog.Debug($"Radio Debug: Current Timers [Wait={waitTimer}, Timeout={timeoutTimer}]");

            Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
            if (shelterRadio != null)
            {
                FPELog.Debug($"Radio Debug: Shelter Radio found. IncomingTransmission={shelterRadio.incomingTransmission}");
            }
        }

        public static void Postfix(ExplorationManager __instance, ExplorationManager.RadioDialogParams radioParams, bool __result)
        {
            float waitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            float timeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            
            Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
            bool incoming = shelterRadio != null && shelterRadio.incomingTransmission;

            FPELog.Debug($"Radio Debug: Dialog Setup Finished. Result={__result}, Timers=[{waitTimer}/{timeoutTimer}], Incoming={incoming}");
        }
    }

    [HarmonyPatch(typeof(ExplorationManager), "OnTransmissionAcceptedButton")]
    public static class OnTransmissionAcceptedButton_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance)
        {
            float timeout = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            FPELog.Debug($"Radio Debug: OnTransmissionAcceptedButton. TimeoutBefore={timeout}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            float timeout = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            FPELog.Debug($"Radio Debug: OnTransmissionAcceptedButton finished. TimeoutAfter={timeout}");
        }
    }

    [HarmonyPatch(typeof(ExplorationManager), "OnTransmissionRejectedButton")]
    public static class OnTransmissionRejectedButton_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance)
        {
            float timeout = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            FPELog.Debug($"Radio Debug: OnTransmissionRejectedButton. TimeoutBefore={timeout}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            float timeout = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            float wait = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            FPELog.Debug($"Radio Debug: OnTransmissionRejectedButton finished. Timeout={timeout}, Wait={wait}");
        }
    }

    [HarmonyPatch(typeof(ExplorationManager), "RadioDialogClosed")]
    public static class RadioDialogClosed_DebugPatch
    {
        public static void Prefix(ExplorationManager __instance, RadioDialogPanel.RadioResponse response)
        {
            Safe.TryGetField(__instance, "m_radioCallback", out ExplorationManager.RadioDialogCallback callback);
            FPELog.Debug($"Radio Debug: RadioDialogClosed called. Response={response}, HasCallback={callback != null}");
        }

        public static void Postfix(ExplorationManager __instance)
        {
            Safe.TryGetField(__instance, "m_radioCallback", out ExplorationManager.RadioDialogCallback callback);
            float wait = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            FPELog.Debug($"Radio Debug: RadioDialogClosed finished. HasCallback={callback != null}, WaitAfter={wait}");
        }
    }
}
