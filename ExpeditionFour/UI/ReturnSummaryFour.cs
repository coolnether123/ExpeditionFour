using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.UI
{
    // This patch modifies the post-expedition summary screen to correctly display 3 or 4 party members.
    // It manually arranges the character info blocks into a 2x2 grid.
    [HarmonyPatch(typeof(ExplorationParty), "Update_Returned_ShowExperienceGained")]
    internal static class ReturnSummaryFour
    {
        // A simple HashSet to ensure we only modify the layout for a given party once.
        private static readonly HashSet<int> _doneForParty = new HashSet<int>();

        static void Postfix(ExplorationParty __instance)
        {
            if (__instance == null || __instance.state != ExplorationParty.ePartyState.ReturnedShowExperienceGained) return;

            var id = __instance.id;
            if (_doneForParty.Contains(id)) return;

            // Find the active summary character blocks in the scene.
            var root = GameObject.Find("UI Root");
            if (root == null)
            {
                FPELog.Warn("[FPE/UI] SummaryLayout: UI Root not found.");
                return;
            }

            var blocks = root.GetComponentsInChildren<EncounterSummaryCharacter>(true)
                             .Where(b => b.gameObject.activeInHierarchy)
                             .ToList();

            // *** DEFENSIVE FIX ***
            // If no blocks are found (e.g., after combat), we must find a disabled template to clone.
            // This prevents the mod from failing when the UI is in an unexpected state.
            EncounterSummaryCharacter template = null;
            if (blocks.Count == 0)
            {
                FPELog.Warn("[FPE/UI] SummaryLayout: No active EncounterSummaryCharacter found. Searching for a template...");
                template = root.GetComponentsInChildren<EncounterSummaryCharacter>(true).FirstOrDefault();
                if (template == null)
                {
                    FPELog.Warn("[FPE/UI] SummaryLayout: CRITICAL - No template found. Cannot create summary blocks.");
                    return;
                }
            }
            else
            {
                template = blocks[0];
            }

            // Get the parent transform where we will place our summary blocks.
            var parent = template.transform.parent;
            if (parent != null)
            {
                var grid = parent.GetComponent<UIGrid>(); if (grid) grid.enabled = false;
                var table = parent.GetComponent<UITable>(); if (table) table.enabled = false;
            }

            // Get the actual list of party members who returned.
            var pms = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers").GetValue(__instance) as List<PartyMember>;
            var members = (pms ?? new List<PartyMember>()).Where(pm => pm != null && pm.person != null).ToList();

            int targetBlockCount = members.Count;

            // Ensure we have enough UI blocks for every returned member.
            while (blocks.Count < targetBlockCount)
            {
                var clone = Object.Instantiate(template.gameObject, parent);
                clone.name = template.gameObject.name + "_FPE_Clone" + blocks.Count;
                clone.transform.localScale = Vector3.one;
                clone.SetActive(true);
                var comp = clone.GetComponent<EncounterSummaryCharacter>();
                if (comp != null) blocks.Add(comp);
            }

            // Hide any extra blocks that aren't needed.
            for (int i = targetBlockCount; i < blocks.Count; i++)
            {
                blocks[i].gameObject.SetActive(false);
            }

            // Bind the party member data to the UI blocks.
            for (int i = 0; i < targetBlockCount; i++)
            {
                var block = blocks[i];
                var member = members[i];

                if (block != null && member != null)
                {
                    // Get the internal EncounterCharacter instance from the EncounterSummaryCharacter block.
                    // This field is private, so we use AccessTools.
                    EncounterCharacter encounterChar = AccessTools.Field(typeof(EncounterSummaryCharacter), "character").GetValue(block) as EncounterCharacter;

                    // If the EncounterCharacter is null (e.g., for newly cloned blocks), create one.
                    if (encounterChar == null)
                    {
                        GameObject charGo = new GameObject($"EncounterCharacter_{member.person.firstName}");
                        // Make it a child of the block's GameObject for proper hierarchy and cleanup.
                        charGo.transform.SetParent(block.transform);
                        encounterChar = charGo.AddComponent<EncounterCharacter>();
                        // Set the private 'character' field of the EncounterSummaryCharacter.
                        AccessTools.Field(typeof(EncounterSummaryCharacter), "character").SetValue(block, encounterChar);
                    }

                    // Setup the EncounterCharacter with the PartyMember data.
                    encounterChar.Setup(member);

                    // Call the EncounterSummaryCharacter's ShowCharacter method to display the data.
                    // We'll use Strength and Dexterity as example stats to display.
                    // The expGained and itemsFound are not directly passed here; they are likely
                    // handled by other UI elements on the overall summary panel.
                    block.ShowCharacter(encounterChar, BaseStats.StatType.Strength, BaseStats.StatType.Dexterity);

                    block.gameObject.SetActive(true); // Ensure the block is active
                }
            }

            // Define the manual layout positions.
            float xLeft = -260f;
            float xRight = 160f;
            float yTop = 75f; // Using a more stable Y-anchor
            float yBottom = yTop - 160f;
            const float X_OFFSET = 45f;

            // Apply the correct layout based on the number of members.
            if (targetBlockCount <= 2)
            {
                // Vertical layout for 1-2 members (like vanilla).
                for (int i = 0; i < targetBlockCount; i++)
                {
                    blocks[i].transform.localPosition = new Vector3(-50f, yTop - (i * 160f), 0);
                }
            }
            else
            {
                // 2x2 grid layout for 3-4 members.
                var targets = new Vector3[]
                {
                    new Vector3(xLeft + X_OFFSET, yTop, 0),    // Top-Left
                    new Vector3(xRight + X_OFFSET, yTop, 0),   // Top-Right
                    new Vector3(xLeft + X_OFFSET, yBottom, 0), // Bottom-Left
                    new Vector3(xRight + X_OFFSET, yBottom, 0) // Bottom-Right
                };

                for (int i = 0; i < targetBlockCount && i < 4; i++)
                {
                    blocks[i].transform.localPosition = targets[i];
                }
            }

            _doneForParty.Add(id);
        }
    }
}