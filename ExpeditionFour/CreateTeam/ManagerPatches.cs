using HarmonyLib;
using UnityEngine;

// REWRITTEN: This patch now fully replaces the original method.
[HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.AddMemberToParty))]
public static class ExplorationManager_AddMemberToParty_Patch
{
    // This is now a full replacement, so it must return false.
    public static bool Prefix(ExplorationManager __instance, int partyId, ref PartyMember __result)
    {
        var party = __instance.GetParty(partyId);
        if (party == null)
        {
            __result = null;
            return false; // Do not run original
        }

        int limit = Mathf.Max(2, FourPersonConfig.MaxPartySize);
        FPELog.Info($"AddMemberToParty Patch: Checking party capacity. Current: {party.membersCount}, Max: {limit}.");

        if (party.membersCount >= limit)
        {
            FPELog.Warn("AddMemberToParty Patch: Party is full. Blocking addition.");
            __result = null;
            return false; // Do not run original
        }
        
        FPELog.Info("AddMemberToParty Patch: Party has space. Adding new member.");
        
        // This is the logic from the original method, now controlled by us.
        PartyMember member = party.gameObject.AddComponent<PartyMember>();
        party.AddMember(member);
        __result = member; // Set the return value

        return false; // IMPORTANT: We skip the original method entirely.
    }
}