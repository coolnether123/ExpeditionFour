/*using HarmonyLib;
using System;
using UnityEngine;

namespace FourPersonExpeditions.Debugging
{
    // This patch safely hooks into the EncounterDialoguePanel to see what decisions
    // the underlying dialogue logic (BaseDialogueStage) is making.
    [HarmonyPatch(typeof(EncounterDialoguePanel), "HandleDialogStageResult")]
    public static class DialoguePanel_Debug_Patch
    {
        // This runs just before the switch statement in HandleDialogStageResult.
        // It will log the exact result that the dialogue stage returned.
        public static void Prefix(BaseDialogueStage.DialogueResult result)
        {
            try
            {
                // This is the most important log. It tells us if the logic decided
                // to start combat, trade, end the encounter, etc.
                FPELog.Info($"[DialoguePanel] Dialogue Stage returned result: {result}");

                // If the result is DoCombat, but the encounter still doesn't start,
                // it means the problem is inside EncounterManager.OnDialogueOver.
                if (result == BaseDialogueStage.DialogueResult.DoCombat)
                {
                    FPELog.Info("[DialoguePanel] The stage has requested COMBAT. EncounterManager.OnDialogueOver should now be called.");
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"[DialoguePanel] Debug Patch Exception: {e}");
            }
        }
    }

    // Also log the outcome in EncounterManager to confirm the handoff.
    [HarmonyPatch(typeof(EncounterManager), nameof(EncounterManager.OnDialogueOver))]
    public static class OnDialogueOver_Debug_Patch
    {
        public static void Prefix(EncounterManager.EncounterDialogueOutcome outcome)
        {
            FPELog.Info($"[EncounterManager] OnDialogueOver received outcome: {outcome}");
        }
    }
}*/