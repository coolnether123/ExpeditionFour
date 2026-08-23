using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Extends the combat encounter summary to four members.
    /// Shows Strength and Dexterity XP gains after combat.
    /// </summary>
    [HarmonyPatch(typeof(EncounterCombatSummaryPanel), "OnShow")]
    internal static class CombatSummaryFour
    {
        static void Prefix(EncounterCombatSummaryPanel __instance)
        {
            // Expand character slots from 2 to 4
            // Unlike the return panel, this panel stores only characterSummaries.
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

            var logic = __instance.gameObject.GetComponent<CombatSummaryLogic>();
            if (logic == null) logic = __instance.gameObject.AddComponent<CombatSummaryLogic>();

            logic.Initialize(__instance, summaries);
        }
    }
}

