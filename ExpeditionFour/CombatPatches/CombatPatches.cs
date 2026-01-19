using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;
using ModAPI.UI;

namespace FourPersonExpeditions.CombatFixes
{
    #region 1. Dialogue Stage Fix (The Encounter Starter) - SAFER PREFIX

    /// <summary>
    /// This patch fixes the soft-lock during NPC encounters with parties > 2.
    /// The original code checks 'if (party.Count > 1)' and enters a broken state.
    /// This Prefix bypasses that faulty logic by ensuring it only runs during an
    /// active, player-controlled encounter, preventing interference with other UI panels.
    /// </summary>
    [HarmonyPatch(typeof(DialogueStageOpening), "State_Npc_WaitOpeningText")]
    public static class DialogueStageOpening_Fix
    {
        public static bool Prefix(object __instance)
        {
            try
            {
                // *** SAFER GUARD CLAUSES ***
                // 1. Only run during an active encounter.
                // 2. Critically, only run when the encounter is NOT player-controlled
                //    (i.e., when an NPC initiates dialogue), which is the specific scenario that breaks.
                if (EncounterManager.Instance == null || !EncounterManager.Instance.EncounterInProgress || EncounterManager.Instance.isPlayerControlled)
                {
                    return true; // Let the original method run for all other cases.
                }

                if (!EncounterDialoguePanel.Instance.IsTextDone())
                {
                    return true; // Let the original method handle text display timing.
                }

                FPELog.Info("[DialogueStageFix] Intercepting NPC-initiated encounter to prevent soft-lock.");

                if (!Safe.TryCall<BaseDialogueStage.DialogueState>(__instance, "State_Npc_PlayerOpeningResponse", out var nextState))
                {
                    FPELog.Warn("[DialogueStageFix] Could not get next state");
                    return true;
                }
                
                Safe.InvokeMethod(__instance, "SetState", nextState);

                return false; // Skip the original buggy method.
            }
            catch (Exception e)
            {
                FPELog.Warn($"[DialogueStageFix] An exception occurred: {e.Message}");
                return true;
            }
        }
    }

    #endregion

    #region 2. Encounter Manager Slot Creation (The Actors)
    // TODO: For a future version (v0.8+), take a better look at character positioning
    // to patch over the whole thing and do it custom, rather than relying on game's base positions.
    [HarmonyPatch(typeof(EncounterManager), nameof(EncounterManager.StartManager))]
    public static class EncounterManager_StartManager_Patch
    {
        public static void Postfix(EncounterManager __instance)
        {
            if (__instance == null) return;
            if (!Safe.TryGetField(__instance, "player_characters", out List<EncounterCharacter> list))
            {
                FPELog.Warn("[CombatSetup] Could not access player_characters");
                return;
            }
            if (list == null || list.Count == 0) return;

            int target = Mathf.Max(list.Count, FourPersonConfig.MaxPartySize);
            if (list.Count >= target) return;

            FPELog.Info($"[CombatSetup] Expanding player character slots from {list.Count} to {target}.");

            var last = list[list.Count - 1];
            var parent = last.transform.parent;

            Vector3 posStep = new Vector3(1f, 0f, 0f);
            if (list.Count >= 2)
            {
                posStep = last.transform.localPosition - list[list.Count - 2].transform.localPosition;
            }

            Vector3 lastFieldHome = Safe.GetFieldOrDefault(last, "field_home_position", Vector3.zero);
            Vector3 lastBreachHome = Safe.GetFieldOrDefault(last, "breach_home_position", Vector3.zero);
            Vector3 lastAboveHome = Safe.GetFieldOrDefault(last, "above_breach_home_position", Vector3.zero);

            Vector3 stepField = posStep, stepBreach = posStep, stepAbove = posStep;
            if (list.Count >= 2)
            {
                var prev = list[list.Count - 2];
                stepField = lastFieldHome - Safe.GetFieldOrDefault(prev, "field_home_position", Vector3.zero);
                stepBreach = lastBreachHome - Safe.GetFieldOrDefault(prev, "breach_home_position", Vector3.zero);
                stepAbove = lastAboveHome - Safe.GetFieldOrDefault(prev, "above_breach_home_position", Vector3.zero);
            }

            int startCount = list.Count;
            for (int i = startCount; i < target; i++)
            {
                var cloneGo = UnityEngine.Object.Instantiate(last.gameObject);
                cloneGo.transform.SetParent(parent);
                cloneGo.name = $"{last.name}_FPE_Clone_{i}";
                int step = i - (startCount - 1);
                Vector3 calculatedPosition = last.transform.localPosition + (posStep * step);
                
                cloneGo.transform.localPosition = calculatedPosition;
                cloneGo.transform.localRotation = last.transform.localRotation;
                cloneGo.transform.localScale = last.transform.localScale;

                var encChar = cloneGo.GetComponent<EncounterCharacter>();
                if (encChar != null)
                {
                    Vector3 finalFieldHome = lastFieldHome + (stepField * step);
                    Vector3 finalBreachHome = lastBreachHome + (stepBreach * step);
                    Vector3 finalAboveHome = lastAboveHome + (stepAbove * step);

                    if (i == startCount) // This is the first character added by the patch (the 4th person)
                    {
                        // Adjust X to move further left, Y to move up, Z remains 0
                        finalFieldHome += new Vector3(-300f, 105f, 0f);
                        finalBreachHome += new Vector3(-300f, 105f, 0f);
                        finalAboveHome += new Vector3(-300f, 105f, 0f);

                        FPELog.Info($"[CombatSetup] Fourth Person - Field Home Position: X={finalFieldHome.x}, Y={finalFieldHome.y}, Z={finalFieldHome.z}");
                        FPELog.Info($"[CombatSetup] Fourth Person - Breach Home Position: X={finalBreachHome.x}, Y={finalBreachHome.y}, Z={finalBreachHome.z}");
                        FPELog.Info($"[CombatSetup] Fourth Person - Above Breach Home Position: X={finalAboveHome.x}, Y={finalAboveHome.y}, Z={finalAboveHome.z}");
                    }

                    Safe.SetField(encChar, "field_home_position", finalFieldHome);
                    Safe.SetField(encChar, "breach_home_position", finalBreachHome);
                    Safe.SetField(encChar, "above_breach_home_position", finalAboveHome);

                    encChar.Initialise();
                    cloneGo.SetActive(false);
                    list.Add(encChar);
                }
            }
        }
    }

    #endregion

    #region 3. Combat Panel UI Fix (The Stage)
    // This section remains the same as it is correct.
    [HarmonyPatch]
    public static class EncounterCombatPanel_Fixes
    {
        [HarmonyPatch(typeof(EncounterCombatPanel), "Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix_ExpandHealthBars(EncounterCombatPanel __instance)
        {
            if (!Safe.TryGetField(__instance, "mini_health_bars", out List<MiniHealthBar> bars))
                return;
            if (!Safe.TryGetField(__instance, "mini_healthbar_root", out Transform root))
                return;

            if (root == null || bars == null) return;

            int requiredCount = FourPersonConfig.MaxPartySize * 2;
            if (bars.Count >= requiredCount) return;

            var template = bars.Count > 0 ? bars[bars.Count - 1] : null;
            if (template == null) return;

            Vector3 posStep = new Vector3(80f, 0f, 0f);
            if (bars.Count >= 2)
            {
                posStep = bars[bars.Count - 1].transform.localPosition - bars[bars.Count - 2].transform.localPosition;
            }

            int startCount = bars.Count;
            for (int i = startCount; i < requiredCount; i++)
            {
                var cloneGo = UIHelper.Clone(template.gameObject, root);   
                cloneGo.name = $"{template.name}_FPE_Clone_{i}";
                int step = i - (startCount - 1);
                cloneGo.transform.localPosition = template.transform.localPosition + (posStep * step);
                var bar = cloneGo.GetComponent<MiniHealthBar>();
                if (bar != null)
                {
                    bar.SetInactive();
                    bars.Add(bar);
                }
            }
        }

        [HarmonyPatch(typeof(EncounterCombatPanel), "AssignMiniHealthBars")]
        [HarmonyPrefix]
        public static bool AssignMiniHealthBars_Prefix_ReassignAll(EncounterCombatPanel __instance)
        {
            if (!Safe.TryGetField(__instance, "player_characters", out List<EncounterCharacter> players))
                return true;
            if (!Safe.TryGetField(__instance, "npc_characters", out List<EncounterCharacter> npcs))
                return true;
            if (!Safe.TryGetField(__instance, "mini_health_bars", out List<MiniHealthBar> bars))
                return true;

            int barIndex = 0;
            if (players != null)
            {
                for (int i = 0; i < players.Count && barIndex < bars.Count; i++)
                {
                    bars[barIndex++].Initialise(players[i]);
                }
            }
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Count && barIndex < bars.Count; i++)
                {
                    bars[barIndex++].Initialise(npcs[i]);
                }
            }
            for (int i = barIndex; i < bars.Count; i++)
            {
                bars[i].SetInactive();
            }
            return false;
        }
    }
    #endregion
}