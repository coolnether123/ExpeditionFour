using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.DebugPatches
{
    [HarmonyPatch(typeof(ExplorationParty), "Awake")]
    internal static class ExplorationParty_Awake_DebugPatch
    {
        static void Postfix(ExplorationParty __instance)
        {
            FPELog.Warn($"[FPE/TRACE] ExplorationParty #{__instance.id} - Awake() called. State: {__instance.state}");
        }
    }
}
