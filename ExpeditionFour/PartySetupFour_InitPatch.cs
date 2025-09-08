using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
public static class ExpeditionMainPanelNew_OnShow_SetupPatch
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        try
        {
            if (__instance == null || __instance.PartySetup == null) return;

            var go = __instance.gameObject;

            // Ensure our driver exists
            var logic = go.GetComponent<FourPersonPartyLogic>();
            if (logic == null) logic = go.AddComponent<FourPersonPartyLogic>();
            logic.MaxPartySize = Mathf.Max(2, FourPersonConfig.MaxPartySize);

            // Try to capture a "title" label to show slot index
            if (logic.TitleLabel == null)
            {
                logic.TitleLabel = TryFindTitle(__instance);
                if (logic.TitleLabel == null && __instance.partySetupScript != null)
                    logic.TitleLabel = __instance.partySetupScript.confirmLabel;
            }

            // 1) Clone and position extra avatars
            SetupAvatars(__instance, logic);

            // 2) Ensure the party has N PartyMember components
            SetupPartyMembers(__instance, logic);

            // 3) Reset selection state and set initial highlight
            logic.ResetState();
            var elig = __instance.eligiblePeople;
            int first = (elig != null && elig.Count > 0) ? 0 : -1;
            if (first != -1) logic.HighlightedIndices[0] = first;

            // 4) Force a UI refresh
            __instance.partySetupScript?.SendMessage(
                "UpdatePage",
                SendMessageOptions.DontRequireReceiver
            );

            FPELog.Info(
                "OnShow setup complete. Slots=" + logic.MaxPartySize +
                " Eligible=" + (elig != null ? elig.Count : 0)
            );
        }
        catch (System.Exception ex)
        {
            FPELog.Warn("OnShow setup failed: " + ex.Message);
        }
    }

    private static UILabel TryFindTitle(ExpeditionMainPanelNew panel)
    {
        UILabel title = null;
        var labels = panel.gameObject.GetComponentsInChildren<UILabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            var l = labels[i];
            if (l == null || string.IsNullOrEmpty(l.name)) continue;
            var n = l.name.ToLowerInvariant();
            if (n.Contains("title") || n.Contains("header"))
            {
                title = l;
                break;
            }
        }
        return title;
    }

    private static void SetupAvatars(
    ExpeditionMainPanelNew panel,
    FourPersonPartyLogic logic
)
    {
        var setup = panel.partySetupScript;
        if (setup == null) return;

        // We no longer clone. We just get references to the two vanilla avatar slots.
        if (logic.AllMemberAvatars == null)
            logic.AllMemberAvatars = new List<ExpeditionPartySetup.MemberAvatar>();

        logic.AllMemberAvatars.Clear();
        logic.AllMemberAvatars.Add(setup.memberAvatar);   // Slot 0 in our list
        logic.AllMemberAvatars.Add(setup.memberAvatar2);  // Slot 1 in our list

        logic.isInitialized = true; // Mark as done.
        FPELog.Info("Avatar setup simplified: Using 2 vanilla slots for all members.");
    }

    private static void SetupPartyMembers(
        ExpeditionMainPanelNew panel,
        FourPersonPartyLogic logic
    )
    {
        if (logic.AllPartyMembers == null)
            logic.AllPartyMembers = new List<PartyMember>();

        // If already populated to MaxPartySize, do nothing
        if (logic.AllPartyMembers.Count >= logic.MaxPartySize) return;

        var tr = Traverse.Create(panel);
        var pm1 = tr.Field("m_partyMember1").GetValue<PartyMember>();
        var pm2 = tr.Field("m_partyMember2").GetValue<PartyMember>();
        var temp = new List<PartyMember>();
        if (pm1 != null) temp.Add(pm1);
        if (pm2 != null) temp.Add(pm2);

        int partyId = tr.Field("m_partyId").GetValue<int>();
        for (int i = temp.Count; i < logic.MaxPartySize; i++)
        {
            // Uses your patched ExplorationManager.AddMemberToParty
            var extra = ExplorationManager.Instance.AddMemberToParty(partyId);
            temp.Add(extra);
        }

        logic.AllPartyMembers.Clear();
        logic.AllPartyMembers.AddRange(temp);
    }
}

[HarmonyPatch(typeof(ExpeditionPartySetup), nameof(ExpeditionPartySetup.CancelPerson))]
public static class ExpeditionPartySetup_CancelPerson_Patch
{
    public static bool Prefix(ExpeditionPartySetup __instance, ref bool __result)
    {
        var panel = ExpeditionMainPanelNew.Instance;
        if (panel == null) return true;
        var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        if (logic.ActiveSelectionSlot > 0)
        {
            logic.ActiveSelectionSlot--;

            // Put highlight back to what was picked here, or pick first available
            int sel = logic.SelectedMemberIndices[logic.ActiveSelectionSlot];
            if (sel == -1)
            {
                var elig = panel.eligiblePeople;
                int first = -1;
                for (int i = 0; i < elig.Count; i++)
                {
                    if (!logic.IsIndexSelected(i)) { first = i; break; }
                }
                logic.HighlightedIndices[logic.ActiveSelectionSlot] = first;
            }
            else
            {
                logic.HighlightedIndices[logic.ActiveSelectionSlot] = sel;
            }

            __instance.SendMessage(
                "UpdatePage",
                SendMessageOptions.DontRequireReceiver
            );
            __result = true;     // tell the panel we handled "back"
            return false;        // skip vanilla
        }

        return true; // vanilla close when already at first slot
    }
}


[HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
public static class ExpeditionMainPanelNew_Update_Postfix
{
    public static void Postfix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return;

        // Count selections
        int selectedCount = 0;
        for (int i = 0; i < logic.SelectedMemberIndices.Count; i++)
            if (logic.SelectedMemberIndices[i] != -1) selectedCount++;

        var tr = Traverse.Create(__instance);
        bool ready = selectedCount > 0 && __instance.route.Count > 0;

        if (GameModeManager.instance != null &&
            GameModeManager.instance.currentGameMode ==
            GameModeManager.GameMode.Stasis)
        {
            bool enough =
                tr.Field("m_sufficientBatteryForTrip").GetValue<bool>();
            ready = ready && enough;
        }
        else
        {
            float waterReq = tr.Field("m_waterRequired").GetValue<float>();
            int petrolReq = tr.Field("m_petrolRequired").GetValue<int>();

            bool enoughWater =
                WaterManager.Instance != null &&
                WaterManager.Instance.StoredWater >= waterReq;

            bool enoughPetrol =
                InventoryManager.Instance.GetNumItemsOfType(
                    ItemManager.ItemType.Petrol) >= petrolReq;

            ready = ready && enoughWater && enoughPetrol;

            // Keep vanilla behavior: at least one stays home
            var elig = __instance.eligiblePeople;
            if (elig != null) ready = ready && selectedCount < elig.Count;
        }

        tr.Field("m_isReadyToGo").SetValue(ready);

        // If already on map, ensure UI matches readiness
        if (__instance.MapScreen != null &&
            __instance.MapScreen.activeInHierarchy)
        {
            if (__instance.mapScreenConfirmButton != null)
                __instance.mapScreenConfirmButton.SetEnabled(ready);

            var legend = tr.Field("m_mapScreenLegend")
                          .GetValue<LegendContainer>();
            if (legend != null)
                legend.SetButtonEnabled(
                    LegendContainer.ButtonEnum.XButton,
                    ready
                );
        }
    }
}

[HarmonyPatch(typeof(ExpeditionPartySetup), "UpdatePage")]
public static class ExpeditionPartySetup_UpdatePage_Patch
{
    private static void SetBarValue(UIProgressBar bar, float value, bool colour = true)
    {
        if (bar == null) return;
        bar.value = value;
        if (!colour || bar.foregroundWidget == null) return;

        var foregroundWidget = bar.foregroundWidget as UISprite;
        if (foregroundWidget == null) return;

        // Colors are private in the original, so we define them here.
        Color LowBarColor = new Color(0.39f, 0.58f, 0.36f);
        Color MedBarColor = new Color(0.62f, 0.54f, 0.09f);
        Color HighBarColor = new Color(0.7f, 0.17f, 0.17f);

        if (bar.value < 0.3f) foregroundWidget.color = LowBarColor;
        else if (bar.value < 0.7f) foregroundWidget.color = MedBarColor;
        else foregroundWidget.color = HighBarColor;
    }

    public static bool Prefix(ExpeditionPartySetup __instance)
    {
        var panel = ExpeditionMainPanelNew.Instance;
        if (panel == null) return true;
        var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        var tr = Traverse.Create(__instance);
        var elig = panel.eligiblePeople;
        int activeSlot = logic.ActiveSelectionSlot;

        // --- 1. Update the Main Stats Panel ---
        // It should always display the stats for the character currently being highlighted in the active slot.
        int highlightedIndex = logic.HighlightedIndices[activeSlot];
        FamilyMember highlightedPerson = null;
        if (highlightedIndex >= 0 && highlightedIndex < elig.Count)
        {
            highlightedPerson = elig[highlightedIndex];
        }
        UpdateMainStatsPanel(__instance, highlightedPerson); // This helper contains all the stat/bar updates

        // --- 2. Update the two visible avatars ---
        int firstAvatarSlotIndex = (activeSlot / 2) * 2; // This will be 0 for slots 0/1, 2 for slots 2/3, etc.
        int secondAvatarSlotIndex = firstAvatarSlotIndex + 1;

        // Populate the left avatar (vanilla memberAvatar)
        UpdateAvatarUI(logic.AllMemberAvatars[0], logic, firstAvatarSlotIndex, elig);

        // Populate the right avatar (vanilla memberAvatar2)
        if (secondAvatarSlotIndex < logic.MaxPartySize)
        {
            UpdateAvatarUI(logic.AllMemberAvatars[1], logic, secondAvatarSlotIndex, elig);
        }
        else
        {
            // If MaxPartySize is odd, hide the second slot on the last page.
            UICloneUtil.SetAvatarActive(logic.AllMemberAvatars[1], false);
        }

        // --- 3. Update Arrow Buttons and Confirm Label ---
        UpdateArrowButtons(__instance, logic, elig, highlightedIndex);
        bool isLastConfirmableSlot = activeSlot >= logic.MaxPartySize - 1 || highlightedIndex == -1;
        __instance.confirmLabel.text = Localization.Get(isLastConfirmableSlot ? "ui.map" : "ui.nextperson");

        return false; // Block original method
    }

    // New helper to populate a single avatar UI element
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

        // Highlight the single active slot
        bool active = (slotIndex == logic.ActiveSelectionSlot);
        int hi = 20, lo = 10;
        if (avatar.name != null) avatar.name.depth = active ? hi : lo;
        if (avatar.avatar != null) avatar.avatar.depth = active ? hi : lo;
        if (avatar.polaroid != null) avatar.polaroid.depth = active ? (hi - 1) : (lo - 1);
        if (avatar.background != null) avatar.background.depth = active ? (hi - 2) : (lo - 2);
    }

    // Helper for stats (full version, no placeholders)
    private static void UpdateMainStatsPanel(ExpeditionPartySetup setupInstance, FamilyMember person)
    {
        var tr = Traverse.Create(setupInstance);
        if (person != null)
        {
            setupInstance.memberCharisma.text = person.BaseStats.Charisma.Level.ToString("00");
            setupInstance.memberDexterity.text = person.BaseStats.Dexterity.Level.ToString("00");
            setupInstance.memberIntelligence.text = person.BaseStats.Intelligence.Level.ToString("00");
            setupInstance.memberPerception.text = person.BaseStats.Perception.Level.ToString("00");
            setupInstance.memberStrength.text = person.BaseStats.Strength.Level.ToString("00");

            SetBarValue(tr.Field("strengthBar").GetValue<UIProgressBar>(), person.BaseStats.Strength.NormalizedExp, false);
            SetBarValue(tr.Field("dexterityBar").GetValue<UIProgressBar>(), person.BaseStats.Dexterity.NormalizedExp, false);
            SetBarValue(tr.Field("perceptionBar").GetValue<UIProgressBar>(), person.BaseStats.Perception.NormalizedExp, false);
            SetBarValue(tr.Field("intelligenceBar").GetValue<UIProgressBar>(), person.BaseStats.Intelligence.NormalizedExp, false);
            SetBarValue(tr.Field("charismaBar").GetValue<UIProgressBar>(), person.BaseStats.Charisma.NormalizedExp, false);

            setupInstance.healthLabel.text = person.health.ToString() + "/" + person.maxHealth.ToString();
            setupInstance.statusLabel.text = person.GetLocalizedStatusText();
            setupInstance.illnessLabel.text = person.illness.ToString();

            SetBarValue(setupInstance.hungerBar, person.stats.hunger.NormalizedValue);
            SetBarValue(setupInstance.thirstBar, person.stats.thirst.NormalizedValue);
            SetBarValue(setupInstance.tirednessBar, person.stats.fatigue.NormalizedValue);
            SetBarValue(setupInstance.bathroomBar, person.stats.toilet.NormalizedValue);
            SetBarValue(setupInstance.hygieneBar, person.stats.dirtiness.NormalizedValue);
            SetBarValue(setupInstance.stressBar, person.stats.stress.NormalizedValue);
            SetBarValue(setupInstance.traumaBar, person.stats.trauma.NormalizedValue);

            var loyaltyBar = tr.Field("loyaltyBar").GetValue<UIProgressBar>();
            if (loyaltyBar != null) { SetBarValue(loyaltyBar, person.stats.loyalty.NormalizedValue, false); loyaltyBar.gameObject.SetActive(person.loyalty != FamilyMember.LoyaltyEnum.Loyal); }
            if (setupInstance.traumaBar != null) { setupInstance.traumaBar.gameObject.SetActive(person.loyalty == FamilyMember.LoyaltyEnum.Loyal); }

            var stringList = new List<string>();
            stringList.AddRange(person.traits.GetLocalizedStrengthNames(true));
            stringList.AddRange(person.traits.GetLocalizedWeaknessNames(true));
            setupInstance.memberTraits.text = string.Join(", ", stringList.ToArray());
        }
        else
        {
            // Clear all stats
            setupInstance.statusLabel.text = string.Empty;
            setupInstance.healthLabel.text = string.Empty;
            setupInstance.illnessLabel.text = string.Empty;
            setupInstance.memberCharisma.text = string.Empty;
            setupInstance.memberDexterity.text = string.Empty;
            setupInstance.memberIntelligence.text = string.Empty;
            setupInstance.memberPerception.text = string.Empty;
            setupInstance.memberStrength.text = string.Empty;
            setupInstance.memberTraits.text = string.Empty;

            SetBarValue(tr.Field("strengthBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("dexterityBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("perceptionBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("intelligenceBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("charismaBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(setupInstance.hungerBar, 0f);
            SetBarValue(setupInstance.thirstBar, 0f);
            SetBarValue(setupInstance.tirednessBar, 0f);
            SetBarValue(setupInstance.bathroomBar, 0f);
            SetBarValue(setupInstance.hygieneBar, 0f);
            SetBarValue(setupInstance.stressBar, 0f);
            SetBarValue(setupInstance.traumaBar, 0f);
            SetBarValue(tr.Field("loyaltyBar").GetValue<UIProgressBar>(), 0f, false);
        }
    }

    // Helper for arrows
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


