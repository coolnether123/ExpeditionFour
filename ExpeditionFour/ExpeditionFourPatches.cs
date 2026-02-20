using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Reflection;
using System.Linq;
using FourPersonExpeditions;

namespace FourPersonExpeditions
{
    public static class ExpeditionFourPatches
    {
        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "CalculateRouteDistance")]
        public static class CalculateRouteDistance_Patch
        {
            // Using a Postfix is cleaner here since we just need to recalculate at the end.
            // The transpiler approach would be overly complex for this simple use case.
            public static void Postfix(ExpeditionMainPanelNew __instance)
            {
                CorrectWaterRequirement(__instance);
            }
        }

        public static void CorrectWaterRequirement(ExpeditionMainPanelNew panel)
        {
            var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return;

            int selectedCount = logic.SelectedMemberIndices.Count(i => i != -1);
            if (selectedCount <= 0) return; 

            // Re-implement the calculation logic:
            float dist = Safe.GetFieldOrDefault<float>(panel, "m_routeDistance", 0f);
            
            // Stasis Mode check
            bool isStasis = false;
            if ((Object)GameModeManager.instance != (Object)null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
                isStasis = true;

            bool useVehicle = Safe.GetFieldOrDefault(panel, "useVehicle", false);
            bool useHorse = Safe.GetFieldOrDefault(panel, "useHorse", false);

            float baseReq = 0f;
            if (isStasis)
            {
                baseReq = dist / ExplorationManager.Instance.worldUnitsPerMile * ExplorationManager.Instance.m_batteryPerMile;
            }
            else
            {
                baseReq = dist / ExplorationManager.Instance.worldUnitsPerMile * ExplorationManager.Instance.waterPerPersonPerMile;
                
                // Vehicle modifiers
                if (useVehicle) baseReq *= ExplorationManager.Instance.RVWaterModifier;
                else if (useHorse) baseReq *= ExplorationManager.Instance.HorseWaterModifier;
            }

            float finalReq = baseReq * selectedCount;
            Safe.SetField(panel, "m_waterRequired", finalReq);
            
            // Also update Petrol if vehicle
            int petrol = 0;
            if (useVehicle && !isStasis)
            {
                var vehicle = Safe.GetFieldOrDefault<Obj_CamperVan>(panel, "m_vehicle", null);
                if (vehicle != null)
                {
                     petrol = Mathf.FloorToInt(dist / ExplorationManager.Instance.worldUnitsPerMile * vehicle.PetrolPerPersonPerMile * Mathf.Max(1, selectedCount));
                }
            }
            Safe.SetField(panel, "m_petrolRequired", petrol);

            // Update UI Labels so player sees correct numbers
            if (!isStasis)
            {
                if (panel.waterRequiredLabel != null && (Object)WaterManager.Instance != (Object)null)
                    panel.waterRequiredLabel.text = Mathf.Ceil(finalReq).ToString("N0") + "/" + WaterManager.Instance.StoredWater.ToString("N0");
                if (panel.petrolRequiredLabel != null)
                    panel.petrolRequiredLabel.text = Mathf.Ceil((float)petrol).ToString("N0") + "/" + InventoryManager.Instance.GetNumItemsOfType(ItemManager.ItemType.Petrol).ToString("N0");
            }
            else
            {
                // Stasis mode battery calculation - need to get combined battery for all selected members
                var hazmatScript = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.HazmatSuits_Stasis)[0] as Obj_HazmatSuit_Stasis;
                if (hazmatScript != null)
                {
                    var elig = panel.eligiblePeople;
                    
                    // Map selected members to their hazmat suit indices
                    int[] suitIndices = new int[4] { -1, -1, -1, -1 };
                    int suitCount = 0;
                    
                    for (int i = 0; i < logic.SelectedMemberIndices.Count && i < 4; i++)
                    {
                        int selIdx = logic.SelectedMemberIndices[i];
                        if (selIdx >= 0 && selIdx < elig.Count)
                        {
                            var person = elig[selIdx];
                            switch (person.firstName)
                            {
                                case "Gregory": suitIndices[suitCount++] = 0; break;
                                case "Nancy":   suitIndices[suitCount++] = 1; break;
                                case "Mark":    suitIndices[suitCount++] = 2; break;
                                case "Lucy":    suitIndices[suitCount++] = 3; break;
                            }
                        }
                    }
                    
                    // Calculate combined battery based on number of selected members
                    float combinedBattery = 0f;
                    if (suitCount == 1)
                        combinedBattery = hazmatScript.IsSuitAvailable(suitIndices[0]) ? hazmatScript.GetBatteryValue(suitIndices[0]) : 0f;
                    else if (suitCount == 2)
                        combinedBattery = hazmatScript.GetCombinedBatteryValueForParty(suitIndices[0], suitIndices[1]);
                    else if (suitCount >= 3)
                    {
                        // For 3-4 people, sum individual batteries (vanilla only supports 2)
                        for (int i = 0; i < suitCount; i++)
                        {
                            if (suitIndices[i] >= 0 && hazmatScript.IsSuitAvailable(suitIndices[i]))
                                combinedBattery += hazmatScript.GetBatteryValue(suitIndices[i]);
                        }
                    }
                    
                    combinedBattery = Mathf.Ceil(combinedBattery);
                    float remaining = Mathf.Max(0, combinedBattery - finalReq);
                    
                    // Update UI label
                    if (panel.waterRequiredLabel != null)
                        panel.waterRequiredLabel.text = remaining.ToString("N0") + "/" + combinedBattery.ToString("N0");
                    
                    // Update sufficiency flag
                    Safe.SetField(panel, "m_sufficientBatteryForTrip", combinedBattery >= finalReq);
                }
            }
        }

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "FinaliseExpedition")]
        public static class FinaliseExpedition_Patch
        {
            public static bool Prefix(ExpeditionMainPanelNew __instance, ref bool __result)
            {
                var panel = __instance;
                FPELog.Debug("FinaliseExpedition Prefix: Starting.");
                var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
                if (logic == null) 
                {
                    FPELog.Warn("FinaliseExpedition: Logic component missing. Aborting mod logic.");
                    return true;
                }

                // Sync the vanilla indices to match our first 2 slots just in case vanilla code runs later
                Safe.SetField(panel, "m_currentPerson1Index", logic.SelectedMemberIndices.Count > 0 ? logic.SelectedMemberIndices[0] : -1);
                Safe.SetField(panel, "m_currentPerson2Index", logic.SelectedMemberIndices.Count > 1 ? logic.SelectedMemberIndices[1] : -1);

                if (!Safe.TryGetField(panel, "m_loadoutScript", out ExpeditionLoadout loadout))
                {
                    FPELog.Warn("FinaliseExpedition: Loadout script not found.");
                    return true;
                }
                if (!Safe.TryGetField(panel, "m_partyId", out int partyId))
                {
                    FPELog.Warn("FinaliseExpedition: Party ID not found.");
                    return true;
                }
                
                var route = panel.route;
                int selectedCount = logic.SelectedMemberIndices.Count(i => i != -1);
                FPELog.Debug($"FinaliseExpedition: Party size={selectedCount}, Route points={route?.Count ?? 0}");

                if (selectedCount == 0 || route == null || route.Count == 0)
                {
                    FPELog.Warn("FinaliseExpedition: Validation failed (no members or no route).");
                    __result = false;
                    return false;
                }

                var selectedMembers = new List<FamilyMember>(selectedCount);

                // Process ALL party members
                for (int i = 0; i < logic.AllPartyMembers.Count; i++)
                {
                    var pm = logic.AllPartyMembers[i];
                    if (pm == null) continue;
                    
                    bool isSelected = (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1);
                    
                    if (isSelected && (Object)pm.person != (Object)null)
                    {
                        selectedMembers.Add(pm.person);
                        pm.person.job_queue.ForceClear();
                        pm.person.ai_queue.ForceClear();
                        
                        // IMPORTANT: Deduct equipment from global inventory
                        foreach (var eq in pm.GetEquippedItems())
                        {
                            InventoryManager.Instance.RemoveItemsOfType(eq.m_type, eq.m_count);
                        }
                    }
                    else
                    {
                        // Remove unselected members from the party object to clean up
                        ExplorationManager.Instance.RemoveMemberFromParty(partyId, pm);
                    }
                }

                if (selectedMembers.Count != selectedCount)
                {
                    FPELog.Warn($"FinaliseExpedition: Selected/member mismatch. expected={selectedCount}, actual={selectedMembers.Count}");
                    __result = false;
                    return false;
                }

                for (int i = 0; i < selectedMembers.Count; i++)
                {
                    var member = selectedMembers[i];
                    if ((Object)member == (Object)null) continue;

                    // Normalize movement/leave state before BeginExploring queues leave jobs.
                    member.CancelCurrentJob(true);
                    member.CancelCurrentAIJob(true);
                    member.StopClimbingImmediately(false);
                    member.finishedLeavingShelter = false;
                    // Do not force isAway=false here; BeginExploring owns this transition.
                    // Clearing it here opens a race window for external job injectors.

                    float walkSpeed = member.GetWalkSpeed();
                    if (walkSpeed <= 0f)
                    {
                        FPELog.Warn($"FinaliseExpedition: Blocking launch because {member.firstName} has zero walk speed.");
                        __result = false;
                        return false;
                    }
                }

                // Transfer Carry Items (Loadout)
                int amountWater = 0, amountRation = 0, amountMeat = 0, amountDesperate = 0;
                if (loadout != null && loadout.carriedItems != null)
                {
                    foreach (var itemSlot in loadout.carriedItems.GetItems())
                    {
                        InventoryManager.Instance.RemoveItemsOfType(itemSlot.m_type, itemSlot.m_count);
                        ExplorationManager.Instance.AddToPartyItems(partyId, itemSlot.m_type, itemSlot.m_count);
                        
                        if (itemSlot.m_type == ItemManager.ItemType.Water) amountWater += itemSlot.m_count;
                        else if (itemSlot.m_type == ItemManager.ItemType.Ration) amountRation += itemSlot.m_count;
                        else if (itemSlot.m_type == ItemManager.ItemType.Meat) amountMeat += itemSlot.m_count;
                        else if (itemSlot.m_type == ItemManager.ItemType.DesperateMeat) amountDesperate += itemSlot.m_count;
                    }
                }

                ExplorationManager.Instance.SetRoute(partyId, new List<Vector2>(route));

                // Deduct Resources (Water/Fuel)
                float waterRequired = Safe.GetFieldOrDefault(panel, "m_waterRequired", 0f);
                int petrolRequired = Safe.GetFieldOrDefault(panel, "m_petrolRequired", 0);

                if ((Object)GameModeManager.instance != (Object)null)
                {
                    if (GameModeManager.instance.currentGameMode != GameModeManager.GameMode.Stasis)
                    {
                        if (WaterManager.Instance.UseWater(waterRequired))
                            ExplorationManager.Instance.SetWater(partyId, waterRequired, WaterManager.Instance.Contamination);
                    }
                    else
                    {
                        ExplorationManager.Instance.SetBattery(partyId, waterRequired);
                    }
                }

                if (InventoryManager.Instance.GetNumItemsOfType(ItemManager.ItemType.Petrol) >= petrolRequired)
                {
                    InventoryManager.Instance.RemoveItemsOfType(ItemManager.ItemType.Petrol, petrolRequired);
                    ExplorationManager.Instance.SetPetrol(partyId, petrolRequired);
                }

                // Deduct additional items tracked by counts
                if ((Object)WaterManager.Instance != (Object)null && amountWater > 0)
                    WaterManager.Instance.UseWater((float)amountWater);
                
                if ((Object)FoodManager.Instance != (Object)null)
                {
                    if (amountRation > 0) FoodManager.Instance.TakeRations(amountRation);
                    if (amountMeat > 0) FoodManager.Instance.TakeMeat(amountMeat);
                    if (amountDesperate > 0) FoodManager.Instance.TakeDesperateMeat(amountDesperate);
                }

                if ((Object)TutorialManager.Instance != (Object)null)
                    TutorialManager.Instance.SetPopupSeen(TutorialManager.PopupType.ExpeditionReminder);

                __result = true;
                return false; // Skip original
            }
        }
        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "ConfirmExpeditionSettings")]
        public static class ConfirmExpeditionSettings_DebugPatch
        {
            public static void Prefix()
            {
                FPELog.Debug("ConfirmExpeditionSettings: Prefix reached. Showing confirmation message box.");
            }
        }
    }
}
