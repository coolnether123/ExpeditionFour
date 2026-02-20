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
        
        // Temporarily store selection for validation
        int oldSelection = logic.SelectedMemberIndices[logic.ActiveSelectionSlot];
        logic.SelectedMemberIndices[logic.ActiveSelectionSlot] = index;

        // Perform Validation Checks (Empty Shelter, Loyalty, Food Poisoning)
        if (ValidateSelection(__instance, logic))
        {
            // Validation failed, restore previous selection and stay on page
            logic.SelectedMemberIndices[logic.ActiveSelectionSlot] = oldSelection;
            return false;
        }

        FPELog.Debug($"OnSelect Patch: Stored selection. Slot: {logic.ActiveSelectionSlot}, Character Index: {index}.");

        if (index != -1 && logic.ActiveSelectionSlot < logic.MaxPartySize - 1)
        {
            FPELog.Debug("OnSelect Patch: Advancing to next party slot.");
            logic.ActiveSelectionSlot++;
            logic.HighlightedIndices[logic.ActiveSelectionSlot] = -1; // Force user to pick

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

    private static bool ValidateSelection(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        // 1. Loyalty Check
        if (CheckLoyalty4(panel, logic)) return true;
        
        // 2. Food Poisoning Check
        if (CheckFoodPoisoning4(panel, logic)) return true;
        
        // 3. Empty Shelter Check
        if (CheckEmptyShelter4(panel, logic)) return true;

        // 4. Hazmat Check (Stasis mode)
        if ((Object)GameModeManager.instance != (Object)null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
        {
            if (!CheckHazmat4(panel, logic)) return true;
        }
        
        return false;
    }

    private static bool CheckEmptyShelter4(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        List<FamilyMember> stayingBehind = new List<FamilyMember>();
        if (FamilyManager.Instance != null)
            stayingBehind.AddRange(FamilyManager.Instance.GetAllFamilyMembers());

        var elig = panel.eligiblePeople;
        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex >= 0 && selIndex < elig.Count)
                stayingBehind.Remove(elig[selIndex]);
        }

        int count = 0;
        foreach (var member in stayingBehind)
        {
            if (member != null && !member.isDead && !member.isCatatonic && !member.isUncontrollable && !member.isAway && !member.IsUnconscious)
                count++;
        }

        if (count <= 0)
        {
            MessageBox.Show(MessageBoxButtons.Okay_Button, "UI.ExpeditionPartyEmptyWarning");
            return true;
        }
        return false;
    }

    private static bool CheckLoyalty4(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        var elig = panel.eligiblePeople;
        float disloyalNeedThreshold = ExplorationManager.Instance.disloyalNeedThreshold;
        bool isVeryDisloyal = false;
        bool hasHighNeedsDisloyal = false;

        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            
            if (!person.isLoyal)
            {
                if (person.loyalty <= FamilyMember.LoyaltyEnum.Cautious)
                    isVeryDisloyal = true;
                
                BehaviourStats stats = person.stats;
                if (stats.hunger.NormalizedValue >= disloyalNeedThreshold || 
                    stats.thirst.NormalizedValue >= disloyalNeedThreshold || 
                    stats.fatigue.NormalizedValue >= disloyalNeedThreshold || 
                    stats.toilet.NormalizedValue >= disloyalNeedThreshold || 
                    stats.dirtiness.NormalizedValue >= disloyalNeedThreshold || 
                    stats.stress.NormalizedValue >= disloyalNeedThreshold)
                    hasHighNeedsDisloyal = true;
            }
        }

        if (isVeryDisloyal)
            MessageBox.Show(MessageBoxButtons.Okay_Button, "UI.ExpeditionPartyDisloyalWarning");
        else if (hasHighNeedsDisloyal)
            MessageBox.Show(MessageBoxButtons.Okay_Button, "UI.ExpeditionPartyDisloyalWarning.Stats");

        return isVeryDisloyal || hasHighNeedsDisloyal;
    }

    private static bool CheckFoodPoisoning4(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        var elig = panel.eligiblePeople;
        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            if (person.illness != null && person.illness.foodPoisoning.isActive)
            {
                MessageBox.Show(MessageBoxButtons.Okay_Button, "UI.FoodPoisonWarning");
                return true;
            }
        }
        return false;
    }

    private static bool CheckHazmat4(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        var hazmatScript = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.HazmatSuits_Stasis)[0] as Obj_HazmatSuit_Stasis;
        if (hazmatScript == null) return true;

        var elig = panel.eligiblePeople;
        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            int suitIdx = -1;
            switch (person.firstName)
            {
                case "Gregory": suitIdx = 0; break;
                case "Nancy":   suitIdx = 1; break;
                case "Mark":    suitIdx = 2; break;
                case "Lucy":    suitIdx = 3; break;
            }
            
            if (suitIdx != -1)
            {
                bool hasSuit = person.HazmatNumber != -1 || hazmatScript.IsSuitAvailable(suitIdx);
                if (!hasSuit)
                {
                    MessageBox.Show(MessageBoxButtons.Okay_Button, "Text.UI.NoHazmatWarning");
                    return false;
                }
            }
        }
        return true;
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

        EnsurePartyMemberSlots(__instance, logic);

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

    private static void EnsurePartyMemberSlots(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        if (panel == null || logic == null || ExplorationManager.Instance == null) return;
        if (!Safe.TryGetField(panel, "m_partyId", out int partyId)) return;

        var party = ExplorationManager.Instance.GetParty(partyId);
        if (party == null) return;

        int requiredSlots = 0;
        for (int i = 0; i < logic.MaxPartySize && i < logic.SelectedMemberIndices.Count; i++)
        {
            if (logic.SelectedMemberIndices[i] != -1)
                requiredSlots = i + 1;
        }

        requiredSlots = Mathf.Max(requiredSlots, 2);

        var components = party.GetComponents<PartyMember>();
        for (int i = components.Length; i < requiredSlots; i++)
        {
            var added = ExplorationManager.Instance.AddMemberToParty(partyId);
            if (added == null)
            {
                FPELog.Warn($"[FPE] UpdatePartyMembers: Failed to add slot {i} for party {partyId}.");
                break;
            }
        }

        components = party.GetComponents<PartyMember>();
        logic.AllPartyMembers.Clear();
        logic.AllPartyMembers.AddRange(components);
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

        bool allSelectedCanMove = true;
        if (anyoneSelected)
        {
            var elig = __instance.eligiblePeople;
            if (elig == null)
            {
                allSelectedCanMove = false;
            }
            else
            {
                foreach (int idx in logic.SelectedMemberIndices)
                {
                    if (idx < 0 || idx >= elig.Count) continue;
                    var member = elig[idx];
                    if (member != null && member.GetWalkSpeed() <= 0f)
                    {
                        allSelectedCanMove = false;
                        break;
                    }
                }
            }
        }

        bool isReady = anyoneSelected && hasRoute && enoughResources && allSelectedCanMove;

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
