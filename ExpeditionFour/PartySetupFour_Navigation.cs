using HarmonyLib;
using UnityEngine;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Intercepts character navigation (cycling through eligible survivors) within the expedition setup screen.
    /// This implementation ensures that characters already selected in other slots are skipped.
    /// </summary>
    [HarmonyPatch(typeof(ExpeditionPartySetup), nameof(ExpeditionPartySetup.NextMember))]
    public static class ExpeditionPartySetup_NextMember_Patch
    {
        public static bool Prefix(ExpeditionPartySetup __instance)
        {
            var panel = ExpeditionMainPanelNew.Instance;
            if (panel == null) return true;
            var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true;

            var elig = panel.eligiblePeople;
            if (elig == null || elig.Count == 0) return false;

            int activeSlot = logic.ActiveSelectionSlot;
            int currentIndex = logic.HighlightedIndices[activeSlot];

            // Calculate the next candidate index, skipping those already tagged for the expedition
            int nextAvailableIndex = PartySetupNavigationUtil.FindNextAvailableIndex(currentIndex, elig.Count, logic, 1);

            logic.HighlightedIndices[activeSlot] = nextAvailableIndex;
            FPELog.Info($"Member Navigation: Highlighting character index {nextAvailableIndex} for selection slot {activeSlot}.");

            // Refresh the page to update the avatar and stats displays
            __instance.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
            return false;
        }
    }

    /// <summary>
    /// Intercepts character navigation in the reverse direction.
    /// </summary>
    [HarmonyPatch(typeof(ExpeditionPartySetup), nameof(ExpeditionPartySetup.PreviousMember))]
    public static class ExpeditionPartySetup_PreviousMember_Patch
    {
        public static bool Prefix(ExpeditionPartySetup __instance)
        {
            var panel = ExpeditionMainPanelNew.Instance;
            if (panel == null) return true;
            var logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true;

            var elig = panel.eligiblePeople;
            if (elig == null || elig.Count == 0) return false;

            int activeSlot = logic.ActiveSelectionSlot;
            int currentIndex = logic.HighlightedIndices[activeSlot];

            int prevAvailableIndex = PartySetupNavigationUtil.FindNextAvailableIndex(currentIndex, elig.Count, logic, -1);

            logic.HighlightedIndices[activeSlot] = prevAvailableIndex;
            FPELog.Info($"Member Navigation: Highlighting character index {prevAvailableIndex} for selection slot {activeSlot}.");

            __instance.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
            return false;
        }
    }

    /// <summary>
    /// Utility class for calculating the next or previous valid character index during setup.
    /// </summary>
    internal static class PartySetupNavigationUtil
    {
        /// <summary>
        /// Finds the next available character index by cycling in the specified direction.
        /// An index of -1 represents the "Nobody" or "Slot Empty" state.
        /// </summary>
        public static int FindNextAvailableIndex(int currentIndex, int eligibleCount, FourPersonPartyLogic logic, int direction)
        {
            // Total options include all eligible characters plus the empty slot (-1)
            int totalOptions = eligibleCount + 1; 
            
            for (int i = 1; i <= totalOptions; i++)
            {
                int rawIdx = currentIndex + (i * direction);
                // Ensure the index wraps correctly around the candidate list
                int idx = ((rawIdx % totalOptions) + totalOptions) % totalOptions;
                
                // Map the wrap-around index back to -1 for "Nobody"
                if (idx == eligibleCount) idx = -1; 
                
                // Return the index if the character is not already selected in another slot
                if (!logic.IsIndexSelected(idx)) return idx;
            }
            return currentIndex;
        }
    }
}
