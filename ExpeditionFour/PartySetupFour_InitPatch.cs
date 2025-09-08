using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// This patch simplifies avatar setup. We no longer clone anything.
[HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
public static class ExpeditionMainPanelNew_OnShow_SetupPatch
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>()
                    ?? __instance.gameObject.AddComponent<FourPersonPartyLogic>();
        logic.MaxPartySize = FourPersonConfig.MaxPartySize;

        // --- Correctly Synchronize Party Members ---
        FPELog.Info("OnShow Postfix: Synchronizing PartyMember list...");
        var tr = Traverse.Create(__instance);
        int partyId = tr.Field("m_partyId").GetValue<int>();
        var party = ExplorationManager.Instance.GetParty(partyId);

        if (party != null)
        {
            // First, ensure the game has enough PartyMember components attached.
            int currentMembers = party.GetComponents<PartyMember>().Length;
            for (int i = currentMembers; i < logic.MaxPartySize; i++)
            {
                ExplorationManager.Instance.AddMemberToParty(partyId);
            }

            // Now, get ALL PartyMember components from the party object. This is our source of truth.
            logic.AllPartyMembers.Clear();
            logic.AllPartyMembers.AddRange(party.GetComponents<PartyMember>());
            FPELog.Info($"Synchronization complete. Found {logic.AllPartyMembers.Count} PartyMember components.");
        }
        else
        {
            FPELog.Warn("Could not find ExplorationParty object to synchronize members!");
        }


        if (!logic.isInitialized)
        {
            var setup = __instance.partySetupScript;
            if (setup != null)
            {
                logic.AllMemberAvatars.Clear();
                logic.AllMemberAvatars.Add(setup.memberAvatar);
                logic.AllMemberAvatars.Add(setup.memberAvatar2);
                logic.isInitialized = true;
                FPELog.Info("Avatar UI initialized: Using 2 vanilla slots.");
            }
        }

        logic.ResetState();
        var elig = __instance.eligiblePeople;
        logic.HighlightedIndices[0] = (elig != null && elig.Count > 0) ? 0 : -1;

        __instance.partySetupScript?.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
    }
}


// This is the key fix. We use a POSTFIX to correct the UI after vanilla runs.
[HarmonyPatch(typeof(ExpeditionPartySetup), "UpdatePage")]
public static class ExpeditionPartySetup_UpdatePage_Patch
{
    public static void Postfix(ExpeditionPartySetup __instance)
    {
        var panel = ExpeditionMainPanelNew.Instance;
        if (panel == null) return;
        var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null || !logic.isInitialized) return;

        var elig = panel.eligiblePeople;
        int activeSlot = logic.ActiveSelectionSlot;

        // --- 1. Determine which pair of slots is visible ---
        int firstVisibleSlot = (activeSlot / 2) * 2;
        int secondVisibleSlot = firstVisibleSlot + 1;

        // --- 2. Correct the LEFT avatar (memberAvatar) ---
        var leftAvatar = logic.AllMemberAvatars[0];
        UpdateAvatarUI(leftAvatar, logic, firstVisibleSlot, elig);

        // --- 3. Correct the RIGHT avatar (memberAvatar2) ---
        var rightAvatar = logic.AllMemberAvatars[1];
        if (secondVisibleSlot < logic.MaxPartySize)
        {
            UpdateAvatarUI(rightAvatar, logic, secondVisibleSlot, elig);
        }
        else
        {
            UICloneUtil.SetAvatarActive(rightAvatar, false);
        }

        // The vanilla method has already updated the stats for the highlighted character.
        // We don't need to do it again unless it's for a slot vanilla doesn't know about.
        // The `Next/PreviousMember` patches already handle this by writing to vanilla fields.

        // We DO need to correct the arrow buttons and confirm label, as vanilla logic is wrong for our case.
        int highlightedIndex = logic.HighlightedIndices[activeSlot];
        UpdateArrowButtons(__instance, logic, elig, highlightedIndex);

        bool isLastConfirmableSlot = activeSlot >= logic.MaxPartySize - 1 || highlightedIndex == -1;
        if (__instance.confirmLabel != null)
            __instance.confirmLabel.text = Localization.Get(isLastConfirmableSlot ? "ui.map" : "ui.nextperson");
    }

    private static void UpdateAvatarUI(ExpeditionPartySetup.MemberAvatar avatar, FourPersonPartyLogic logic, int slotIndex, IList<FamilyMember> elig)
    {
        UICloneUtil.SetAvatarActive(avatar, true);

        int characterIndexToShow = (slotIndex == logic.ActiveSelectionSlot)
            ? logic.HighlightedIndices[slotIndex]
            : logic.SelectedMemberIndices[slotIndex];

        if (characterIndexToShow >= 0 && characterIndexToShow < elig.Count)
        {
            var p = elig[characterIndexToShow];
            if (avatar.avatar != null) p.ColorizeAvatarSprite(avatar.avatar);
            if (avatar.name != null) avatar.name.text = p.firstName;
        }
        else
        {
            if (avatar.avatar != null) avatar.avatar.sprite2D = null;
            if (avatar.name != null) avatar.name.text = Localization.Get("Text.Name.Nobody");
        }

        bool active = (slotIndex == logic.ActiveSelectionSlot);
        int hi = 20, lo = 10;
        if (avatar.name != null) avatar.name.depth = active ? hi : lo;
        if (avatar.avatar != null) avatar.avatar.depth = active ? hi : lo;
        if (avatar.polaroid != null) avatar.polaroid.depth = active ? (hi - 1) : (lo - 1);
        if (avatar.background != null) avatar.background.depth = active ? (hi - 2) : (lo - 2);
    }

    private static void UpdateArrowButtons(ExpeditionPartySetup setupInstance, FourPersonPartyLogic logic, IList<FamilyMember> elig, int highlightedIndex)
    {
        bool canGoNext = false;
        for (int i = 1; i <= elig.Count + 1; i++)
        {
            int nextIdx = (highlightedIndex + i) % (elig.Count + 1);
            if (nextIdx == elig.Count) nextIdx = -1;
            if (!logic.IsIndexSelected(nextIdx))
            {
                canGoNext = true;
                break;
            }
        }
        if (setupInstance.memberRightArrow != null) setupInstance.memberRightArrow.GetComponent<UIButton>().isEnabled = canGoNext;

        bool canGoPrev = false;
        for (int i = 1; i <= elig.Count + 1; i++)
        {
            int prevIdx = (highlightedIndex - i + elig.Count + 1) % (elig.Count + 1);
            if (prevIdx == elig.Count) prevIdx = -1;
            if (!logic.IsIndexSelected(prevIdx))
            {
                canGoPrev = true;
                break;
            }
        }
        if (setupInstance.memberLeftArrow != null) setupInstance.memberLeftArrow.GetComponent<UIButton>().isEnabled = canGoPrev;
    }
}