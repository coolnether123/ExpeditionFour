using HarmonyLib;
using FourPersonExpeditions;

namespace FourPersonExpeditions.DebugPatches
{
    /// <summary>
    /// Debug patches for Obj_Radio component.
    /// Monitors signal state and UI indicator logic to troubleshoot radio responsiveness.
    /// </summary>
    [HarmonyPatch(typeof(Obj_Radio), "set_incomingTransmission")]
    public static class ObjRadio_IncomingTransmission_DebugPatch
    {
        public static void Prefix(bool value)
        {
            FPELog.Info($"Radio Debug: Obj_Radio.incomingTransmission set to {value}.");
        }
    }

    [HarmonyPatch(typeof(Obj_Radio), "AcceptTransmission")]
    public static class ObjRadio_AcceptTransmission_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance)
        {
            FPELog.Info($"Radio Debug: Obj_Radio.AcceptTransmission called. StateBefore={__instance.incomingTransmission}");
        }

        public static void Postfix(Obj_Radio __instance)
        {
            FPELog.Info($"Radio Debug: Obj_Radio.AcceptTransmission finished. StateAfter={__instance.incomingTransmission}");
        }
    }

    [HarmonyPatch(typeof(Obj_Radio), "RejectTransmission")]
    public static class ObjRadio_RejectTransmission_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance)
        {
            FPELog.Info($"Radio Debug: Obj_Radio.RejectTransmission called. StateBefore={__instance.incomingTransmission}");
        }

        public static void Postfix(Obj_Radio __instance)
        {
            FPELog.Info($"Radio Debug: Obj_Radio.RejectTransmission finished. StateAfter={__instance.incomingTransmission}");
        }
    }

    [HarmonyPatch(typeof(Obj_Radio), "SetIncomingTransmissionIndicator")]
    public static class ObjRadio_SetIncomingTransmissionIndicator_DebugPatch
    {
        public static void Prefix(Obj_Radio __instance, bool isActive)
        {
            bool iconActive = __instance.incomingTransmissionIcon != null && __instance.incomingTransmissionIcon.activeSelf;
            FPELog.Info($"Radio Debug: SetIncomingTransmissionIndicator({isActive}). IconActiveBefore={iconActive}");
        }

        public static void Postfix(Obj_Radio __instance, bool isActive)
        {
            bool iconActive = __instance.incomingTransmissionIcon != null && __instance.incomingTransmissionIcon.activeSelf;
            FPELog.Info($"Radio Debug: SetIncomingTransmissionIndicator finished. IconActiveAfter={iconActive}");
        }
    }
}
