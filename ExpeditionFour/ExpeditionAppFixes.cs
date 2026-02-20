using HarmonyLib;
using UnityEngine;
using ModAPI.Reflection;
using System.Collections.Generic;
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
        // FIX: Apply Trauma/Stress and Water/Battery Consumption to Extra Members
        // -------------------------------------------------------------------------------------------------
        [HarmonyPatch(typeof(ExplorationParty), "Update")]
        public static class ExplorationParty_Update_Patch
        {
            public static void Postfix(ExplorationParty __instance)
            {
                if (__instance == null) return;
                
                // Only act when traveling or returning
                if (__instance.state != ExplorationParty.ePartyState.Traveling &&
                    !__instance.isReturning)
                    return;

                if (ExplorationManager.Instance == null) return;

                List<PartyMember> members = null;
                if (!Safe.TryGetField(__instance, "m_partyMembers", out members) || members == null) return;
                if (members.Count <= 2) return;

                int extraPeople = members.Count - 2;

                // Calculate movement delta from the actual expedition speed field.
                // ExplorationParty has no GetTravelSpeed() method in this game build.
                float currentSpeed = Safe.GetFieldOrDefault<float>(__instance, "m_travelSpeed", 0f);

                if (Mathf.Approximately(currentSpeed, 0f)) return;

                if (ExplorationManager.Instance.worldUnitsPerMile <= 0) return;

                float unitsMoved = currentSpeed * Time.deltaTime;
                float milesMoved = unitsMoved / ExplorationManager.Instance.worldUnitsPerMile;

                // -----------------------------------
                // 1. Water / Battery Consumption
                // -----------------------------------
                bool isStasis = false;
                if ((UnityEngine.Object)GameModeManager.instance != (UnityEngine.Object)null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
                    isStasis = true;

                if (isStasis)
                {
                    // Battery Consumption (Stasis)
                    // Vanilla: Consumes batteryPerMile * 2 (presumably)
                    // We need to consume for extra people.
                    float batteryPerMile = Safe.GetFieldOrDefault(ExplorationManager.Instance, "m_batteryPerMile", 0f);
                    if (batteryPerMile > 0)
                    {
                        float extraBattery = milesMoved * batteryPerMile * extraPeople;
                        // Deduct from party battery
                        float currentBattery = Safe.GetFieldOrDefault<float>(__instance, "m_batteryCharge", 0f);
                        currentBattery = Mathf.Max(0f, currentBattery - extraBattery);
                        Safe.SetField(__instance, "m_batteryCharge", currentBattery);
                    }
                }
                else
                {
                    // Water Consumption (Standard)
                    float waterRate = ExplorationManager.Instance.waterPerPersonPerMile;
                    
                    // Vehicle modifiers
                    Obj_CamperVan veh = null;
                    Obj_Horse horse = null;
                    Safe.TryCall<Obj_CamperVan>(__instance, "GetVehicle", out veh);
                    Safe.TryCall<Obj_Horse>(__instance, "GetHorse", out horse);

                    bool useVehicle = veh != null;
                    bool useHorse = horse != null;

                    if (useVehicle) waterRate *= ExplorationManager.Instance.RVWaterModifier;
                    else if (useHorse) waterRate *= ExplorationManager.Instance.HorseWaterModifier;

                    if (waterRate > 0)
                    {
                        float extraWater = milesMoved * waterRate * extraPeople;
                        
                        // Deduct from party water
                        float currentWater = Safe.GetFieldOrDefault<float>(__instance, "m_waterAllocated", 0f);
                        currentWater = Mathf.Max(0f, currentWater - extraWater);
                        Safe.SetField(__instance, "m_waterAllocated", currentWater);
                        
                        // Deduct petrol if vehicle?
                        if (useVehicle)
                        {
                            // Petrol is usually per mile, regardless of people? 
                            // Or per person? 
                            // Vanilla logic: "PetrolPerPersonPerMile" implies per person.
                            // ExpeditionPatches calculates petrol based on count.
                            // So we should consume extra petrol too.
                            
                             // Get vehicle script to check property
                             float petrolRate = veh.PetrolPerPersonPerMile;
                             
                             if (petrolRate > 0)
                             {
                                 float extraPetrol = milesMoved * petrolRate * extraPeople;
                                 float currentPetrol = Safe.GetFieldOrDefault<float>(__instance, "m_petrolAllocated", 0f);
                                 currentPetrol = Mathf.Max(0f, currentPetrol - extraPetrol);
                                 Safe.SetField(__instance, "m_petrolAllocated", currentPetrol);
                             }
                        }
                    }
                }

                // -----------------------------------
                // 2. Stress / Fatigue Application
                // -----------------------------------
                float stressPerMile = Safe.GetFieldOrDefault(ExplorationManager.Instance, "stressPerMile", 0f);
                if (stressPerMile <= 0) stressPerMile = Safe.GetFieldOrDefault(ExplorationManager.Instance, "stressIncreasePerMile", 0f);
                
                float fatiguePerMile = Safe.GetFieldOrDefault(ExplorationManager.Instance, "fatiguePerMile", 0f); // Guessing name

                if (stressPerMile > 0 || fatiguePerMile > 0)
                {
                    // Apply to members 2+
                    for (int i = 2; i < members.Count; i++)
                    {
                        var pm = members[i];
                        if (pm != null && pm.person != null && pm.person.stats != null)
                        {
                            if (stressPerMile > 0)
                                pm.person.stats.stress.Modify(milesMoved * stressPerMile);
                            
                            if (fatiguePerMile > 0)
                                pm.person.stats.fatigue.Modify(milesMoved * fatiguePerMile);
                        }
                    }
                }
            }
        }
    }
}
