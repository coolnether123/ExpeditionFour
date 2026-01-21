using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Patches the expedition return summary screen to correctly display up to four members.
    /// Refactored to use SummaryPanelExpander for DRY compliance.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationSummaryPanel), "OnShow")]
    internal static class ReturnSummaryFour
    {
        static void Prefix(ExplorationSummaryPanel __instance)
        {
            // Delegate expansion logic to the reusable expander
            SummaryPanelExpander.ExpandCharacterSlots(
                __instance,
                summariesFieldName: "characterSummaries",
                charactersFieldName: "m_playerCharacters"
            );
        }

        static void Postfix(ExplorationSummaryPanel __instance)
        {
            if (__instance == null) return;
            if (!Safe.TryGetField(__instance, "m_party", out ExplorationParty party) || party == null) return;

            // Fix close button depth
            SummaryPanelExpander.FixCloseButton(__instance.gameObject);

            // Apply custom layout for 3-4 member parties
            SummaryPanelExpander.ApplyCustomLayout(
                __instance,
                party.membersCount,
                summariesFieldName: "characterSummaries",
                gridFieldName: "member_grid",
                layout: SummaryPanelLayout.Default
            );
        }
    }
}
