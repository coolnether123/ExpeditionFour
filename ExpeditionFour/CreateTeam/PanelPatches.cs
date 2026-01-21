using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;



[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnSelect))]
public static class ExpeditionMainPanelNew_OnSelect_Patch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null || __instance.PartySetup == null || !__instance.PartySetup.activeInHierarchy) return true;

        int index = logic.HighlightedIndices[logic.ActiveSelectionSlot];
        logic.SelectedMemberIndices[logic.ActiveSelectionSlot] = index;
        FPELog.Debug($"OnSelect Patch: Stored selection. Slot: {logic.ActiveSelectionSlot}, Character Index: {index}.");

        if (index != -1 && logic.ActiveSelectionSlot < logic.MaxPartySize - 1)
        {
            FPELog.Debug("OnSelect Patch: Advancing to next party slot.");
            logic.ActiveSelectionSlot++;

            // Find the first available character to be the default highlight for the new slot.
            // Default the next slot to 'None' (-1) to force manual selection, as requested.
            int firstAvailable = -1;
            logic.HighlightedIndices[logic.ActiveSelectionSlot] = firstAvailable;

            __instance.partySetupScript?.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
            return false;
        }
        else
        {
            FPELog.Debug("OnSelect Patch: Finalizing selections and showing map.");
            Safe.InvokeMethod(__instance, "UpdatePartyMembers");
            __instance.ShowMapMenu();
            return false;
        }
    }
}

[HarmonyPatch(typeof(ExpeditionMainPanelNew), "UpdatePartyMembers")]
public static class ExpeditionMainPanelNew_UpdatePartyMembers_Patch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null) return true;
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        FPELog.Debug("UpdatePartyMembers Patch: Assigning selected family members to the party.");
        int max = Mathf.Min(logic.MaxPartySize, logic.AllPartyMembers.Count);
        var elig = __instance.eligiblePeople;
        for (int i = 0; i < max; i++)
        {
            var partyMember = logic.AllPartyMembers[i];
            if (partyMember == null) continue;
            int selIndex = (i < logic.SelectedMemberIndices.Count) ? logic.SelectedMemberIndices[i] : -1;
            FamilyMember fam = null;
            if (selIndex >= 0 && selIndex < elig.Count)
                fam = elig[selIndex];

            if ((UnityEngine.Object)partyMember.person != (UnityEngine.Object)fam)
            {
                string famName = (fam != null) ? fam.firstName : "Nobody";
                FPELog.Debug($"UpdatePartyMembers Patch: Assigning '{famName}' to party slot {i}.");
                partyMember.person = fam;
                partyMember.ClearAllEquipment();
            }
        }

        if ((UnityEngine.Object)GameModeManager.instance != (UnityEngine.Object)null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
        {
            for (int i = 0; i < max; i++)
            {
                var partyMember = logic.AllPartyMembers[i];
                if ((UnityEngine.Object)partyMember == (UnityEngine.Object)null || (UnityEngine.Object)partyMember.person == (UnityEngine.Object)null) continue;
                int haz = -1;
                switch (partyMember.person.firstName)
                {
                    case "Gregory": haz = 0; break;
                    case "Nancy": haz = 1; break;
                    case "Mark": haz = 2; break;
                    case "Lucy": haz = 3; break;
                }
                if (haz >= 0) partyMember.SetHazmatNumber(haz);
            }
        }
        return false;
    }
}




[HarmonyPatch(typeof(ExpeditionMainPanelNew), "CalculateRouteDistance")]
public static class ExpeditionMainPanelNew_CalculateRouteDistance_Patch
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null) return;
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return;

        int selectedCount = 0;
        for (int i = 0; i < logic.SelectedMemberIndices.Count; i++)
            if (logic.SelectedMemberIndices[i] != -1) selectedCount++;
        selectedCount = Mathf.Clamp(selectedCount, 0, logic.MaxPartySize);

        float waterRequired = 0f;
        int petrolRequired = 0;

        if (!Safe.TryGetField(__instance, "m_routeDistance", out float routeDistance))
            return;

        if ((UnityEngine.Object)GameModeManager.instance != (UnityEngine.Object)null &&
            GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
        {
            waterRequired = routeDistance / ExplorationManager.Instance.worldUnitsPerMile * ExplorationManager.Instance.m_batteryPerMile;
            waterRequired *= selectedCount;
        }
        else
        {
            waterRequired = routeDistance / ExplorationManager.Instance.worldUnitsPerMile * ExplorationManager.Instance.waterPerPersonPerMile;
            waterRequired *= selectedCount;
            
            Safe.TryGetField(ExplorationManager.Instance, "m_parties", out System.Collections.IDictionary parties);
            int pCount = parties != null ? parties.Count : -1;
            

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"CalculateRouteDistance: Count={selectedCount}, Water={waterRequired}. GlobalPartyStatus: ActiveParties={pCount} (includes current). Parties: ");
            if (parties != null)
            {
                foreach (System.Collections.DictionaryEntry entry in parties)
                {
                    object p = entry.Value;
                    string state = "Unknown";
                    int mCount = 0;
                    if (p != null) 
                    {
                        Safe.TryGetField(p, "state", out object stateObj);
                        state = stateObj?.ToString() ?? "NullState";
                        if (Safe.TryGetField(p, "m_partyMembers", out System.Collections.IList mems) && mems != null)
                            mCount = mems.Count;
                    }
                    sb.Append($"[ID {entry.Key}: {state} ({mCount} mems)] ");
                }
            }
            // Removed verbose party status logging - called too frequently during route selection
            // FPELog.Info(sb.ToString());

            bool useVehicle = Safe.GetFieldOrDefault(__instance, "useVehicle", false);
            bool useHorse = Safe.GetFieldOrDefault(__instance, "useHorse", false);
            var vehicle = Safe.GetFieldOrDefault<Obj_CamperVan>(__instance, "m_vehicle", null);
            if (useVehicle && vehicle != null)
            {
                petrolRequired = Mathf.FloorToInt(routeDistance / ExplorationManager.Instance.worldUnitsPerMile * vehicle.PetrolPerPersonPerMile * Mathf.Max(1, selectedCount));
                waterRequired *= ExplorationManager.Instance.RVWaterModifier;
            }
            else if (useHorse)
            {
                petrolRequired = 0;
                waterRequired *= ExplorationManager.Instance.HorseWaterModifier;
            }
            else
            {
                petrolRequired = 0;
            }
        }

        Safe.SetField(__instance, "m_waterRequired", waterRequired);
        Safe.SetField(__instance, "m_petrolRequired", petrolRequired);

        if ((UnityEngine.Object)GameModeManager.instance == (UnityEngine.Object)null || GameModeManager.instance.currentGameMode != GameModeManager.GameMode.Stasis)
        {
            if (__instance.waterRequiredLabel != null && (UnityEngine.Object)WaterManager.Instance != (UnityEngine.Object)null)
                __instance.waterRequiredLabel.text = Mathf.Ceil(waterRequired).ToString("N0") + "/" + WaterManager.Instance.StoredWater.ToString("N0");
            if (__instance.petrolRequiredLabel != null)
                __instance.petrolRequiredLabel.text = Mathf.Ceil((float)petrolRequired).ToString("N0") + "/" + InventoryManager.Instance.GetNumItemsOfType(ItemManager.ItemType.Petrol).ToString("N0");
        }
    }
}

[HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
public static class ExpeditionMainPanelNew_Update_Patch
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null || !__instance.MapScreen.activeInHierarchy) return;

        var logic = __instance.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return;

        // 1. Check if at least one person is selected
        bool anyoneSelected = false;
        foreach (int idx in logic.SelectedMemberIndices)
        {
            if (idx != -1) { anyoneSelected = true; break; }
        }

        // 2. Check route
        var route = __instance.route;
        bool hasRoute = route != null && route.Count > 0;

        // 3. Check Resources
        float waterReq = Safe.GetFieldOrDefault(__instance, "m_waterRequired", 0f);
        int petrolReq = Safe.GetFieldOrDefault(__instance, "m_petrolRequired", 0);
        
        bool enoughResources = true;
        if ((UnityEngine.Object)GameModeManager.instance != (UnityEngine.Object)null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
        {
             bool sufficientBattery = Safe.GetFieldOrDefault(__instance, "m_sufficientBatteryForTrip", false);
             enoughResources = sufficientBattery;
        }
        else
        {
            enoughResources = (WaterManager.Instance.StoredWater >= waterReq) && 
                             (InventoryManager.Instance.GetNumItemsOfType(ItemManager.ItemType.Petrol) >= petrolReq);
        }

        bool isReady = anyoneSelected && hasRoute && enoughResources;

        // Override the vanilla field
        Safe.SetField(__instance, "m_isReadyToGo", isReady);

        // Force enable the button/legend if ready
        if (__instance.mapScreenConfirmButton != null)
        {
            __instance.mapScreenConfirmButton.SetEnabled(isReady);
        }
        
        if (__instance.m_mapScreenLegend != null)
        {
            __instance.m_mapScreenLegend.SetButtonEnabled(LegendContainer.ButtonEnum.XButton, isReady);
        }
    }
}

[HarmonyPatch(typeof(ExpeditionMainPanelNew), "FinaliseExpedition")]
public static class ExpeditionMainPanelNew_FinaliseExpedition_Patch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance, ref bool __result)
    {
        if (__instance == null) return true;
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        if (!Safe.TryGetField(__instance, "m_loadoutScript", out ExpeditionLoadout loadout))
            return true;
        if (!Safe.TryGetField(__instance, "m_partyId", out int partyId))
            return true;
        
        var route = __instance.route;

        int selectedCount = 0;
        for (int i = 0; i < logic.SelectedMemberIndices.Count; i++) if (logic.SelectedMemberIndices[i] != -1) selectedCount++;
        if (selectedCount == 0 || route == null || route.Count == 0)
        {
            __result = false;
            return false;
        }

        for (int i = 0; i < logic.AllPartyMembers.Count; i++)
        {
            var pm = logic.AllPartyMembers[i];
            if (pm == null) continue;
            bool isSelected = (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1);
            if (isSelected && (UnityEngine.Object)pm.person != (UnityEngine.Object)null)
            {
                pm.person.job_queue.ForceClear();
                pm.person.ai_queue.ForceClear();
                foreach (var eq in pm.GetEquippedItems())
                    InventoryManager.Instance.RemoveItemsOfType(eq.m_type, eq.m_count);
            }
            else
            {
                ExplorationManager.Instance.RemoveMemberFromParty(partyId, pm);
            }
        }

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

        float waterRequired = Safe.GetFieldOrDefault(__instance, "m_waterRequired", 0f);
        int petrolRequired = Safe.GetFieldOrDefault(__instance, "m_petrolRequired", 0);
        
        FPELog.Debug($"FinaliseExpedition: Deducting Resources. Water={waterRequired}, Petrol={petrolRequired}");

        if ((UnityEngine.Object)GameModeManager.instance != (UnityEngine.Object)null)
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

        if ((UnityEngine.Object)WaterManager.Instance != (UnityEngine.Object)null && amountWater > 0)
            WaterManager.Instance.UseWater((float)amountWater);
        if ((UnityEngine.Object)FoodManager.Instance != (UnityEngine.Object)null)
        {
            if (amountRation > 0) FoodManager.Instance.TakeRations(amountRation);
            if (amountMeat > 0) FoodManager.Instance.TakeMeat(amountMeat);
            if (amountDesperate > 0) FoodManager.Instance.TakeDesperateMeat(amountDesperate);
        }

        if ((UnityEngine.Object)TutorialManager.Instance != (UnityEngine.Object)null)
            TutorialManager.Instance.SetPopupSeen(TutorialManager.PopupType.ExpeditionReminder);

        __result = true;
        return false;
    }
}