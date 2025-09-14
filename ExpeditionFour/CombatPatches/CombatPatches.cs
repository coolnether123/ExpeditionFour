using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

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

                var tr = Traverse.Create(__instance);
                var nextState = tr.Method("State_Npc_PlayerOpeningResponse").GetValue<BaseDialogueStage.DialogueState>();
                tr.Method("SetState", new object[] { nextState }).GetValue();

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
            var tr = Traverse.Create(__instance);
            var list = tr.Field("player_characters").GetValue<List<EncounterCharacter>>();
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

            var lastTr = Traverse.Create(last);
            Vector3 lastFieldHome = lastTr.Field("field_home_position").GetValue<Vector3>();
            Vector3 lastBreachHome = lastTr.Field("breach_home_position").GetValue<Vector3>();
            Vector3 lastAboveHome = lastTr.Field("above_breach_home_position").GetValue<Vector3>();

            Vector3 stepField = posStep, stepBreach = posStep, stepAbove = posStep;
            if (list.Count >= 2)
            {
                var prevTr = Traverse.Create(list[list.Count - 2]);
                stepField = lastFieldHome - prevTr.Field("field_home_position").GetValue<Vector3>();
                stepBreach = lastBreachHome - prevTr.Field("breach_home_position").GetValue<Vector3>();
                stepAbove = lastAboveHome - prevTr.Field("above_breach_home_position").GetValue<Vector3>();
            }

            int startCount = list.Count;
            for (int i = startCount; i < target; i++)
            {
                var cloneGo = UnityEngine.Object.Instantiate(last.gameObject, parent);
                cloneGo.name = $"{last.name}_FPE_Clone_{i}";
                int step = i - (startCount - 1);
                Vector3 calculatedPosition = last.transform.localPosition + (posStep * step);
                
                cloneGo.transform.localPosition = calculatedPosition;
                cloneGo.transform.localRotation = last.transform.localRotation;
                cloneGo.transform.localScale = last.transform.localScale;

                var encChar = cloneGo.GetComponent<EncounterCharacter>();
                if (encChar != null)
                {
                    var encTr = Traverse.Create(encChar);
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

                    encTr.Field("field_home_position").SetValue(finalFieldHome);
                    encTr.Field("breach_home_position").SetValue(finalBreachHome);
                    encTr.Field("above_breach_home_position").SetValue(finalAboveHome);

                    encChar.Initialise();
                    cloneGo.SetActive(false);
                    list.Add(encChar);
                }
            }

            // Log positions and names for all player characters
            FPELog.Info("[CombatSetup] All Player Character Positions:");
            for (int i = 0; i < list.Count; i++)
            {
                var character = list[i];
                var charTr = Traverse.Create(character); // Create Traverse for the character

                Vector3 fieldHome = charTr.Field("field_home_position").GetValue<Vector3>();
                Vector3 breachHome = charTr.Field("breach_home_position").GetValue<Vector3>();
                Vector3 aboveBreachHome = charTr.Field("above_breach_home_position").GetValue<Vector3>();

                FPELog.Info($"[CombatSetup] Character: {character.Name_Short} (Index: {i}) - Position: X={character.transform.position.x}, Y={character.transform.position.y}, Z={character.transform.position.z}");
                FPELog.Info($"[CombatSetup] Character: {character.Name_Short} (Index: {i}) - Field Home: X={fieldHome.x}, Y={fieldHome.y}, Z={fieldHome.z}");
                FPELog.Info($"[CombatSetup] Character: {character.Name_Short} (Index: {i}) - Breach Home: X={breachHome.x}, Y={breachHome.y}, Z={breachHome.z}");
                FPELog.Info($"[CombatSetup] Character: {character.Name_Short} (Index: {i}) - Above Breach Home: X={aboveBreachHome.x}, Y={aboveBreachHome.y}, Z={aboveBreachHome.z}");
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
            var tr = Traverse.Create(__instance);
            var bars = tr.Field("mini_health_bars").GetValue<List<MiniHealthBar>>();
            var root = tr.Field("mini_healthbar_root").GetValue<Transform>();

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
                var cloneGo = UnityEngine.Object.Instantiate(template.gameObject);   
                cloneGo.transform.SetParent(root);
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
            var tr = Traverse.Create(__instance);
            var players = tr.Field("player_characters").GetValue<List<EncounterCharacter>>();
            var npcs = tr.Field("npc_characters").GetValue<List<EncounterCharacter>>();
            var bars = tr.Field("mini_health_bars").GetValue<List<MiniHealthBar>>();

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