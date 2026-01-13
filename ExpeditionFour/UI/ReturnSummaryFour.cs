using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;
using ModAPI.UI;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Patches the expedition return summary screen to correctly display up to four members.
    /// It ensures the internal character arrays are expanded to prevent crashes and properly positions the portraits.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationSummaryPanel), "OnShow")]
    internal static class ReturnSummaryFour
    {
        static void Prefix(ExplorationSummaryPanel __instance)
        {
            if (__instance == null) return;

            // Access the private fields: characterSummaries (List) and m_playerCharacters (Array)
            if (!Safe.TryGetField(__instance, "characterSummaries", out List<EncounterSummaryCharacter> summaries) || summaries == null) return;
            if (!Safe.TryGetField(__instance, "m_playerCharacters", out EncounterCharacter[] characters) || characters == null) return;

            // Ensure we have 4 slots in both the summary list and the character array to prevent out-of-bounds crashes in vanilla code.
            if (summaries.Count < 4 || characters.Length < 4)
            {
                FPELog.Info($"ReturnSummary: Expanding UI containers. Summaries: {summaries.Count}->4, Characters: {characters.Length}->4");
                
                var templateSummary = summaries[0];
                var summaryList = summaries; // Already re-assigned by TryGetField? No, it's a ref.
                
                var characterList = characters.ToList();
                
                while (summaryList.Count < 4)
                {
                    var cloneGo = UIHelper.Clone(templateSummary.gameObject, templateSummary.transform.parent);
                    cloneGo.name = templateSummary.name + "_FPE_" + summaryList.Count;
                    
                    var newSummary = cloneGo.GetComponent<EncounterSummaryCharacter>();
                    var newCharacter = cloneGo.GetComponentInChildren<EncounterCharacter>(true);
                    
                    if (newSummary != null) summaryList.Add(newSummary);
                    if (newCharacter != null) characterList.Add(newCharacter);
                }

                // Push the expanded collections back into the private fields
                Safe.SetField(__instance, "characterSummaries", summaryList);
                Safe.SetField(__instance, "m_playerCharacters", characterList.ToArray());
            }
        }

        static void Postfix(ExplorationSummaryPanel __instance)
        {
            if (__instance == null) return;
            if (!Safe.TryGetField(__instance, "m_party", out ExplorationParty party) || party == null) return;
            if (!Safe.TryGetField(__instance, "characterSummaries", out List<EncounterSummaryCharacter> summaries)) return;
            if (!Safe.TryGetField(__instance, "member_grid", out UIGrid grid) || grid == null) return;

            // Fix interactive button depth (keep it in the background logic)
            FixCloseButton(__instance.gameObject);

            // Handle layout for 3-4 member parties
            if (party.membersCount <= 2) 
            {
                grid.enabled = true;
                return; 
            }

            grid.enabled = false; // Disable vanilla grid for custom 2x2 layout

            float xLeft = FourPersonUIPositions.EncounterSummaryLeftX;
            float xRight = FourPersonUIPositions.EncounterSummaryRightX;
            float yTop = FourPersonUIPositions.EncounterSummaryTopY;
            float yBottom = yTop - FourPersonUIPositions.EncounterSummaryVerticalSpacing;

            var targets = new Vector3[]
            {
                new Vector3(xLeft, yTop, 0),
                new Vector3(xRight, yTop, 0),
                new Vector3(xLeft, yBottom, 0),
                new Vector3(xRight, yBottom, 0)
            };

            for (int i = 0; i < summaries.Count; i++)
            {
                var block = summaries[i];
                if (block == null) continue;
                
                if (i < party.membersCount)
                {
                    block.gameObject.SetActive(true);
                    block.transform.localPosition = targets[i % 4];
                    
                    // Boost depth to ensure portraits appear on top of the paper background
                    UIHelper.SetChildDepths(block.transform, 1000 + (i * 10));

                    // Use unique material instances for the clones to prevent avatar color issues
                    if (block.name.Contains("_FPE_"))
                    {
                        var sprite = block.GetComponentInChildren<UI2DSprite>(true);
                        if (sprite != null && sprite.material != null && !sprite.material.name.Contains("(Clone)"))
                        {
                            sprite.material = new Material(sprite.material);
                        }
                    }
                }
                else
                {
                    block.gameObject.SetActive(false);
                }
            }
        }

        private static void FixCloseButton(GameObject panelGo)
        {
            var candidates = panelGo.GetComponentsInChildren<UIButton>(true)
                                    .Where(b => b.name == "X" || b.name.ToLower().Contains("close"))
                                    .ToList();

            foreach (var btn in candidates)
            {
                var widgets = btn.GetComponentsInChildren<UIWidget>(true);
                foreach (var w in widgets) w.depth = 8000;

                var col = btn.GetComponent<BoxCollider>();
                if (col != null) col.center = new Vector3(col.center.x, col.center.y, -10f);
            }
        }
    }
}
