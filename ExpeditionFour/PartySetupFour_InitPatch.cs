using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Configures the party setup panel when it is shown. 
    /// Ensures that the underlying ExplorationParty has sufficient member slots for a four-person expedition.
    /// </summary>
    [HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
    public static class ExpeditionMainPanelNew_OnShow_SetupPatch
    {
        public static void Postfix(ExpeditionMainPanelNew __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>()
                        ?? __instance.gameObject.AddComponent<FourPersonPartyLogic>();
            logic.MaxPartySize = FourPersonConfig.MaxPartySize;

            FPELog.Debug("ExpeditionMainPanel: Synchronizing PartyMember components for the current party.");
            
            if (Safe.TryGetField(__instance, "m_partyId", out int partyId))
            {
                var party = ExplorationManager.Instance.GetParty(partyId);

                if (party != null)
                {
                    // Confirm that the vanilla party object has enough PartyMember components for our expanded capacity
                    int currentMembers = party.GetComponents<PartyMember>().Length;
                    for (int i = currentMembers; i < logic.MaxPartySize; i++)
                    {
                        ExplorationManager.Instance.AddMemberToParty(partyId);
                    }

                    // Update the logic controller with the synchronized member components
                    logic.AllPartyMembers.Clear();
                    logic.AllPartyMembers.AddRange(party.GetComponents<PartyMember>());
                    FPELog.Debug($"ExpeditionMainPanel: Successfully synchronized {logic.AllPartyMembers.Count} member slots.");
                }
                else
                {
                    FPELog.Warn("ExpeditionMainPanel: Failed to locate the ExplorationParty object during synchronization.");
                }
            }

            // Initialize member avatar references from the vanilla setup script
            if (!logic.isInitialized)
            {
                var setup = __instance.partySetupScript;
                if (setup != null)
                {
                    logic.AllMemberAvatars.Clear();
                    logic.AllMemberAvatars.Add(setup.memberAvatar);
                    logic.AllMemberAvatars.Add(setup.memberAvatar2);
                    logic.isInitialized = true;
                }
            }

            logic.ResetState();
            var elig = __instance.eligiblePeople;
            logic.HighlightedIndices[0] = (elig != null && elig.Count > 0) ? 0 : -1;

            // Trigger a UI refresh
            __instance.partySetupScript?.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    /// Extends the UpdatePage logic to correctly display and manage four-person party selection.
    /// It implements a paging system for character avatars while reusing the vanilla UI slots.
    /// </summary>
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

            // Determine which "page" (pair) of avatars should be currently visible
            int firstVisibleSlot = (activeSlot / 2) * 2;
            int secondVisibleSlot = firstVisibleSlot + 1;

            // Update the left and right avatar slots based on the current selection context
            if (logic.AllMemberAvatars.Count > 0)
                UpdateAvatarUI(logic.AllMemberAvatars[0], logic, firstVisibleSlot, elig);

            if (logic.AllMemberAvatars.Count > 1)
            {
                var rightAvatar = logic.AllMemberAvatars[1];
                if (secondVisibleSlot < logic.MaxPartySize)
                {
                    UpdateAvatarUI(rightAvatar, logic, secondVisibleSlot, elig);
                }
                else
                {
                    UICloneUtil.SetAvatarActive(rightAvatar, false);
                }
            }

            // Refresh paging buttons and labels
            int highlightedIndex = logic.HighlightedIndices[activeSlot];
            UpdateArrowButtons(__instance, logic, elig, highlightedIndex);

            bool isLastConfirmableSlot = activeSlot >= logic.MaxPartySize - 1 || highlightedIndex == -1;
            if (__instance.confirmLabel != null)
                __instance.confirmLabel.text = Localization.Get(isLastConfirmableSlot ? "ui.map" : "ui.nextperson");

            // Update character stats panel for the currently highlighted member
            FamilyMember person = (highlightedIndex >= 0 && highlightedIndex < elig.Count) ? elig[highlightedIndex] : null;
            UpdateStatsUI(__instance, person);
        }

        /// <summary>
        /// Updates the stat labels and bars on the setup panel.
        /// </summary>
        private static void UpdateStatsUI(ExpeditionPartySetup setup, FamilyMember person)
        {
            if (person != null)
            {
                BaseStats baseStats = person.BaseStats;
                BehaviourStats stats = person.stats;
                
                if (baseStats != null)
                {
                    setup.memberCharisma.text = baseStats.Charisma.Level.ToString("00");
                    setup.memberDexterity.text = baseStats.Dexterity.Level.ToString("00");
                    setup.memberIntelligence.text = baseStats.Intelligence.Level.ToString("00");
                    setup.memberPerception.text = baseStats.Perception.Level.ToString("00");
                    setup.memberStrength.text = baseStats.Strength.Level.ToString("00");

                    Safe.InvokeMethod(setup, "SetBarValue", setup.strengthBar, baseStats.Strength.NormalizedExp, false);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.dexterityBar, baseStats.Dexterity.NormalizedExp, false);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.perceptionBar, baseStats.Perception.NormalizedExp, false);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.intelligenceBar, baseStats.Intelligence.NormalizedExp, false);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.charismaBar, baseStats.Charisma.NormalizedExp, false);
                }

                setup.healthLabel.text = $"{person.health}/{person.maxHealth}";
                setup.statusLabel.text = person.GetLocalizedStatusText();
                setup.illnessLabel.text = person.illness.ToString();

                if (stats != null)
                {
                    Safe.InvokeMethod(setup, "SetBarValue", setup.hungerBar, stats.hunger.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.thirstBar, stats.thirst.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.tirednessBar, stats.fatigue.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.bathroomBar, stats.toilet.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.hygieneBar, stats.dirtiness.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.stressBar, stats.stress.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.traumaBar, stats.trauma.NormalizedValue, true);
                    Safe.InvokeMethod(setup, "SetBarValue", setup.loyaltyBar, stats.loyalty.NormalizedValue, false);

                    if (setup.traumaBar != null)
                        setup.traumaBar.gameObject.SetActive(person.loyalty == FamilyMember.LoyaltyEnum.Loyal);
                    if (setup.loyaltyBar != null)
                        setup.loyaltyBar.gameObject.SetActive(person.loyalty != FamilyMember.LoyaltyEnum.Loyal);
                }

                List<string> traitNames = new List<string>();
                traitNames.AddRange(person.traits.GetLocalizedStrengthNames(true));
                traitNames.AddRange(person.traits.GetLocalizedWeaknessNames(true));
                setup.memberTraits.text = string.Join(", ", traitNames.ToArray());
            }
            else
            {
                // Clear all stat UI elements if no character is selected
                setup.statusLabel.text = string.Empty;
                setup.healthLabel.text = string.Empty;
                setup.illnessLabel.text = string.Empty;
                setup.memberCharisma.text = string.Empty;
                setup.memberDexterity.text = string.Empty;
                setup.memberIntelligence.text = string.Empty;
                setup.memberPerception.text = string.Empty;
                setup.memberStrength.text = string.Empty;
                setup.memberTraits.text = string.Empty;

                Safe.InvokeMethod(setup, "SetBarValue", setup.strengthBar, 0.0f, false);
                Safe.InvokeMethod(setup, "SetBarValue", setup.dexterityBar, 0.0f, false);
                Safe.InvokeMethod(setup, "SetBarValue", setup.perceptionBar, 0.0f, false);
                Safe.InvokeMethod(setup, "SetBarValue", setup.intelligenceBar, 0.0f, false);
                Safe.InvokeMethod(setup, "SetBarValue", setup.charismaBar, 0.0f, false);
                Safe.InvokeMethod(setup, "SetBarValue", setup.hungerBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.thirstBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.tirednessBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.bathroomBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.hygieneBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.stressBar, 0.0f, true);
                Safe.InvokeMethod(setup, "SetBarValue", setup.traumaBar, 0.0f, true);
            }
        }

        /// <summary>
        /// Updates the visual avatar representation for a single slot.
        /// handles depth adjustments to highlight the currently active slot.
        /// </summary>
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

            bool isActiveSlot = (slotIndex == logic.ActiveSelectionSlot);
            int hi = FourPersonUIPositions.CombatUIDepthHigh;
            int lo = FourPersonUIPositions.CombatUIDepthLow;

            if (avatar.name != null) avatar.name.depth = isActiveSlot ? hi : lo;
            if (avatar.avatar != null) avatar.avatar.depth = isActiveSlot ? hi : lo;
            if (avatar.polaroid != null) avatar.polaroid.depth = isActiveSlot ? (hi - 1) : (lo - 1);
            if (avatar.background != null) avatar.background.depth = isActiveSlot ? (hi - 2) : (lo - 2);
        }

        /// <summary>
        /// Enables or disables arrow buttons based on candidate availability.
        /// </summary>
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
}
