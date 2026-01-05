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

        int nextAvailableIndex = PartySetupNavigationUtil.FindNextAvailableIndex(currentIndex, elig.Count, logic, 1);

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

        int prevAvailableIndex = PartySetupNavigationUtil.FindNextAvailableIndex(currentIndex, elig.Count, logic, -1);

        logic.HighlightedIndices[activeSlot] = prevAvailableIndex;
        FPELog.Info($"PreviousMember: Highlight for slot {activeSlot} set to index {prevAvailableIndex}.");

        // Just tell the UI to redraw.
        __instance.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
        return false; // Skip vanilla
    }
}

internal static class PartySetupNavigationUtil
{
    public static int FindNextAvailableIndex(int currentIndex, int eligibleCount, FourPersonPartyLogic logic, int direction)
    {
        int count = eligibleCount + 1; // +1 for "Nobody"
        for (int i = 1; i <= count; i++)
        {
            int rawIdx = currentIndex + (i * direction);
            int idx = ((rawIdx % count) + count) % count;
            if (idx == eligibleCount) idx = -1; // Wrap to "Nobody"
            if (!logic.IsIndexSelected(idx)) return idx;
        }
        return currentIndex;
    }
}
