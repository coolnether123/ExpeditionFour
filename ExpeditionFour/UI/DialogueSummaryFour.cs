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

            // Get the list of summaries
            if (!Safe.TryGetField(__instance, "characterSummaries", out System.Collections.Generic.List<EncounterSummaryCharacter> summaries))
            {
                return;
            }

            // Add or get our logic component for pagination and auto-advance
            var logic = __instance.gameObject.GetComponent<CombatSummaryLogic>();
            if (logic == null) logic = __instance.gameObject.AddComponent<CombatSummaryLogic>();

            // Initialize logic with the panel and the summary list
            // This will handle showing only the first page and setting up arrows
            logic.Initialize(__instance, summaries);
        }
    }
}

