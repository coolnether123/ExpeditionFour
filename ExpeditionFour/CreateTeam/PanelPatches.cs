using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;



[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnSelect))]
public static class ExpeditionMainPanelNew_OnSelect_Patch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null || __instance.PartySetup == null || !__instance.PartySetup.activeInHierarchy) return true;

        int index = logic.HighlightedIndices[logic.ActiveSelectionSlot];
        logic.SelectedMemberIndices[logic.ActiveSelectionSlot] = index;
        FPELog.Info($"OnSelect Patch: Stored selection. Slot: {logic.ActiveSelectionSlot}, Character Index: {index}.");

        if (index != -1 && logic.ActiveSelectionSlot < logic.MaxPartySize - 1)
        {
            FPELog.Info("OnSelect Patch: Advancing to next party slot.");
            logic.ActiveSelectionSlot++;

            // Find the first available character to be the default highlight for the new slot.
            int firstAvailable = -1;
            for (int i = 0; i < __instance.eligiblePeople.Count; i++)
            {
                if (!logic.IsIndexSelected(i))
                {
                    firstAvailable = i;
                    break;
                }
            }
            logic.HighlightedIndices[logic.ActiveSelectionSlot] = firstAvailable;

            __instance.partySetupScript?.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
            return false;
        }
        else
        {
            FPELog.Info("OnSelect Patch: Finalizing selections and showing map.");
            Traverse.Create(__instance).Method("UpdatePartyMembers").GetValue();
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

        FPELog.Info("UpdatePartyMembers Patch: Assigning selected family members to the party.");
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
                FPELog.Info($"UpdatePartyMembers Patch: Assigning '{famName}' to party slot {i}.");
                if (fam != null)
                {
                    MMLogger.Log($"Character Selected: {fam.firstName} was added to the expedition.");
                }
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


[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnExtra1))]
public static class ExpeditionMainPanelNew_OnExtra1_Loadout_Prefix
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null) return true;
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        var tr = Traverse.Create(__instance);
        var page = tr.Field("m_page").GetValue();
        var loadout = tr.Field("m_loadoutScript").GetValue<ExpeditionLoadout>();
        string pageName = page.ToString();
        if (pageName == "LoadoutMember1" || pageName == "LoadoutMember2")
        {
            int current = Mathf.Max(0, logic.ActiveLoadoutIndex);
            int next = -1;
            for (int i = current + 1; i < logic.MaxPartySize; i++)
            {
                if (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1)
                {
                    next = i; break;
                }
            }

            if (next != -1)
            {
                logic.ActiveLoadoutIndex = next;
                var pm = (next < logic.AllPartyMembers.Count) ? logic.AllPartyMembers[next] : null;
                if (pm != null && loadout != null)
                {
                    loadout.InitializeLoadout(pm, false);
                    bool hasAnother = false;
                    for (int j = next + 1; j < logic.MaxPartySize; j++)
                    {
                        if (j < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[j] != -1) { hasAnother = true; break; }
                    }
                    loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));
                }
                return false;
            }
            else
            {
                var confirm = tr.Method("ConfirmExpeditionSettings");
                try { confirm.GetValue(); } catch { }
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnExtra1))]
public static class ExpeditionMainPanelNew_OnExtra1_Loadout_Postfix
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null) return;
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return;
        var tr = Traverse.Create(__instance);
        var page = tr.Field("m_page").GetValue();
        var loadout = tr.Field("m_loadoutScript").GetValue<ExpeditionLoadout>();
        string pageName = page.ToString();
        if (pageName == "LoadoutMember1")
        {
            int first = -1;
            for (int i = 0; i < logic.MaxPartySize; i++)
            {
                if (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1) { first = i; break; }
            }
            if (first == -1)
            {
                var confirm = tr.Method("ConfirmExpeditionSettings");
                try { confirm.GetValue(); } catch { }
                return;
            }
            logic.ActiveLoadoutIndex = first;
            var pm = (first < logic.AllPartyMembers.Count) ? logic.AllPartyMembers[first] : null;
            if (pm != null && loadout != null)
            {
                loadout.InitializeLoadout(pm, false);
                bool hasAnother = false;
                for (int j = first + 1; j < logic.MaxPartySize; j++)
                {
                    if (j < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[j] != -1) { hasAnother = true; break; }
                }
                loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));
            }
        }
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

        var tr = Traverse.Create(__instance);
        float routeDistance = tr.Field("m_routeDistance").GetValue<float>();
        float waterRequired = 0f;
        int petrolRequired = 0;

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

            bool useVehicle = tr.Field("useVehicle").GetValue<bool>();
            bool useHorse = tr.Field("useHorse").GetValue<bool>();
            var vehicle = tr.Field("m_vehicle").GetValue<Obj_CamperVan>();
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

        tr.Field("m_waterRequired").SetValue(waterRequired);
        tr.Field("m_petrolRequired").SetValue(petrolRequired);

        if ((UnityEngine.Object)GameModeManager.instance == (UnityEngine.Object)null || GameModeManager.instance.currentGameMode != GameModeManager.GameMode.Stasis)
        {
            if (__instance.waterRequiredLabel != null && (UnityEngine.Object)WaterManager.Instance != (UnityEngine.Object)null)
                __instance.waterRequiredLabel.text = Mathf.Ceil(waterRequired).ToString("N0") + "/" + WaterManager.Instance.StoredWater.ToString("N0");
            if (__instance.petrolRequiredLabel != null)
                __instance.petrolRequiredLabel.text = Mathf.Ceil((float)petrolRequired).ToString("N0") + "/" + InventoryManager.Instance.GetNumItemsOfType(ItemManager.ItemType.Petrol).ToString("N0");
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

        var tr = Traverse.Create(__instance);
        var loadout = tr.Field("m_loadoutScript").GetValue<ExpeditionLoadout>();
        int partyId = tr.Field("m_partyId").GetValue<int>();
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

        float waterRequired = tr.Field("m_waterRequired").GetValue<float>();
        int petrolRequired = tr.Field("m_petrolRequired").GetValue<int>();
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