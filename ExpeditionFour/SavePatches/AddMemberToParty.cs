using HarmonyLib;
using UnityEngine;
using System;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Overrides the vanilla AddMemberToParty logic to allow for more than two members in an expedition party.
    /// This is crucial for both expedition initialization and when loading saves with larger parties.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.AddMemberToParty))]
    public static class AddMemberToParty_Prefix
    {
        /// <summary>
        /// Intercepts the addition of a member to a party, enforcing the mod-defined maximum instead of the vanilla cap of two.
        /// </summary>
        public static bool Prefix(ExplorationManager __instance, int partyId, ref PartyMember __result)
        {
            try
            {
                var party = __instance.GetParty(partyId);
                if (party == null)
                {
                    __result = null;
                    return false;
                }

                int max = FourPersonConfig.MaxPartySize; 
                if (party.membersCount >= max)
                {
                    FPELog.Warn($"AddMemberToParty: Failed to add member to party {partyId}. Maximum party size of {max} reached.");
                    __result = null;
                    return false;
                }

                // Replicate vanilla behavior for component addition and registration
                var member = party.gameObject.AddComponent<PartyMember>();
                party.AddMember(member);   
                __result = member;
                
                return false; // Suppress original method to bypass vanilla capacity validation
            }
            catch (Exception ex)
            {
                FPELog.Warn($"AddMemberToParty: Unexpected error during member addition: {ex.Message}");
                __result = null;
                return false;
            }
        }
    }
}
