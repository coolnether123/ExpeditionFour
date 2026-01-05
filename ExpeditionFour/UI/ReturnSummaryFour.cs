using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.UI
{
    [HarmonyPatch(typeof(ExplorationParty), "Update_Returned_ShowExperienceGained")]
    internal static class ReturnSummaryFour
    {
        static void Postfix(ExplorationParty __instance)
        {
            if (__instance == null || __instance.state != ExplorationParty.ePartyState.ReturnedShowExperienceGained) return;

            var root = GameObject.Find("UI Root");
            if (root == null) return;

            var activeBlocks = root.GetComponentsInChildren<EncounterSummaryCharacter>(true)
                                   .Where(b => b.gameObject.activeInHierarchy)
                                   .ToList();

            EncounterSummaryCharacter template = activeBlocks.Count > 0
                ? activeBlocks[0]
                : root.GetComponentsInChildren<EncounterSummaryCharacter>(true).FirstOrDefault();

            if (template == null) return;

            var parent = template.transform.parent;
            if (parent != null)
            {
                var grid = parent.GetComponent<UIGrid>(); if (grid) grid.enabled = false;
                var table = parent.GetComponent<UITable>(); if (table) table.enabled = false;
            }

            var pms = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers").GetValue(__instance) as List<PartyMember>;
            var members = (pms ?? new List<PartyMember>()).Where(pm => pm != null && pm.person != null).ToList();
            int targetBlockCount = members.Count;

            var allBlocks = new List<EncounterSummaryCharacter>(activeBlocks);

            UI2DSprite templateAvatarSprite = template.GetComponentInChildren<UI2DSprite>(true);
            Material baseMaterial = (templateAvatarSprite != null) ? templateAvatarSprite.material : null;

            if (baseMaterial == null)
            {
                FPELog.Warn("[FPE/UI] Could not find a base material from the template avatar sprite. Colors will fail.");
                return;
            }

            while (allBlocks.Count < targetBlockCount)
            {
                var cloneGo = Object.Instantiate(template.gameObject);
                cloneGo.transform.SetParent(parent);
                cloneGo.name = template.gameObject.name + "_FPE_Clone_" + allBlocks.Count;
                cloneGo.transform.localScale = Vector3.one;
                cloneGo.SetActive(true);
                var comp = cloneGo.GetComponent<EncounterSummaryCharacter>();
                if (comp != null)
                {
                    UI2DSprite clonedAvatarSprite = comp.GetComponentInChildren<UI2DSprite>(true);
                    if (clonedAvatarSprite != null)
                    {
                        // --- THE DEFINITIVE FIX ---
                        // Create a NEW INSTANCE of the material for each clone.
                        // This prevents all sprites from sharing the same material and overwriting each other's colors.
                        // This is necessary for older Unity versions like 5.3.
                        clonedAvatarSprite.material = new Material(baseMaterial);
                    }
                    allBlocks.Add(comp);
                }
            }

            for (int i = targetBlockCount; i < allBlocks.Count; i++)
            {
                allBlocks[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < targetBlockCount; i++)
            {
                var block = allBlocks[i];
                var member = members[i];

                if (block == null || member == null) continue;

                EncounterCharacter oldEncounterChar = AccessTools.Field(typeof(EncounterSummaryCharacter), "character").GetValue(block) as EncounterCharacter;
                if (oldEncounterChar != null && oldEncounterChar.gameObject != null) Object.Destroy(oldEncounterChar.gameObject);

                GameObject charGo = new GameObject($"EncounterCharacter_{member.person.firstName}");
                charGo.transform.SetParent(block.transform);
                EncounterCharacter encounterChar = charGo.AddComponent<EncounterCharacter>();
                AccessTools.Field(typeof(EncounterSummaryCharacter), "character").SetValue(block, encounterChar);
                encounterChar.Setup(member);

                // Now that each block has a unique material instance, the game's original code will work as intended.
                block.ShowCharacter(encounterChar, BaseStats.StatType.Perception, BaseStats.StatType.Strength);

                block.gameObject.SetActive(true);
            }

            // --- Layout Logic ---
            float xLeft = FourPersonUIPositions.EncounterSummaryLeftX;
            float xRight = FourPersonUIPositions.EncounterSummaryRightX;
            float yTop = FourPersonUIPositions.EncounterSummaryTopY;
            float yBottom = yTop - FourPersonUIPositions.EncounterSummaryVerticalSpacing;
            float xOffset = FourPersonUIPositions.EncounterSummaryXOffset;

            if (targetBlockCount <= 2)
            {
                for (int i = 0; i < targetBlockCount; i++)
                    allBlocks[i].transform.localPosition = new Vector3(
                        -50f,
                        yTop - (i * FourPersonUIPositions.EncounterSummaryVerticalSpacing),
                        0);
            }
            else
            {
                var targets = new Vector3[]
                {
                    new Vector3(xLeft + xOffset, yTop, 0),
                    new Vector3(xRight + xOffset, yTop, 0),
                    new Vector3(xLeft + xOffset, yBottom, 0),
                    new Vector3(xRight + xOffset, yBottom, 0)
                };
                for (int i = 0; i < targetBlockCount && i < 4; i++)
                    allBlocks[i].transform.localPosition = targets[i];
            }
        }
    }
}
