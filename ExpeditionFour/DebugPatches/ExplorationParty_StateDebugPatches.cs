using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpeditionFour.DebugPatches
{
    [HarmonyPatch(typeof(ExplorationParty), "PushState")]
    internal static class ExplorationParty_PushState_Patch
    {
        static void Prefix(ExplorationParty __instance, ExplorationParty.ePartyState stateType)
        {
            try
            {
                FPELog.Warn($"[FPE/TRACE] ExplorationParty #{__instance.id} - PUSH State: {stateType}");
            }
            catch { /* tracing only */ }
        }
    }

    [HarmonyPatch(typeof(ExplorationParty), "PopState")]
    internal static class ExplorationParty_PopState_Patch
    {
        static void Postfix(ExplorationParty __instance, object __result)
        {
            try
            {
                // Use Traverse to access the 'state' field of the inaccessible StateDef object
                var stateDefState = Traverse.Create(__result).Field("state").GetValue<ExplorationParty.ePartyState>();
                FPELog.Warn($"[FPE/TRACE] ExplorationParty #{__instance.id} - POP State: {stateDefState}");
            }
            catch (System.Exception e)
            {
                try { FPELog.Warn($"[FPE/TRACE] ExplorationParty #{__instance?.id} - POP State: <unknown> ({e.GetType().Name})"); } catch { }
            }
        }
    }
}
