using HarmonyLib;
using UnityEngine;

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

        // Find the next available person or "Nobody"
        int nextAvailableIndex = currentIndex;
        for (int i = 1; i <= elig.Count + 1; i++)
        {
            int nextIndex = (currentIndex + i) % (elig.Count + 1);
            if (nextIndex == elig.Count) nextIndex = -1; // Wraps to "Nobody"

            if (!logic.IsIndexSelected(nextIndex))
            {
                nextAvailableIndex = nextIndex;
                break;
            }
        }

        logic.HighlightedIndices[activeSlot] = nextAvailableIndex;
        FPELog.Info($"NextMember: Highlight for slot {activeSlot} set to index {nextAvailableIndex}.");

        // Just tell the UI to redraw. Our UpdatePage patch will handle the rest.
        __instance.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
        return false; // Skip vanilla
    }
}

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

        // Find the previous available person or "Nobody"
        int prevAvailableIndex = currentIndex;
        for (int i = 1; i <= elig.Count + 1; i++)
        {
            int prevIndex = (currentIndex - i + elig.Count + 1) % (elig.Count + 1);
            if (prevIndex == elig.Count) prevIndex = -1; // Wraps to "Nobody"

            if (!logic.IsIndexSelected(prevIndex))
            {
                prevAvailableIndex = prevIndex;
                break;
            }
        }

        logic.HighlightedIndices[activeSlot] = prevAvailableIndex;
        FPELog.Info($"PreviousMember: Highlight for slot {activeSlot} set to index {prevAvailableIndex}.");

        // Just tell the UI to redraw.
        __instance.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
        return false; // Skip vanilla
    }
}