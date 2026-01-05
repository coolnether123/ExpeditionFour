using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

[HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
public static class ExplorationManager_SaveLoad_Postfix
{
    private static readonly HashSet<ExplorationParty.ePartyState> AwayStates =
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
            ExplorationParty.ePartyState.VehicleReturning,
            ExplorationParty.ePartyState.EnteringShelter,
            ExplorationParty.ePartyState.ReturnedShowExperienceGained,
            ExplorationParty.ePartyState.ReturnedRequestTransferPanel,
            ExplorationParty.ePartyState.ReturnedWaitItemTransfer,
            ExplorationParty.ePartyState.HorseLeaving,
            ExplorationParty.ePartyState.HorseReturning,
            ExplorationParty.ePartyState.EnteringShelterNextUpdate,
            ExplorationParty.ePartyState.ReturnedShowHazmatExperienceGained,
        };

    static void Postfix(ExplorationManager __instance, SaveData data)
    {
        if (!data.isLoading) return;

        try
        {
            // Access private dictionary m_parties -> { partyId => ExplorationParty }
            var partiesField = AccessTools.Field(typeof(ExplorationManager), "m_parties");
            var parties = partiesField.GetValue(__instance) as IDictionary<int, ExplorationParty>;
            if (parties == null || parties.Count == 0) return;

            // Build a set of FamilyMembers already assigned to any party
            var assigned = new HashSet<FamilyMember>();
            foreach (var p in parties.Values)
            {
                for (int i = 0; i < p.membersCount; i++)
                {
                    var m = p.GetMember(i);
                    if (m?.person != null) assigned.Add(m.person);
                }
            }

            // Pick the most likely active party (avoid relying on unordered dictionary keys).
            ExplorationParty targetParty = null;
            var candidates = parties.Values
                .Where(p => p != null && p.membersCount < FourPersonConfig.MaxPartySize)
                .OrderByDescending(p => AwayStates.Contains(p.state))
                .ThenByDescending(p => p.membersCount)
                .ThenBy(p => p.id)
                .ToList();
            if (candidates.Count > 0) targetParty = candidates[0];
            if (targetParty == null)
            {
                FPELog.Warn("[FPE/TRACE] PostLoadRepair: No eligible party found to attach away members.");
                return;
            }
            var targetPartyId = targetParty.id;

            // Any 'away' family not in assigned => add to party (up to your max)
            var everyone = FamilyManager.Instance.GetAllFamilyMembers(); // public API
            foreach (var fm in everyone)
            {
                if (fm != null && fm.isAway && !assigned.Contains(fm))
                {
                    FPELog.Info($"[FPE/TRACE] PostLoadRepair: Adding missing away member to party #{targetPartyId}: {fm.firstName}");
                    PartyMember pm = null;
                    try { pm = __instance.AddMemberToParty(targetPartyId); }
                    catch (System.Exception e)
                    {
                        FPELog.Warn($"[FPE/TRACE] PostLoadRepair: AddMemberToParty threw: {e}");
                    }
                    if (pm != null)
                    {
                        pm.person = fm; // bind the person back to the member
                        FPELog.Info($"[FPE/TRACE] PostLoadRepair: Added {fm.firstName} to party #{targetPartyId}");
                    }
                    else
                    {
                        FPELog.Info($"[FPE/TRACE] PostLoadRepair: Could not add {fm.firstName} - party may be full.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            FPELog.Warn("[FourPersonExpeditions] Post-load repair failed: " + ex);
        }
    }
}
