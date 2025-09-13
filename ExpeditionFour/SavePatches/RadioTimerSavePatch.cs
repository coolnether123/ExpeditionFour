using HarmonyLib;
using System;
using System.Collections.Generic; // Added for HashSet

namespace ExpeditionFour.SavePatches
{
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.SaveLoad))]
    internal static class RadioTimerSavePatch
    {
        // States where a radio dialog is active and needs to be re-initialized on load.
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
                FPELog.Warn($"[FPE/TRACE] RadioTimerSavePatch: Party#{__instance.id} SaveLoad(state={state})");

                // --- Radio Timer Re-initialization ---
                if (RadioWaitStates.Contains(state))
                {
                    var em = ExplorationManager.Instance;
                    if (em == null)
                    {
                        FPELog.Warn("[FPE/TRACE] RadioTimerSavePatch: ExplorationManager.Instance is null on load; skipping radio re-init.");
                        return;
                    }

                    var tr = Traverse.Create(em);
                    float timeoutBefore = 0f, waitBefore = 0f;
                    try
                    {
                        timeoutBefore = tr.Field("m_radioTimeoutTimer").GetValue<float>();
                        waitBefore = tr.Field("m_radioWaitTimer").GetValue<float>();
                    }
                    catch { /* best-effort read */ }

                    tr.Field("m_radioTimeoutTimer").SetValue(em.radioTransmissionTimeout);
                    tr.Field("m_radioWaitTimer").SetValue(0f);

                    // Re-activate incoming transmission icon on the radio object
                    Obj_Radio shelterRadio = null;
                    try { shelterRadio = tr.Method("GetShelterRadio").GetValue<Obj_Radio>(); } catch { }
                    if (shelterRadio != null)
                    {
                        shelterRadio.incomingTransmission = true;
                        FPELog.Warn($"[FPE/TRACE] RadioTimerSavePatch: Radio timers set (before: wait={waitBefore}, timeout={timeoutBefore}; after: wait=0, timeout={em.radioTransmissionTimeout}). Incoming set true.");
                    }
                    else
                    {
                        FPELog.Warn($"[FPE/TRACE] RadioTimerSavePatch: Radio timers set (before: wait={waitBefore}, timeout={timeoutBefore}); no Obj_Radio found.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                FPELog.Warn($"[FPE/TRACE] RadioTimerSavePatch error: {ex}");
            }
        }
    }
}
