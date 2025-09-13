using HarmonyLib;
using System;
using UnityEngine;

namespace FourPersonExpeditions.DebugPatches
{
    // Patch for incomingTransmission property setter
    [HarmonyPatch(typeof(Obj_Radio), "set_incomingTransmission")]
    public static class ObjRadio_IncomingTransmission_DebugPatch
    {
        public static void Prefix(bool value)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.incomingTransmission SET: value={value}");
        }
    }

    // Patch for AcceptTransmission
    [HarmonyPatch(typeof(Obj_Radio), "AcceptTransmission")]
    public static class ObjRadio_AcceptTransmission_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.AcceptTransmission called. incomingTransmission before: {__instance.incomingTransmission}");
        }

        public static void Postfix(Obj_Radio __instance)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.AcceptTransmission finished. incomingTransmission after: {__instance.incomingTransmission}");
        }
    }

    // Patch for RejectTransmission
    [HarmonyPatch(typeof(Obj_Radio), "RejectTransmission")]
    public static class ObjRadio_RejectTransmission_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.RejectTransmission called. incomingTransmission before: {__instance.incomingTransmission}");
        }

        public static void Postfix(Obj_Radio __instance)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.RejectTransmission finished. incomingTransmission after: {__instance.incomingTransmission}");
        }
    }

    // Patch for SetIncomingTransmissionIndicator
    [HarmonyPatch(typeof(Obj_Radio), "SetIncomingTransmissionIndicator")]
    public static class ObjRadio_SetIncomingTransmissionIndicator_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance, bool isActive)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.SetIncomingTransmissionIndicator called with isActive={isActive}. Current incomingTransmissionIcon.activeSelf={__instance.incomingTransmissionIcon.activeSelf}");
        }

        public static void Postfix(Obj_Radio __instance, bool isActive)
        {
            FPELog.Info($"[RadioDebug] Obj_Radio.SetIncomingTransmissionIndicator finished. incomingTransmissionIcon.activeSelf after SetActive: {__instance.incomingTransmissionIcon.activeSelf}");
        }
    }
}
