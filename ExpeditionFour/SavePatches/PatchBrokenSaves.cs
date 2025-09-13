using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

[HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
public static class ExplorationManager_SaveLoad_Postfix
{
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

            // First party id (Sheltered supports multiple parties, but your mod uses one)
            var firstPartyId = parties.Keys.First();

            // Any 'away' family not in assigned => add to party (up to your max)
            var everyone = FamilyManager.Instance.GetAllFamilyMembers(); // public API
            foreach (var fm in everyone)
            {
                if (fm != null && fm.isAway && !assigned.Contains(fm))
                {
                    FPELog.Info($"[FPE/TRACE] PostLoadRepair: Adding missing away member to party: {fm.firstName}");
                    PartyMember pm = null;
                    try { pm = __instance.AddMemberToParty(firstPartyId); }
                    catch (System.Exception e)
                    {
                        FPELog.Warn($"[FPE/TRACE] PostLoadRepair: AddMemberToParty threw: {e}");
                    }
                    if (pm != null)
                    {
                        pm.person = fm; // bind the person back to the member
                        FPELog.Info($"[FPE/TRACE] PostLoadRepair: Added {fm.firstName} to party #{firstPartyId}");
                    }
                    else
                    {
                        FPELog.Info($"[FPE/TRACE] PostLoadRepair: Could not add {fm.firstName} — party may be full.");
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
