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

            for (int i = 0; i < blocks.Count && i < 4; i++)
            {
                var b = blocks[i];
                var m = (i < members.Count) ? members[i] : null;

                bool rebound = false;
                try
                {
                    var setMethod = AccessTools.Method(b.GetType(), "InitializeFromMember");
                    if (setMethod != null && m != null)
                    {
                        setMethod.Invoke(b, new object[] { m });
                        rebound = true;
                    }
                }
                catch { }

                if (m != null)
                {
                    // Always ensure portrait is refreshed (covers clones)
                    var avatar = FindAvatarSprite(b);
                    if (avatar != null)
                    {
                        // Force each cloned sprite to get its own material instance
                        if (avatar.material != null)
                        {
                            avatar.material = new Material(avatar.material); // Create independent copy
                        }

                        avatar.sprite2D = null;
                        m.ColorizeAvatarSprite(avatar);
                        avatar.MarkAsChanged();
                    }
                    FPELog.Info($"[FPE/UI] Using avatar sprite: {avatar?.name} for {m.firstName}");
                }

                if (!rebound && m != null)
                {
                    // your existing fallback label updates...
                    var nameLbl = b.GetComponentInChildren<UILabel>();
                    if (nameLbl != null) nameLbl.text = m.firstName;
                    foreach (var lbl in b.GetComponentsInChildren<UILabel>())
                    {
                        string lname = lbl.name.ToLowerInvariant();
                        if (lname.Contains("strength"))
                            lbl.text = $"Strength {m.BaseStats?.Strength?.Level ?? 0}";
                        else if (lname.Contains("dexterity"))
                            lbl.text = $"Dexterity {m.BaseStats?.Dexterity?.Level ?? 0}";
                        else if (lname.Contains("charisma"))
                            lbl.text = $"Charisma {m.BaseStats?.Charisma?.Level ?? 0}";
                        else if (lname.Contains("intelligence"))
                            lbl.text = $"Intelligence {m.BaseStats?.Intelligence?.Level ?? 0}";
                        else if (lname.Contains("perception"))
                            lbl.text = $"Perception {m.BaseStats?.Perception?.Level ?? 0}";
                    }
                }
            }

            // 2×2 layout (L/R columns). Use the first block’s local Y as anchor.
            float xLeft = -260f;
            float xRight = 160f;
            float yTop = blocks[0].transform.localPosition.y;
            float yBottom = yTop - 160f;

            const float X_OFFSET = 45f;

            var targets = new Vector3[]
            {
                new Vector3(xLeft  + X_OFFSET, yTop,    0),
                new Vector3(xLeft  + X_OFFSET, yBottom, 0),
                new Vector3(xRight + X_OFFSET, yTop,    0),
                new Vector3(xRight + X_OFFSET, yBottom, 0),
            };

            int placed = 0;
            for (int i = 0; i < blocks.Count && i < 4; i++)
            {
                var t = blocks[i].transform;
                t.localPosition = targets[i];
                t.localScale = Vector3.one;
                placed++;
            }

            FPELog.Warn($"[FPE/UI] SummaryLayout: found {blocks.Count} character blocks, placed {placed} as 2x2.");

            _doneForParty.Add(id);
        }
    }
}
