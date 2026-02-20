using HarmonyLib;
using FourPersonExpeditions;

namespace FourPersonExpeditions.DebugPatches
{
    /// <summary>
    /// Debug patch to monitor the initialization of ExplorationParty objects.
    /// Helpful for tracking party creation during new expeditions or save loading.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), "Awake")]
    internal static class ExplorationParty_Awake_DebugPatch
    {
        static void Postfix(ExplorationParty __instance)
        {
            FPELog.Debug($"Party Trace: Party #{__instance.id} - Awake called. Initial State: {__instance.state}");
        }
    }
}
