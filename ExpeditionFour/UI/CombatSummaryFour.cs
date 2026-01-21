using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Patches the combat encounter summary screen to display up to four members.
    /// Shows Strength and Dexterity XP gains after combat.
    /// </summary>
    [HarmonyPatch(typeof(EncounterCombatSummaryPanel), "OnShow")]
    internal static class CombatSummaryFour
    {
        static void Prefix(EncounterCombatSummaryPanel __instance)
        {
            // Expand character slots from 2 to 4
            // Note: EncounterCombatSummaryPanel doesn't have m_playerCharacters array,
            // only characterSummaries list
            SummaryPanelExpander.ExpandCharacterSlots(
                __instance,
                summariesFieldName: "characterSummaries",
                charactersFieldName: null  // This panel doesn't have a separate character array
            );
        }

        static void Postfix(EncounterCombatSummaryPanel __instance)
        {
            if (__instance == null) return;

            // Fix close button depth
            SummaryPanelExpander.FixCloseButton(__instance.gameObject);

            // Get the list of summaries
            if (!Safe.TryGetField(__instance, "characterSummaries", out System.Collections.Generic.List<EncounterSummaryCharacter> summaries))
            {
                return;
            }

            // Add or get our logic component
            var logic = __instance.gameObject.GetComponent<CombatSummaryLogic>();
            if (logic == null) logic = __instance.gameObject.AddComponent<CombatSummaryLogic>();

            // Initialize logic with the panel and the summary list
            // This will handle showing only the first page and setting up arrows
            logic.Initialize(__instance, summaries);
        }
    }
}

