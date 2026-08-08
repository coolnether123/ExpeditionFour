using HarmonyLib;
using UnityEngine;
using ModAPI.Reflection;
using System;

namespace FourPersonExpeditions
{
    public static class ExpeditionAppFixes
    {
        [HarmonyPatch(typeof(ExplorationManager), "UpdateManager")]
        public static class ExplorationManager_UpdateManager_RadioCleanupPatch
        {
            public static void Postfix(ExplorationManager __instance)
            {
                if (__instance == null) return;

                try
                {
                    if (__instance.AnyPartiesCallingIn()) return;

                    var radioDialogPanel = Safe.GetFieldOrDefault<RadioDialogPanel>(__instance, "m_radioDialogPanel", null);
                    if (radioDialogPanel != null && radioDialogPanel.IsShowing()) return;

                    float timeout = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
                    if (timeout > Mathf.Epsilon) return;

                    if (!Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio) || shelterRadio == null)
                        return;

                    if (!shelterRadio.incomingTransmission) return;

                    shelterRadio.incomingTransmission = false;
                    Safe.SetField(__instance, "m_radioCallback", (ExplorationManager.RadioDialogCallback)null);

                    FPELog.Debug("[RadioCleanup] Cleared stale incoming transmission with no active party call state.");
                }
                catch (Exception ex)
                {
                    FPELog.Warn($"[RadioCleanup] Failed to run cleanup: {ex.Message}");
                }
            }
        }

        // -------------------------------------------------------------------------------------------------
        // FIX: Match four-person petrol scaling during travel
        // -------------------------------------------------------------------------------------------------
        // The vanilla build already scales water and travel deterioration across every
        // PartyMember in Begin_Traveling/Update_Traveling.  The old per-frame patch used
        // fields that do not exist in this build and also re-applied fatigue to members
        // three and four.  Scale the actual vanilla petrol rate once, after vanilla has
        // initialized it, so the amount charged at send time is consumed consistently.
        [HarmonyPatch(typeof(ExplorationParty), "Begin_Traveling")]
        public static class ExplorationParty_BeginTraveling_PetrolScalingPatch
        {
            public static void Postfix(ExplorationParty __instance)
            {
                if (__instance == null || __instance.membersCount <= 2)
                    return;

                Obj_CamperVan vehicle = Safe.GetFieldOrDefault<Obj_CamperVan>(__instance, "m_vehicle", null);
                if (vehicle == null)
                    return;

                float vanillaRate = Safe.GetFieldOrDefault<float>(__instance, "m_petrolUsePerWorldUnit", 0f);
                if (vanillaRate <= 0f)
                    return;

                Safe.SetField(__instance, "m_petrolUsePerWorldUnit", vanillaRate * __instance.membersCount);
                FPELog.Debug($"[TravelScaling] Petrol rate scaled for {__instance.membersCount} expedition members.");
            }
        }

        // Vanilla stores the combined stasis battery requirement on the party,
        // then subtracts that combined amount from every returned suit. Divide
        // the shared value back across the party before the vanilla call so a
        // four-person return consumes each suit's per-person travel cost once.
        [HarmonyPatch(typeof(Obj_HazmatSuit_Stasis), nameof(Obj_HazmatSuit_Stasis.ReduceSuitBatteryValue))]
        public static class Obj_HazmatSuit_Stasis_ReduceBatteryPatch
        {
            public static void Prefix(int suitIndex, ref float value)
            {
                var manager = ExplorationManager.Instance;
                if (manager == null || value <= 0f)
                    return;

                var parties = manager.GetAllExplorarionParties();
                if (parties == null)
                    return;

                foreach (var party in parties)
                {
                    if (party == null || !party.isReturning || party.membersCount <= 1)
                        continue;

                    for (int i = 0; i < party.membersCount; i++)
                    {
                        var member = party.GetMember(i);
                        if (member != null && member.GetHazmatNumber() == suitIndex)
                        {
                            value = Mathf.Ceil(value / party.membersCount);
                            FPELog.Debug($"[StasisReturn] Divided battery charge {value} across {party.membersCount} returning members.");
                            return;
                        }
                    }
                }
            }
        }
    }
}
