using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.UI
{
    [HarmonyPatch(typeof(ExplorationParty), "Update_Returned_ShowExperienceGained")]
    internal static class ReturnSummaryFour
    {
        // Called every frame while the sheet is visible; we guard so we only touch it once.
        private static readonly HashSet<int> _doneForParty = new HashSet<int>();

        static UI2DSprite FindAvatarSprite(Component block)
        {
            var sprites = block.GetComponentsInChildren<UI2DSprite>(true);
            if (sprites == null || sprites.Length == 0) return null;

            // Prefer names that look like a portrait; otherwise largest area
            foreach (var s in sprites)
            {
                var n = (s.name ?? "").ToLowerInvariant();
                if (n.Contains("avatar") || n.Contains("portrait") || n.Contains("char"))
                    return s;
            }

            return sprites.OrderByDescending(s => s.width * s.height).First();
        }

        static void Postfix(ExplorationParty __instance)
        {
            if (__instance == null) return;
            if (__instance.state != ExplorationParty.ePartyState.ReturnedShowExperienceGained) return;

            var id = __instance.id;
            if (_doneForParty.Contains(id)) return;

            // Find the active summary character blocks in the scene.
            // They’re typically under a temporary panel – search broadly under UIRoot.
            var root = GameObject.Find("UI Root");
            if (root == null)
            {
                FPELog.Warn("[FPE/UI] SummaryLayout: UI Root not found.");
                return;
            }

            var blocks = root.GetComponentsInChildren<EncounterSummaryCharacter>(true)
                             .Where(b => b.gameObject.activeInHierarchy)
                             .ToList();

            if (blocks.Count == 0)
            {
                FPELog.Warn("[FPE/UI] SummaryLayout: no EncounterSummaryCharacter found.");
                return;
            }

            // Disable NGUI auto-layout on the parent so our manual positions stick.
            var parent = blocks[0].transform.parent;
            if (parent != null)
            {
                var grid = parent.GetComponent<UIGrid>(); if (grid) grid.enabled = false;
                var table = parent.GetComponent<UITable>(); if (table) table.enabled = false;
            }

            // We need up to 4 blocks. If we only have 2, clone the first 2.
            while (blocks.Count < 4 && blocks.Count > 0)
            {
                var src = blocks[blocks.Count - 1];
                var clone = Object.Instantiate(src.gameObject, parent);
                clone.name = src.gameObject.name + "_FPE" + blocks.Count;
                clone.transform.localScale = Vector3.one;
                clone.SetActive(true);

                var comp = clone.GetComponent<EncounterSummaryCharacter>();
                if (comp != null) blocks.Add(comp);
                else blocks.Add(src); // extremely defensive; shouldn’t happen
            }

            // Bind party members 0..3 into blocks 0..3 (where available).
            var pms = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                 .GetValue(__instance) as List<PartyMember>;
            var members = (pms ?? new List<PartyMember>()).Select(pm => pm?.person).ToList();

            // Only create blocks for actual party members, not always 4
            int targetBlockCount = Mathf.Min(members.Count, FourPersonConfig.MaxPartySize);
            while (blocks.Count < targetBlockCount && blocks.Count > 0)
            {
                var src = blocks[blocks.Count - 1];
                var clone = Object.Instantiate(src.gameObject, parent);
                clone.name = src.gameObject.name + "_FPE" + blocks.Count;
                clone.transform.localScale = Vector3.one;
                clone.SetActive(true);

                var comp = clone.GetComponent<EncounterSummaryCharacter>();
                if (comp != null) blocks.Add(comp);
                else blocks.Add(src); // extremely defensive; shouldn't happen
            }

            // Hide any extra blocks that aren't needed
            for (int i = targetBlockCount; i < blocks.Count; i++)
            {
                blocks[i].gameObject.SetActive(false);
            }

            // 2×2 layout (L/R columns). Use the first block’s local Y as anchor.
            float xLeft = -260f;
            float xRight = 160f;
            float yTop = blocks[0].transform.localPosition.y;
            float yBottom = yTop - 160f;

            const float X_OFFSET = 45f;

            // Layout based on actual number of blocks needed
            int actualBlocks = Mathf.Min(blocks.Count, members.Count);

            if (actualBlocks <= 2)
            {
                // Use vertical layout for 1-2 members (like vanilla)
                for (int i = 0; i < actualBlocks; i++)
                {
                    var t = blocks[i].transform;
                    t.localPosition = new Vector3(-50f, yTop - (i * 160f), 0);
                    t.localScale = Vector3.one;
                }
            }
            else
            {
                // Use 2×2 layout for 3-4 members
                var targets = new Vector3[]
                {
                    new Vector3(xLeft + X_OFFSET, yTop, 0),
                    new Vector3(xLeft + X_OFFSET, yBottom, 0),
                    new Vector3(xRight + X_OFFSET, yTop, 0),
                    new Vector3(xRight + X_OFFSET, yBottom, 0),
                };

                for (int i = 0; i < actualBlocks && i < 4; i++)
                {
                    var t = blocks[i].transform;
                    t.localPosition = targets[i];
                    t.localScale = Vector3.one;
                }
            }

            _doneForParty.Add(id);
        }
    }


namespace ExpeditionFour.ExperiencePatches
    {
        [HarmonyPatch(typeof(ExplorationParty), "Begin_Finished")]
        public static class ExplorationParty_Begin_Finished_Patch
        {
            public static bool Prefix(ExplorationParty __instance)
            {
                // Get the private fields we need
                var tr = Traverse.Create(__instance);
                var partyMembers = tr.Field("m_partyMembers").GetValue<System.Collections.Generic.List<PartyMember>>();
                int searchExperienceGained = tr.Field("m_searchExperienceGained").GetValue<int>();
                bool participatedInCombat = tr.Field("m_participatedInCombat").GetValue<bool>();
                var searchedThisTrip = tr.Field("m_searchedThisTrip").GetValue<System.Collections.Generic.List<string>>();
                bool encounteredNpcsThisTrip = tr.Field("m_encounteredNpcsThisTrip").GetValue<bool>();

                FPELog.Info($"Begin_Finished Patch: Processing {partyMembers.Count} party members for experience");

                // Achievement callback
                if (AchievementManager.instance != null)
                    AchievementManager.instance.OnExpeditionOver(__instance.id);

                // Award experience to ALL party members (this is the fix)
                foreach (PartyMember partyMember in partyMembers)
                {
                    if (partyMember?.person != null)
                    {
                        partyMember.person.isAway = false;
                        var perceptionStat = partyMember.person.BaseStats.GetStatByEnum(BaseStats.StatType.Perception);
                        perceptionStat.IncreaseExp(searchExperienceGained);
                        FPELog.Info($"  -> Awarded {searchExperienceGained} Perception XP to {partyMember.person.firstName}");
                    }
                }

                // Call the exploration manager
                ExplorationManager.Instance.PartyHasReturned(__instance.id);

                // Reset party state flags
                tr.Field("m_partyReturning").SetValue(false);
                tr.Field("m_partyWalkingToShelter").SetValue(false);

                // Journal entries (vanilla logic)
                if (participatedInCombat)
                {
                    JournalManager.Instance.CreateJournalEntry(JournalManager.JournalEntryType.Combat);
                }
                else if ((searchedThisTrip.Count > 0 || encounteredNpcsThisTrip) && UnityEngine.Random.Range(0, 4) == 0)
                {
                    if (searchedThisTrip.Count > 0)
                    {
                        int index = UnityEngine.Random.Range(0, searchedThisTrip.Count);
                        JournalManager.Instance.RecordEvent(JournalEvents.Event.ExplorationLocation,
                            new ActivityLog.ExtraInfoString(__instance.id.ToString(), false),
                            new ActivityLog.ExtraInfoString(searchedThisTrip[index], false));
                    }
                    if (encounteredNpcsThisTrip)
                        JournalManager.Instance.RecordEvent(JournalEvents.Event.ExplorationNPC,
                            new ActivityLog.ExtraInfoString(__instance.id.ToString(), false));

                    JournalManager.Instance.RecordEvent(JournalEvents.Event.ExplorationEnded,
                        new ActivityLog.ExtraInfoString(__instance.id.ToString(), false));
                    JournalManager.Instance.CreateJournalEntry(JournalManager.JournalEntryType.Exploration);
                }

                // Push the Finished state
                var pushStateMethod = AccessTools.Method(typeof(ExplorationParty), "PushState");
                pushStateMethod.Invoke(__instance, new object[] { ExplorationParty.ePartyState.Finished });

                return false; // Skip original method
            }
        }
    }
}
