using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Patches the dialogue encounter summary screen to display up to four members.
    /// Shows Charisma XP (trade) or Intelligence XP (recruit) gains after dialogue encounters.
    /// </summary>
    [HarmonyPatch(typeof(EncounterDialogueSummaryPanel), "OnShow")]
    internal static class DialogueSummaryFour
    {
        static void Prefix(EncounterDialogueSummaryPanel __instance)
        {
            // Expand character slots from 2 to 4
            // Note: EncounterDialogueSummaryPanel doesn't have m_playerCharacters array,
            // only characterSummaries list
            SummaryPanelExpander.ExpandCharacterSlots(
                __instance,
                summariesFieldName: "characterSummaries",
                charactersFieldName: null  // This panel doesn't have a separate character array
            );
        }

        static void Postfix(EncounterDialogueSummaryPanel __instance)
        {
            if (__instance == null) return;

            // Fix close button depth
            SummaryPanelExpander.FixCloseButton(__instance.gameObject);

            // Apply custom layout for 3-4 member parties
            SummaryPanelExpander.ApplyCustomLayout(
                __instance,
                SummaryPanelExpander.GetPlayerControlledCount(),
                summariesFieldName: "characterSummaries",
                gridFieldName: "member_grid",
                layout: SummaryPanelLayout.Default
            );
        }
    }
}

