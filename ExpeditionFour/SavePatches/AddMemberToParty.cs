using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.SavePatches
{
    /// <summary>
    /// Lift the vanilla 2-member cap during both runtime and LOAD.
    /// Mirrors the original method's behavior but allows up to our configured cap.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.AddMemberToParty))]
    public static class AddMemberToParty_Prefix
    {
        // return false to skip original; __result must be set
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

                int max = FourPersonConfig.MaxPartySize; // e.g., 4
                if (party.membersCount >= max)
                {
                    __result = null;
                    return false;
                }

                // Vanilla pattern: add PartyMember component and register with party
                var member = party.gameObject.AddComponent<PartyMember>();
                party.AddMember(member);   // sets member.partyId and adds to list
                __result = member;
                return false;              // skip original (which enforces 2)
            }
            catch (System.Exception ex)
            {
                FPELog.Warn($"[FPE] AddMemberToParty_Prefix failed: {ex}");
                __result = null;
                return false;
            }
        }
    }
}
