using HarmonyLib;
using UnityEngine;
using System;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Replaces the vanilla two-member limit with the configured party limit.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.AddMemberToParty))]
    public static class AddMemberToParty_Prefix
    {
        /// <summary>
        /// Adds a member when the party is below the configured limit.
        /// </summary>
        public static bool Prefix(ExplorationManager __instance, int partyId, ref PartyMember __result)
        {
            try
            {
                var party = __instance.GetParty(partyId);
                if (party == null)
                {
                    FPELog.Warn($"[FPE] AddMemberToParty: FAILED - Party {partyId} not found.");
                    __result = null;
                    return false;
                }

                int max = FourPersonConfig.MaxPartySize; 
                if (party.membersCount >= max)
                {
                    FPELog.Warn($"[FPE] AddMemberToParty: FAILED - Party {partyId} already has {party.membersCount} members (Max={max}).");
                    __result = null;
                    return false;
                }

                FPELog.Debug($"[FPE] AddMemberToParty: Adding member to party {partyId}. Current size: {party.membersCount}");

                // Replicate vanilla behavior for component addition and registration
                var member = party.gameObject.AddComponent<PartyMember>();
                party.AddMember(member);   
                __result = member;
                
                FPELog.Debug($"[FPE] AddMemberToParty: SUCCESS - New size: {party.membersCount}");
                return false; // Suppress original method to bypass vanilla capacity validation
            }
            catch (Exception ex)
            {
                FPELog.Error($"[FPE] AddMemberToParty: CRASH - {ex}");
                __result = null;
                return false;
            }
        }
    }
}
