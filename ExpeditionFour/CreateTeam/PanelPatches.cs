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

        int activeSlot = logic.NormalizeActiveSelectionSlot();
        int index = logic.HighlightedIndices[activeSlot];
        var eligiblePeople = __instance.eligiblePeople;
        if (eligiblePeople == null)
        {
            FPELog.Warn("[FPE] OnSelect rejected because the eligible character list is unavailable.");
            return false;
        }

        if (index < -1 || index >= eligiblePeople.Count ||
            (index >= 0 && logic.IsIndexSelected(index)))
        {
            FPELog.Warn($"[FPE] OnSelect rejected invalid or duplicate character index {index} for slot {activeSlot}.");
            return false;
        }
        
        // Temporarily store selection for validation
        var oldSelections = new List<int>(logic.SelectedMemberIndices);
        logic.SelectedMemberIndices[activeSlot] = index;
        if (index == -1)
        {
            for (int i = activeSlot + 1; i < logic.SelectedMemberIndices.Count; i++)
                logic.SelectedMemberIndices[i] = -1;
        }

        // Perform Validation Checks (Empty Shelter, Loyalty, Food Poisoning)
        if (ValidateSelection(__instance, logic))
        {
            // Validation failed, restore previous selection and stay on page
            for (int i = 0; i < oldSelections.Count; i++)
                logic.SelectedMemberIndices[i] = oldSelections[i];
            return false;
        }

        if (index == -1)
        {
            for (int i = activeSlot + 1; i < logic.HighlightedIndices.Count; i++)
                logic.HighlightedIndices[i] = -1;
        }

        Safe.SetField(__instance, "m_routeIsDirty", true);

        FPELog.Debug($"OnSelect Patch: Stored selection. Slot: {activeSlot}, Character Index: {index}.");

        if (index != -1 && activeSlot < logic.MaxPartySize - 1)
        {
            FPELog.Debug("OnSelect Patch: Advancing to next party slot.");
            logic.ActiveSelectionSlot = activeSlot + 1;
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
        if (FpeRuntimeGuards.IsStasisMode())
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
        if (elig == null) return true;
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
        if (elig == null || ExplorationManager.Instance == null) return true;
        float disloyalNeedThreshold = ExplorationManager.Instance.disloyalNeedThreshold;
        bool isVeryDisloyal = false;
        bool hasHighNeedsDisloyal = false;

        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            if (person == null) continue;
            
            if (!person.isLoyal)
            {
                if (person.loyalty <= FamilyMember.LoyaltyEnum.Cautious)
                    isVeryDisloyal = true;
                
                BehaviourStats stats = person.stats;
                if (stats == null) continue;
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
        if (elig == null) return true;
        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            if (FpeRuntimeGuards.IsFoodPoisoned(person))
            {
                MessageBox.Show(MessageBoxButtons.Okay_Button, "UI.FoodPoisonWarning");
                return true;
            }
        }
        return false;
    }

    private static bool CheckHazmat4(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        Obj_HazmatSuit_Stasis hazmatScript;
        if (!FpeRuntimeGuards.TryGetStasisHazmatSuit(out hazmatScript))
        {
            MessageBox.Show(MessageBoxButtons.Okay_Button, "Text.UI.NoHazmatWarning");
            return false;
        }

        var elig = panel.eligiblePeople;
        if (elig == null) return false;
        foreach (int selIndex in logic.SelectedMemberIndices)
        {
            if (selIndex < 0 || selIndex >= elig.Count) continue;
            var person = elig[selIndex];
            if (person == null) continue;
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

        if (!PartyMemberSlotSynchronizer.EnsureSlots(__instance, logic))
        {
            FPELog.Warn("[FPE] UpdatePartyMembers: Could not provision the selected party slots.");
            return true;
        }

        FPELog.Debug("UpdatePartyMembers Patch: Assigning selected family members to the party.");
        int max = Mathf.Min(logic.MaxPartySize, logic.AllPartyMembers.Count);
        var elig = __instance.eligiblePeople;
        if (elig == null) return true;
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

        if (FpeRuntimeGuards.IsStasisMode())
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
        PartyMemberSlotSynchronizer.SyncVanillaMemberFields(__instance, logic);
        return false;
    }
}




[HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
public static class ExpeditionMainPanelNew_Update_Patch
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null || __instance.MapScreen == null || !__instance.MapScreen.activeInHierarchy) return;

        var logic = __instance.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return;

        var elig = __instance.eligiblePeople;

        // 1. Check if at least one valid person is selected
        bool anyoneSelected = false;
        foreach (int idx in logic.SelectedMemberIndices)
        {
            if (idx >= 0 && elig != null && idx < elig.Count)
            {
                anyoneSelected = true;
                break;
            }
        }

        // 2. Check route
        var route = __instance.route;
        bool hasRoute = route != null && route.Count > 0;

        // 3. Check Resources
        bool enoughResources = ExpeditionFourPatches.HasSufficientLaunchResources(__instance);

        bool allSelectedCanMove = true;
        if (anyoneSelected)
        {
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
