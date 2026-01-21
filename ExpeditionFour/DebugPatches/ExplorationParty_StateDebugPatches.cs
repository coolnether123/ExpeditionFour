using HarmonyLib;
using System;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.DebugPatches
{
    /// <summary>
    /// Debug patches for monitoring the internal state stack of ExplorationParty objects.
    /// Tracks Push and Pop operations to trace party behavior during expeditions.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), "PushState")]
    internal static class ExplorationParty_PushState_Patch
    {
        static void Prefix(ExplorationParty __instance, ExplorationParty.ePartyState stateType)
        {
            try
            {
                FPELog.Info($"Party Trace: Party #{__instance.id} - Pushing state {stateType}.");
            }
            catch { /* Debug tracing should not affect critical execution */ }
        }
    }

    [HarmonyPatch(typeof(ExplorationParty), "PopState")]
    internal static class ExplorationParty_PopState_Patch
    {
        static void Postfix(ExplorationParty __instance, object __result)
        {
            try
            {
                // Retrieve the state identifier from the returned StateDef object
                if (Safe.TryGetField(__result, "state", out ExplorationParty.ePartyState stateDefState))
                {
                    FPELog.Info($"Party Trace: Party #{__instance.id} - Popped state {stateDefState}.");
                }
                else
                {
                    FPELog.Info($"Party Trace: Party #{__instance.id} - Popped state (could not extract identifier).");
                }
            }
            catch (Exception ex)
            {
                FPELog.Warn($"Party Trace Error: Party #{__instance?.id} PopState error: {ex.Message}");
            }
        }
    }
}
