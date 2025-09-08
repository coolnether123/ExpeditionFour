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

        // Ensure our storage
        if (logic.AllMemberAvatars == null)
            logic.AllMemberAvatars = new List<ExpeditionPartySetup.MemberAvatar>();

        // Two anchor avatars from the vanilla UI
        var av1 = setup.memberAvatar;   // anchor A (left)
        var av2 = setup.memberAvatar2;  // anchor B (right)

        if (av1 == null || av2 == null) return;

        // Anchor positions
        var bg1 = av1.background != null ? av1.background.transform : null;
        var bg2 = av2.background != null ? av2.background.transform : null;
        if (bg1 == null || bg2 == null) return;

        // Parent to place new clones under (same as originals)
        var parent = bg1.parent != null ? bg1.parent : setup.transform;

        // Ensure list size by creating/cloning as needed
        // Slot 0 and 1 are the originals
        var slotsNeeded = Mathf.Max(2, logic.MaxPartySize);

        // Seed the list with the two anchors at indices 0 and 1
        if (logic.AllMemberAvatars.Count == 0)
        {
            logic.AllMemberAvatars.Add(av1);
            logic.AllMemberAvatars.Add(av2);
        }
        else
        {
            // Make sure index 0/1 always reference the real anchors
            if (logic.AllMemberAvatars.Count > 0) logic.AllMemberAvatars[0] = av1;
            else logic.AllMemberAvatars.Add(av1);

            if (logic.AllMemberAvatars.Count > 1) logic.AllMemberAvatars[1] = av2;
            else logic.AllMemberAvatars.Add(av2);
        }

        // Create clones for indices >= 2 if missing
        for (int i = 2; i < slotsNeeded; i++)
        {
            if (i >= logic.AllMemberAvatars.Count || logic.AllMemberAvatars[i] == null)
            {
                var clone = UICloneUtil.CloneAvatar(av2, parent);
                // Optional clearer names
                if (clone.background != null)
                    clone.background.gameObject.name = $"MemberAvatar{i + 1}_BG";
                if (clone.polaroid != null)
                    clone.polaroid.gameObject.name = $"MemberAvatar{i + 1}_Polaroid";
                if (clone.avatar != null)
                    clone.avatar.gameObject.name = $"MemberAvatar{i + 1}_Avatar";
                if (clone.name != null)
                    clone.name.gameObject.name = $"MemberAvatar{i + 1}_Name";

                if (i < logic.AllMemberAvatars.Count)
                    logic.AllMemberAvatars[i] = clone;
                else
                    logic.AllMemberAvatars.Add(clone);
            }
        }

        // The positions of the first two avatars are fixed, as they are the vanilla objects.
        // We only need to reposition the new clones we created.
        Vector3 posB = bg2.localPosition; // Position of the second avatar

        for (int i = 2; i < slotsNeeded; i++)
        {
            var avatarToMove = logic.AllMemberAvatars[i];
            if (avatarToMove == null) continue;

            // Calculate the target position: start at the second avatar's position
            // and step forward for each additional slot.
            // (i - 1) means avatar 3 is offset by 1 step, avatar 4 by 2 steps, etc.
            Vector3 targetPosition = posB + (logic.AvatarStepOffset * (i - 1));

            // Use the second avatar as the reference for offsetting all its child components correctly.
            UICloneUtil.OffsetAvatar(avatarToMove, targetPosition, av2);
        }

        // Ensure all avatars are active/visible up to the max size.
        for (int i = 0; i < slotsNeeded; i++)
        {
            UICloneUtil.SetAvatarActive(logic.AllMemberAvatars[i], true);
        }

        // Deactivate any extra, unused avatar slots if MaxPartySize was lowered.
        for (int i = slotsNeeded; i < logic.AllMemberAvatars.Count; i++)
        {
            UICloneUtil.SetAvatarActive(logic.AllMemberAvatars[i], false);
        }

        // If list was longer than needed (e.g., MaxPartySize decreased), deactivate extras.
        for (int i = slotsNeeded; i < logic.AllMemberAvatars.Count; i++)
        {
            var avatar = logic.AllMemberAvatars[i];
            UICloneUtil.SetAvatarActive(avatar, false);
            if (avatar.name != null)
            {
                avatar.name.gameObject.SetActive(false);
            }
        }
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
    // Helper method to set a progress bar's value and color.
    // This is copied from the original ExpeditionPartySetup class to be used here.
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
        int highlightedIndex = logic.HighlightedIndices[activeSlot];

        FamilyMember person = null;
        if (highlightedIndex >= 0 && highlightedIndex < elig.Count)
        {
            person = elig[highlightedIndex];
        }

        // Determine which avatar pair to show (0&1, 2&3, etc.)
        int visiblePairIndex = activeSlot / 2;

        // Show/hide and populate the correct avatar pairs
        for (int i = 0; i < logic.AllMemberAvatars.Count; i++)
        {
            if (i >= logic.MaxPartySize)
            {
                UICloneUtil.SetAvatarActive(logic.AllMemberAvatars[i], false);
                continue;
            }

            var avatar = logic.AllMemberAvatars[i];
            bool shouldBeVisible = (i / 2) == visiblePairIndex;
            UICloneUtil.SetAvatarActive(avatar, shouldBeVisible);
            if (!shouldBeVisible) continue;

            int characterIndexToShow = (i == activeSlot)
                ? logic.HighlightedIndices[i]
                : logic.SelectedMemberIndices[i];

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
        }

        // Update MAIN display with the highlighted character's info
        if (person != null)
        {
            // --- FULL STATS AND LABELS UPDATE ---
            __instance.memberCharisma.text = person.BaseStats.Charisma.Level.ToString("00");
            __instance.memberDexterity.text = person.BaseStats.Dexterity.Level.ToString("00");
            __instance.memberIntelligence.text = person.BaseStats.Intelligence.Level.ToString("00");
            __instance.memberPerception.text = person.BaseStats.Perception.Level.ToString("00");
            __instance.memberStrength.text = person.BaseStats.Strength.Level.ToString("00");

            SetBarValue(tr.Field("strengthBar").GetValue<UIProgressBar>(), person.BaseStats.Strength.NormalizedExp, false);
            SetBarValue(tr.Field("dexterityBar").GetValue<UIProgressBar>(), person.BaseStats.Dexterity.NormalizedExp, false);
            SetBarValue(tr.Field("perceptionBar").GetValue<UIProgressBar>(), person.BaseStats.Perception.NormalizedExp, false);
            SetBarValue(tr.Field("intelligenceBar").GetValue<UIProgressBar>(), person.BaseStats.Intelligence.NormalizedExp, false);
            SetBarValue(tr.Field("charismaBar").GetValue<UIProgressBar>(), person.BaseStats.Charisma.NormalizedExp, false);

            __instance.healthLabel.text = person.health.ToString() + "/" + person.maxHealth.ToString();
            __instance.statusLabel.text = person.GetLocalizedStatusText();
            __instance.illnessLabel.text = person.illness.ToString();

            SetBarValue(__instance.hungerBar, person.stats.hunger.NormalizedValue);
            SetBarValue(__instance.thirstBar, person.stats.thirst.NormalizedValue);
            SetBarValue(__instance.tirednessBar, person.stats.fatigue.NormalizedValue);
            SetBarValue(__instance.bathroomBar, person.stats.toilet.NormalizedValue);
            SetBarValue(__instance.hygieneBar, person.stats.dirtiness.NormalizedValue);
            SetBarValue(__instance.stressBar, person.stats.stress.NormalizedValue);
            SetBarValue(__instance.traumaBar, person.stats.trauma.NormalizedValue);

            var loyaltyBar = tr.Field("loyaltyBar").GetValue<UIProgressBar>();
            if (loyaltyBar != null)
            {
                SetBarValue(loyaltyBar, person.stats.loyalty.NormalizedValue, false);
                loyaltyBar.gameObject.SetActive(person.loyalty != FamilyMember.LoyaltyEnum.Loyal);
            }
            if (__instance.traumaBar != null)
            {
                __instance.traumaBar.gameObject.SetActive(person.loyalty == FamilyMember.LoyaltyEnum.Loyal);
            }

            var stringList = new List<string>();
            stringList.AddRange(person.traits.GetLocalizedStrengthNames(true));
            stringList.AddRange(person.traits.GetLocalizedWeaknessNames(true));
            __instance.memberTraits.text = string.Join(", ", stringList.ToArray());
        }
        else
        {
            // Clear all stats and labels if "Nobody" is selected
            __instance.statusLabel.text = string.Empty;
            __instance.healthLabel.text = string.Empty;
            __instance.illnessLabel.text = string.Empty;
            __instance.memberCharisma.text = string.Empty;
            __instance.memberDexterity.text = string.Empty;
            __instance.memberIntelligence.text = string.Empty;
            __instance.memberPerception.text = string.Empty;
            __instance.memberStrength.text = string.Empty;
            __instance.memberTraits.text = string.Empty;

            SetBarValue(tr.Field("strengthBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("dexterityBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("perceptionBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("intelligenceBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(tr.Field("charismaBar").GetValue<UIProgressBar>(), 0f, false);
            SetBarValue(__instance.hungerBar, 0f);
            SetBarValue(__instance.thirstBar, 0f);
            SetBarValue(__instance.tirednessBar, 0f);
            SetBarValue(__instance.bathroomBar, 0f);
            SetBarValue(__instance.hygieneBar, 0f);
            SetBarValue(__instance.stressBar, 0f);
            SetBarValue(__instance.traumaBar, 0f);
            SetBarValue(tr.Field("loyaltyBar").GetValue<UIProgressBar>(), 0f, false);
        }

        // --- ARROW LOGIC ---
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
        if (__instance.memberRightArrow != null)
            __instance.memberRightArrow.GetComponent<UIButton>().isEnabled = canGoNext;

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
        if (__instance.memberLeftArrow != null)
            __instance.memberLeftArrow.GetComponent<UIButton>().isEnabled = canGoPrev;

        // --- FULL HIGHLIGHT LOGIC ---
        for (int i = 0; i < logic.AllMemberAvatars.Count; i++)
        {
            if (i >= logic.MaxPartySize) continue;

            bool active = (i == logic.ActiveSelectionSlot);
            var avatar = logic.AllMemberAvatars[i];
            int hi = 20, lo = 10;

            if (avatar.name != null) avatar.name.depth = active ? hi : lo;
            if (avatar.avatar != null) avatar.avatar.depth = active ? hi : lo;
            if (avatar.polaroid != null) avatar.polaroid.depth = active ? (hi - 1) : (lo - 1);
            if (avatar.background != null) avatar.background.depth = active ? (hi - 2) : (lo - 2);
        }

        // Update confirm label
        bool isLastConfirmableSlot = logic.ActiveSelectionSlot >= logic.MaxPartySize - 1 || logic.HighlightedIndices[logic.ActiveSelectionSlot] == -1;
        if (__instance.confirmLabel != null)
            __instance.confirmLabel.text = Localization.Get(isLastConfirmableSlot ? "ui.map" : "ui.nextperson");

        return false; // Block the original method
    }
}


