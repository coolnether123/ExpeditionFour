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
            FPELog.Warn($"[FPE/TRACE] PARTY LOAD: {FpeDebugFmt.PartyLine(__instance)}");

            // Reconcile members
            var memberList = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                        .GetValue(__instance) as List<PartyMember>;
            if (memberList == null) return;

            foreach (var pm in memberList)
            {
                var person = pm?.person;
                if (person == null) continue;

                // These are the authoritative values we want while a party is active.
                if (mustBeAway)
                {
                    if (!person.isAway || !person.finishedLeavingShelter)
                    {
                        FPELog.Warn($"[FPE/TRACE]  -> Fixing flags on {person.firstName}: was away={person.isAway}, left={person.finishedLeavingShelter}; forcing away=true,left=true (state={state})");
                    }

                    // Setting the property ensures OnAwayFromShelter() runs.
                    person.isAway = true;
                    person.finishedLeavingShelter = true;

                    // If they happen to be visible because FamilyManager spawned them earlier,
                    // re-assert the away transition by calling the setter again (no-op if already set).
                    person.isAway = true;
                }
            }

            FPELog.Warn($"[FPE/TRACE] PARTY POST-FIX: {FpeDebugFmt.PartyLine(__instance)}");
        }
    }

    // ======================= HEAVY TRACING HOOKS =======================

   
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class Trace_ExplorationManager_SaveLoad
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            FPELog.Warn($"[FPE/TRACE] ExplorationManager.SaveLoad({(data?.isLoading == true ? "loading" : "saving")})");
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            try
            {
                var dict = AccessTools.Field(typeof(ExplorationManager), "m_parties")
                                      .GetValue(__instance) as Dictionary<int, ExplorationParty>;
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        FPELog.Warn($"[FPE/TRACE] {FpeDebugFmt.PartyLine(kv.Value)}");
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
            FPELog.Warn($"[FPE/TRACE] RECALL: {FpeDebugFmt.PartyLine(__instance)}");
        }
    }

    // Log “left/returned” job callbacks – this is where the state flips in vanilla.
    [HarmonyPatch(typeof(Job_GoToLocation), "BeginJob")]
    internal static class Trace_GoToLocation_Begin
    {
        static void Postfix(Job_GoToLocation __instance)
        {
            try
            {
                var ch = AccessTools.Field(typeof(Job), "character").GetValue(__instance) as FamilyMember;
                var action = AccessTools.Field(typeof(Job_GoToLocation), "m_callbackAction").GetValue(__instance);
                if (ch != null)
                    FPELog.Warn($"[FPE/TRACE] Job_GoToLocation.BeginJob for {ch.firstName} action={action}");
            }
            catch { /* best effort */ }
        }
    }
}
