using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Contains formatting helpers for debug logging of mod-specific data.
    /// </summary>
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
            
            if (Safe.TryGetField(p, "m_partyMembers", out List<PartyMember> list) && list != null)
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
    /// Forces consistent "away" flags for all active expedition members upon loading a save.
    /// This prevents issues where characters might appear in the shelter while logically being on an expedition.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.SaveLoad))]
    internal static class FixOnLoad_AwayFlags
    {
        // States where characters MUST be flagged as "away" when a save is loaded.
        private static readonly HashSet<ExplorationParty.ePartyState> MustBeAwayStates =
            new HashSet<ExplorationParty.ePartyState>
            {
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
                ExplorationParty.ePartyState.OpenGroundNpcEncounterStart,
                ExplorationParty.ePartyState.OpenGroundNpcEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitUser,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterRequestNewEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitFinished,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterAutoResolve,
                ExplorationParty.ePartyState.EncounteredQuestNPCs,
                ExplorationParty.ePartyState.EncounteredQuestNPCsWaitUser,
                ExplorationParty.ePartyState.QuestEncounterStart,
                ExplorationParty.ePartyState.QuestEncounterWaitFinished,
                ExplorationParty.ePartyState.ReturnedShowExperienceGained,
                ExplorationParty.ePartyState.ReturnedRequestTransferPanel,
                ExplorationParty.ePartyState.ReturnedWaitItemTransfer
            };

        static void Postfix(ExplorationParty __instance, SaveData data)
        {
            if (__instance == null || data == null || !data.isLoading) return;

            var state = __instance.state;
            var mustBeAway = MustBeAwayStates.Contains(state);

            // Removed verbose party load trace for release
            // FPELog.Info($"Party Load Trace: {FpeDebugFmt.PartyLine(__instance)}");

            if (!Safe.TryGetField(__instance, "m_partyMembers", out List<PartyMember> memberList) || memberList == null) 
                return;

            int nodeIndex = 0;
            foreach (var pm in memberList)
            {
                var person = pm?.person;
                if (person == null) continue;

                if (mustBeAway)
                {
                    if (!person.isAway || !person.finishedLeavingShelter)
                    {
                        FPELog.Info($"Party Load: Synchronizing flags for {person.firstName}. Setting away and finishedLeavingShelter to true.");
                    }

                    // Enforce expedition state flags and clear character job queues
                    person.isAway = true;
                    person.finishedLeavingShelter = true;
                    person.job_queue?.ForceClear();
                    person.ai_queue?.ForceClear();

                    try
                    {
                        var em = ExplorationManager.Instance;
                        var grid = ShelterRoomGrid.Instance;
                        var encounter = EncounterManager.Instance;
                        
                        // Skip repositioning if an encounter is already in progress
                        if (encounter != null && encounter.EncounterInProgress)
                            continue;
                            
                        bool atShelter = true;
                        if (grid != null)
                        {
                            if (grid.WorldCoordsToCellCoords(person.transform.position, out int cx, out int cy))
                            {
                                // y <= 1 represents the surface and upper shelter areas visible to the user
                                atShelter = (cy <= 1);
                            }
                        }

                        if (em != null && atShelter)
                        {
                            // Shift character to an off-screen node to maintain immersion
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
                    catch { /* repositioning is non-critical */ }
                }
            }

            // Repair Logic: Fix parties stuck in transition states if all members are flagged 'Away'.
            // This happens if a save occurs during transition, or due to previous mod logic forcing 'Away'.
            bool allAway = true;
            foreach (var pm in memberList)
            {
                if (pm?.person != null && !pm.person.isAway)
                {
                    allAway = false;
                    break;
                }
            }

            if (allAway)
            {
                if (state == ExplorationParty.ePartyState.LeavingShelter || state == ExplorationParty.ePartyState.VehicleLeaving)
                {
                    FPELog.Info($"Party Load Repair: Party #{__instance.id} stuck in {state} but all members are Away. Forcing state to Traveling.");
                    Safe.SetField(__instance, "m_state", ExplorationParty.ePartyState.Traveling);
                }
                else if (state == ExplorationParty.ePartyState.EnteringShelter || state == ExplorationParty.ePartyState.VehicleReturning)
                {
                    FPELog.Info($"Party Load Repair: Party #{__instance.id} stuck in {state} but all members are Away. Forcing state to ReturnedShowExperienceGained.");
                    Safe.SetField(__instance, "m_state", ExplorationParty.ePartyState.ReturnedShowExperienceGained);
                }
            }

            // Removed verbose party load completion trace for release
            // FPELog.Info($"Party Load Trace Completion: {FpeDebugFmt.PartyLine(__instance)}");
        }

        private static bool IsNodeOccupied(GameObject node, FamilyMember exclude)
        {
            if (node == null) return true;
            var fmList = FamilyManager.Instance?.GetAllFamilyMembers();
            if (fmList == null || fmList.Count == 0 || node.transform == null) return false;
            
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

    /// <summary>
    /// Provides detailed tracing for ExplorationManager SaveLoad operations when debug mode is enabled.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class Trace_ExplorationManager_SaveLoad
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Info($"ExplorationManager.SaveLoad Trace ({(data?.isLoading == true ? "Loading" : "Saving")})");
            
            float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            
            Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
            bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

            FPELog.Info($"Radio State (Prefix): Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
                float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
                
                Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;
                
                RadioDialogPanel panel = Safe.GetFieldOrDefault<RadioDialogPanel>(__instance, "m_radioDialogPanel", null);
                bool radioDialogPanelShowing = panel != null && panel.IsShowing();
                bool anyPartiesCallingIn = __instance.AnyPartiesCallingIn();

                FPELog.Info($"ExplorationManager State (Postfix): Timers=[{radioWaitTimer}/{radioTimeoutTimer}], Incoming=[{incomingTransmission}], DialogShowing=[{radioDialogPanelShowing}], PartiesCalling=[{anyPartiesCallingIn}]");

                if (Safe.TryGetField(__instance, "m_parties", out Dictionary<int, ExplorationParty> partiesDict) && partiesDict != null)
                {
                    foreach (var kv in partiesDict)
                    {
                        var party = kv.Value;
                        FPELog.Info($"Party #{party.id} State: State={party.state}, Recalled={party.isRecalled}, Returning={party.isReturning}, WalkingToShelter={party.isWalkingToShelter}");

                        ExplorationManager.RadioDialogParams radioParams = Safe.GetFieldOrDefault<ExplorationManager.RadioDialogParams>(party, "m_radioParams", null);
                        if (radioParams != null)
                        {
                            FPELog.Info($"Radio Params: Msg='{radioParams.questionTextId}', Caller='{radioParams.caller?.firstName}', Receiver='{radioParams.receiver?.firstName}'");
                        }
                        
                        var radioResponse = Safe.GetFieldOrDefault(party, "m_radioResponse", default(RadioDialogPanel.RadioResponse));
                        FPELog.Info($"Radio Response: {radioResponse}");

                        if (Safe.TryGetField(party, "m_partyMembers", out List<PartyMember> memberList) && memberList != null)
                        {
                            foreach (var pm in memberList)
                            {
                                var fm = pm?.person;
                                if (fm != null)
                                {
                                    var pos = fm.transform != null ? (Vector3?)fm.transform.position : null;
                                    FPELog.Info($"Party Member: {fm.firstName} (isAway={fm.isAway}, left={fm.finishedLeavingShelter}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")}, jobs={fm.job_queue?.size}, ai={fm.ai_queue?.size})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"ExplorationManager.SaveLoad trace error: {e}");
            }
        }
    }

    /// <summary>
    /// Logs when a party is recalled to the shelter.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.RecallToShelter))]
    internal static class Trace_Recall
    {
        static void Postfix(ExplorationParty __instance)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Info($"Party Recall Trace: {FpeDebugFmt.PartyLine(__instance)}");
        }
    }

    /// <summary>
    /// Traces the beginning of GoToLocation jobs to monitor character movement across state boundaries.
    /// </summary>
    [HarmonyPatch(typeof(Job_GoToLocation), "BeginJob")]
    internal static class Trace_GoToLocation_Begin
    {
        static void Postfix(Job_GoToLocation __instance)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                if (!Safe.TryGetField(__instance, "character", out FamilyMember ch) || ch == null) return;
                if (!Safe.TryGetField(__instance, "m_callbackAction", out object action)) return;
                
                FPELog.Info($"Job Trace: GoToLocation.BeginJob for {ch.firstName}, Callback={action}");
            }
            catch { /* best effort trace */ }
        }
    }
}
