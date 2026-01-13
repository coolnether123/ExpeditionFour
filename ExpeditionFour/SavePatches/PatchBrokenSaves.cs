using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Attempts to repair save files where characters are marked as "away" but are not assigned to any expedition party.
    /// This can happen due to mod conflicts or unexpected game shutdowns.
    /// </summary>
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
                if (!Safe.TryGetField(__instance, "m_parties", out Dictionary<int, ExplorationParty> parties) ||
                    parties == null || parties.Count == 0) return;

                // Build a set of characters currently assigned to any active party
                var assignedCharacters = new HashSet<FamilyMember>();
                foreach (var party in parties.Values)
                {
                    for (int i = 0; i < party.membersCount; i++)
                    {
                        var member = party.GetMember(i);
                        if (member?.person != null) assignedCharacters.Add(member.person);
                    }
                }

                // Identify the most suitable party to receive orphaned "away" members
                ExplorationParty recipientParty = parties.Values
                    .Where(p => p != null && p.membersCount < FourPersonConfig.MaxPartySize)
                    .OrderByDescending(p => AwayStates.Contains(p.state))
                    .ThenByDescending(p => p.membersCount)
                    .ThenBy(p => p.id)
                    .FirstOrDefault();

                if (recipientParty == null)
                {
                    FPELog.Info("SaveRepair: No eligible party found to host orphaned expedition members.");
                    return;
                }

                int partyId = recipientParty.id;
                var familyMembers = FamilyManager.Instance.GetAllFamilyMembers();

                foreach (var fm in familyMembers)
                {
                    // If a character is marked away but not found in any party's member list
                    if (fm != null && fm.isAway && !assignedCharacters.Contains(fm))
                    {
                        FPELog.Info($"SaveRepair: Character '{fm.firstName}' is orphaned. Attempting to re-attach to party #{partyId}.");
                        
                        PartyMember pm = null;
                        try { pm = __instance.AddMemberToParty(partyId); }
                        catch (Exception ex)
                        {
                            FPELog.Warn($"SaveRepair: Failed to add member structure to party #{partyId}: {ex.Message}");
                        }

                        if (pm != null)
                        {
                            pm.person = fm; 
                            FPELog.Info($"SaveRepair: Successfully re-attached '{fm.firstName}' to party #{partyId}.");
                        }
                        else
                        {
                            FPELog.Warn($"SaveRepair: Could not re-attach '{fm.firstName}'. The target party might have reached the capacity limit.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FPELog.Warn($"SaveRepair: Encountered an error during post-load reconciliation: {ex.Message}");
            }
        }
    }
}
