using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpeditionFour.SavePatches
{
    // Centralized helpers
    internal static class FpeDebugFmt
    {
        public static string MemberFlags(FamilyMember m)
        {
            if (m == null) return "<null>";
            var pos = m.transform != null ? (Vector3?)m.transform.position : null;
            return $"{m.firstName}(away={m.isAway}, left={m.finishedLeavingShelter}, active={(m.gameObject?.activeSelf ?? false)}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")})";
        }

        public static string PartyLine(ExplorationParty p)
        {
            if (p == null) return "party=<null>";
            var names = new List<string>();
            var list = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                   .GetValue(p) as List<PartyMember>;
            if (list != null)
            {
                foreach (var pm in list)
                {
                    var fm = pm?.person;
                    names.Add(fm != null ? MemberFlags(fm) : "<null>");
                }
            }
            return $"party#{p.id} state={p.state} members=[{string.Join(", ", names.ToArray())}]";
        }
    }

    /// <summary>
    /// Force-consistent "away" flags for any active expedition state on load.
    /// This runs AFTER a party finishes SaveLoad so its m_partyMembers is populated.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.SaveLoad))]
    internal static class FixOnLoad_AwayFlags
    {
        // States where characters MUST be away when a save is (re)loaded.
        private static readonly HashSet<ExplorationParty.ePartyState> MustBeAwayStates =
            new HashSet<ExplorationParty.ePartyState>
            {
                ExplorationParty.ePartyState.GettingReady,
                ExplorationParty.ePartyState.LeavingShelter,
                ExplorationParty.ePartyState.VehicleLeaving,
                ExplorationParty.ePartyState.Traveling,
                ExplorationParty.ePartyState.ReportingLocation,
                ExplorationParty.ePartyState.ReportingLocationWaitUser,
                ExplorationParty.ePartyState.ReportingDiversionsStart,
                ExplorationParty.ePartyState.ReportingDiversions,
                ExplorationParty.ePartyState.ReportingDiversionsWaitUser,
                ExplorationParty.ePartyState.SearchingLocation,
                ExplorationParty.ePartyState.EncounteredItemsStart,
                ExplorationParty.ePartyState.EncounteredItems,
                ExplorationParty.ePartyState.EncounteredItemsWaitUser,
                ExplorationParty.ePartyState.EncounteredItemsRequestTransferPanel,
                ExplorationParty.ePartyState.EncounteredItemsWaitItemTransfer,
                ExplorationParty.ePartyState.EncounteredNPCsStart,
                ExplorationParty.ePartyState.EncounteredNPCs,
                ExplorationParty.ePartyState.EncounteredNPCsWaitUser,
                ExplorationParty.ePartyState.EncounteredNPCsRequestNewEncounter,
                ExplorationParty.ePartyState.EncounteredNPCsWaitFinished,
                ExplorationParty.ePartyState.EncounteredNPCsAutoResolve,
                // Open-ground encounters also occur while the party is away
                ExplorationParty.ePartyState.OpenGroundNpcEncounterStart,
                ExplorationParty.ePartyState.OpenGroundNpcEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitUser,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterRequestNewEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitFinished,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterAutoResolve,
                // Quest encounters behave like NPC encounters
                ExplorationParty.ePartyState.EncounteredQuestNPCs,
                ExplorationParty.ePartyState.EncounteredQuestNPCsWaitUser,
                ExplorationParty.ePartyState.QuestEncounterStart,
                ExplorationParty.ePartyState.QuestEncounterWaitFinished,
                ExplorationParty.ePartyState.VehicleReturning,
                // *** The critical addition: EnteringShelter ***
                ExplorationParty.ePartyState.EnteringShelter,
                // The remaining "Returned..." screens still count as away until control actually hands back.
                ExplorationParty.ePartyState.ReturnedShowExperienceGained,
                ExplorationParty.ePartyState.ReturnedRequestTransferPanel,
                ExplorationParty.ePartyState.ReturnedWaitItemTransfer
            };

        static void Postfix(ExplorationParty __instance, SaveData data)
        {
            if (__instance == null || data == null || !data.isLoading) return;

            var state = __instance.state;
            var mustBeAway = MustBeAwayStates.Contains(state);

            // Dump state & members as loaded.
            FPELog.Info($"[FPE/TRACE] PARTY LOAD: {FpeDebugFmt.PartyLine(__instance)}");

            // Reconcile members
            var memberList = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                        .GetValue(__instance) as List<PartyMember>;
            if (memberList == null) return;

            // We'll also keep a rolling index so that, if we need to
            // reposition characters, we can spread them across offscreen nodes.
            int nodeIndex = 0;
            foreach (var pm in memberList)
            {
                var person = pm?.person;
                if (person == null) continue;

                // These are the authoritative values we want while a party is active.
                if (mustBeAway)
                {
                    if (!person.isAway || !person.finishedLeavingShelter)
                    {
                        FPELog.Info($"[FPE/TRACE]  -> Fixing flags on {person.firstName}: was away={person.isAway}, left={person.finishedLeavingShelter}; forcing away=true,left=true (state={state})");
                    }

                    // Ensure away/left flags and clear any outstanding jobs like vanilla OnShelterLeft().
                    person.isAway = true;
                    person.finishedLeavingShelter = true;
                    person.job_queue?.ForceClear();
                    person.ai_queue?.ForceClear();

                    // If they spawned at/near the shelter, nudge them to an offscreen node
                    // so they aren't visibly idling around the entrance after loading.
                    try
                    {
                        var em = ExplorationManager.Instance;
                        var grid = ShelterRoomGrid.Instance;
                        var encounter = EncounterManager.Instance;
                        if (encounter != null && encounter.EncounterInProgress)
                            continue;
                        bool atShelter = true;
                        if (grid != null)
                        {
                            int cx, cy;
                            if (grid.WorldCoordsToCellCoords(person.transform.position, out cx, out cy))
                            {
                                // Outside area is y==0, and the first indoor row (y==1) close to hatch is also visible.
                                atShelter = (cy <= 1);
                            }
                        }

                        if (em != null && atShelter)
                        {
                            var nodes = em.offScreenNodesRight;
                            if (nodes == null || nodes.Count == 0) nodes = em.offScreenNodes;
                            if (nodes != null && nodes.Count > 0)
                            {
                                for (int n = 0; n < nodes.Count; n++)
                                {
                                    var idx = (nodeIndex + n) % nodes.Count;
                                    var candidate = nodes[idx];
                                    if (candidate == null) continue;
                                    if (!IsNodeOccupied(candidate, person))
                                    {
                                        person.transform.position = candidate.transform.position;
                                        nodeIndex = idx + 1;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { /* best-effort repositioning only */ }
                }
            }

            FPELog.Info($"[FPE/TRACE] PARTY POST-FIX: {FpeDebugFmt.PartyLine(__instance)}");
        }

        private static bool IsNodeOccupied(GameObject node, FamilyMember exclude)
        {
            if (node == null) return true;
            var fmList = FamilyManager.Instance?.GetAllFamilyMembers();
            if (fmList == null || fmList.Count == 0) return false;
            var nodePos = node.transform.position;
            const float minDistSq = 0.25f;
            foreach (var fm in fmList)
            {
                if (fm == null || fm == exclude || fm.transform == null) continue;
                if ((fm.transform.position - nodePos).sqrMagnitude <= minDistSq)
                    return true;
            }
            return false;
        }
    }

    // ======================= HEAVY TRACING HOOKS =======================

   
            [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class Trace_ExplorationManager_SaveLoad
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Info($"[FPE/TRACE] ExplorationManager.SaveLoad({(data?.isLoading == true ? "loading" : "saving")})");
            var tr = Traverse.Create(__instance);
            float radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
            float radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
            Obj_Radio shelterRadio = tr.Method("GetShelterRadio").GetValue<Obj_Radio>(); 
            bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

            FPELog.Info($"[FPE/TRACE]   Radio State (Prefix): Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                var emTr = Traverse.Create(__instance);
                float radioWaitTimer = emTr.Field("m_radioWaitTimer").GetValue<float>();
                float radioTimeoutTimer = emTr.Field("m_radioTimeoutTimer").GetValue<float>();
                Obj_Radio shelterRadio = emTr.Method("GetShelterRadio").GetValue<Obj_Radio>(); 
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;
                bool radioDialogPanelShowing = emTr.Field("m_radioDialogPanel").GetValue<RadioDialogPanel>().IsShowing();
                bool anyPartiesCallingIn = __instance.AnyPartiesCallingIn();

                FPELog.Info($"[FPE/TRACE]   ExplorationManager State (Postfix):");
                FPELog.Info($"[FPE/TRACE]     Radio Timers: Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}");
                FPELog.Info($"[FPE/TRACE]     Radio Incoming Transmission: {incomingTransmission}");
                FPELog.Info($"[FPE/TRACE]     Radio Dialog Panel Showing: {radioDialogPanelShowing}");
                FPELog.Info($"[FPE/TRACE]     Any Parties Calling In: {anyPartiesCallingIn}");

                var partiesDict = AccessTools.Field(typeof(ExplorationManager), "m_parties")
                                      .GetValue(__instance) as Dictionary<int, ExplorationParty>;
                if (partiesDict != null)
                {
                    foreach (var kv in partiesDict)
                    {
                        var party = kv.Value;
                        FPELog.Info($"[FPE/TRACE]   Party #{party.id} State:");
                        FPELog.Info($"[FPE/TRACE]     State: {party.state}");
                        FPELog.Info($"[FPE/TRACE]     Is Recalled: {party.isRecalled}");
                        FPELog.Info($"[FPE/TRACE]     Is Returning: {party.isReturning}");
                        FPELog.Info($"[FPE/TRACE]     Is Walking To Shelter: {party.isWalkingToShelter}");

                        var radioParams = Traverse.Create(party).Field("m_radioParams").GetValue<ExplorationManager.RadioDialogParams>();
                        if (radioParams != null)
                        {
                            FPELog.Info($"[FPE/TRACE]     Radio Params: Question='{radioParams.questionTextId}', Caller='{radioParams.caller?.firstName}', Receiver='{radioParams.receiver?.firstName}'");
                        }
                        var radioResponse = Traverse.Create(party).Field("m_radioResponse").GetValue<RadioDialogPanel.RadioResponse>();
                        FPELog.Info($"[FPE/TRACE]     Radio Response: {radioResponse}");

                        var memberList = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                                    .GetValue(party) as List<PartyMember>;
                        if (memberList != null)
                        {
                            foreach (var pm in memberList)
                            {
                                var fm = pm?.person;
                                if (fm != null)
                                {
                                    var pos = fm.transform != null ? (Vector3?)fm.transform.position : null;
                                    FPELog.Info($"[FPE/TRACE]       Member: {fm.firstName} (isAway={fm.isAway}, finishedLeavingShelter={fm.finishedLeavingShelter}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")}, job_queue.size={fm.job_queue?.size}, ai_queue.size={fm.ai_queue?.size})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"[FPE/TRACE] ExplorationManager.SaveLoad post-trace error: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.RecallToShelter))]
    internal static class Trace_Recall
    {
        static void Postfix(ExplorationParty __instance)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Info($"[FPE/TRACE] RECALL: {FpeDebugFmt.PartyLine(__instance)}");
        }
    }

    // Log “left/returned” job callbacks – this is where the state flips in vanilla.
    [HarmonyPatch(typeof(Job_GoToLocation), "BeginJob")]
    internal static class Trace_GoToLocation_Begin
    {
        static void Postfix(Job_GoToLocation __instance)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                var ch = AccessTools.Field(typeof(Job), "character").GetValue(__instance) as FamilyMember;
                var action = AccessTools.Field(typeof(Job_GoToLocation), "m_callbackAction").GetValue(__instance);
                if (ch != null)
                    FPELog.Info($"[FPE/TRACE] Job_GoToLocation.BeginJob for {ch.firstName} action={action}");
            }
            catch { /* best effort */ }
        }
    }
}
