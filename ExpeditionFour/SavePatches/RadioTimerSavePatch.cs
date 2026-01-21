using HarmonyLib;
using System;
using System.Collections.Generic;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Re-initializes radio timers and incoming transmission states when a party is loaded in a "waiting for user" state.
    /// This ensures that radio dialogs remain responsive after loading a save.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.SaveLoad))]
    internal static class RadioTimerSavePatch
    {
        // States where the party is waiting for a user response via radio
        private static readonly HashSet<ExplorationParty.ePartyState> RadioWaitStates =
            new HashSet<ExplorationParty.ePartyState>
            {
                ExplorationParty.ePartyState.ReportingLocationWaitUser,
                ExplorationParty.ePartyState.ReportingDiversionsWaitUser,
                ExplorationParty.ePartyState.EncounteredItemsWaitUser,
                ExplorationParty.ePartyState.EncounteredNPCsWaitUser,
                ExplorationParty.ePartyState.EncounteredQuestNPCsWaitUser,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitUser
            };

        static void Postfix(ExplorationParty __instance, SaveData data)
        {
            try
            {
                if (__instance == null || data == null || !data.isLoading) return;

                var state = __instance.state;
                FPELog.Info($"Radio Persistence: Processing Party#{__instance.id} in state {state}.");

                if (RadioWaitStates.Contains(state))
                {
                    var em = ExplorationManager.Instance;
                    if (em == null)
                    {
                        FPELog.Warn("Radio Persistence: ExplorationManager instance not found. Skipping radio re-initialization.");
                        return;
                    }

                    // Backup current timers for logging
                    float timeoutBefore = Safe.GetFieldOrDefault(em, "m_radioTimeoutTimer", 0f);
                    float waitBefore = Safe.GetFieldOrDefault(em, "m_radioWaitTimer", 0f);

                    // Reset radio timers to ensure immediate availability and fresh timeout
                    Safe.SetField(em, "m_radioTimeoutTimer", em.radioTransmissionTimeout);
                    Safe.SetField(em, "m_radioWaitTimer", 0f);

                    // Reactivate the incoming transmission indicator on the shelter radio object
                    Safe.TryCall(em, "GetShelterRadio", out Obj_Radio shelterRadio);
                    if (shelterRadio != null)
                    {
                        shelterRadio.incomingTransmission = true;
                        FPELog.Info($"Radio Persistence: Timers reset for Party#{__instance.id}. Incoming transmission active.");
                    }
                    else
                    {
                        FPELog.Warn($"Radio Persistence: Party#{__instance.id} timers reset, but target Obj_Radio was not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                FPELog.Warn($"Radio Persistence Error: {ex.Message}");
            }
        }
    }
}
