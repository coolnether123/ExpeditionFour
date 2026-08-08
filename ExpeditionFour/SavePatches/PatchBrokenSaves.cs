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
        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            if (__instance == null || data == null || !data.isLoading) return;

            try
            {
                if (!Safe.TryGetField(__instance, "m_parties", out Dictionary<int, ExplorationParty> parties) ||
                    parties == null || parties.Count == 0) return;

                // Build a set of characters currently assigned to any active party
                var assignedCharacters = new HashSet<FamilyMember>();
                foreach (var party in parties.Values)
                {
                    if (party == null) continue;
                    for (int i = 0; i < party.membersCount; i++)
                    {
                        var member = party.GetMember(i);
                        if (member?.person != null) assignedCharacters.Add(member.person);
                    }
                }

                if (FamilyManager.Instance == null)
                {
                    FPELog.Warn("SaveRepair: FamilyManager is unavailable; skipping orphan reconciliation.");
                    return;
                }

                var familyMembers = FamilyManager.Instance.GetAllFamilyMembers();
                if (familyMembers == null)
                {
                    FPELog.Warn("SaveRepair: FamilyManager returned no family list; skipping orphan reconciliation.");
                    return;
                }

                foreach (var fm in familyMembers)
                {
                    // If a character is marked away but not found in any party's member list
                    if (fm != null && fm.isAway && !assignedCharacters.Contains(fm))
                    {
                        ExplorationParty recipientParty = parties.Values
                            .Where(p => p != null
                                && p.membersCount < FourPersonConfig.MaxPartySize
                                && p.state != ExplorationParty.ePartyState.None
                                && p.state != ExplorationParty.ePartyState.Finished)
                            .OrderByDescending(p => ExpeditionSaveStatePolicy.AwayStates.Contains(p.state))
                            .ThenByDescending(p => p.membersCount)
                            .ThenBy(p => p.id)
                            .FirstOrDefault();

                        if (recipientParty == null)
                        {
                            FPELog.Warn($"SaveRepair: No eligible party found for orphaned character '{fm.firstName}'.");
                            continue;
                        }

                        int partyId = recipientParty.id;
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
                            assignedCharacters.Add(fm);
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
