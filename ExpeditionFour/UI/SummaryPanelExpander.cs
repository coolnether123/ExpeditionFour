using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Reflection;
using ModAPI.UI;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    /// <summary>
    /// Reusable utility for expanding summary panels from 2 to 4 character slots.
    /// Follows Single Responsibility Principle - handles only panel expansion logic.
    /// </summary>
    public static class SummaryPanelExpander
    {
        private const int TARGET_SLOT_COUNT = 4;

        /// <summary>
        /// Expands a summary panel's character slots from 2 to 4.
        /// This should be called in a Harmony Prefix patch.
        /// </summary>
        /// <param name="panelInstance">The panel instance to expand</param>
        /// <param name="summariesFieldName">Name of the List&lt;EncounterSummaryCharacter&gt; field</param>
        /// <param name="charactersFieldName">Name of the EncounterCharacter[] field (optional)</param>
        /// <returns>True if expansion was successful or not needed</returns>
        public static bool ExpandCharacterSlots(
            object panelInstance,
            string summariesFieldName = "characterSummaries",
            string charactersFieldName = "m_playerCharacters")
        {
            if (panelInstance == null) return false;

            // Get the character summaries list
            if (!Safe.TryGetField(panelInstance, summariesFieldName, out List<EncounterSummaryCharacter> summaries) || summaries == null)
            {
                FPELog.Warn($"SummaryPanelExpander: Failed to get field '{summariesFieldName}'");
                return false;
            }

            // Check if expansion is needed
            if (summaries.Count >= TARGET_SLOT_COUNT)
            {
                return true; // Already expanded
            }

            FPELog.Debug($"SummaryPanelExpander: Expanding {panelInstance.GetType().Name} from {summaries.Count} to {TARGET_SLOT_COUNT} slots");

            // Get the template for cloning
            var template = summaries[0];
            if (template == null)
            {
                FPELog.Warn("SummaryPanelExpander: Template summary is null");
                return false;
            }

            // Expand the summaries list
            var characterList = new List<EncounterCharacter>();
            
            // Try to get the character array if field name is provided
            if (!string.IsNullOrEmpty(charactersFieldName))
            {
                if (Safe.TryGetField(panelInstance, charactersFieldName, out EncounterCharacter[] characters) && characters != null)
                {
                    characterList.AddRange(characters);
                }
            }

            // Clone additional slots
            while (summaries.Count < TARGET_SLOT_COUNT)
            {
                var cloneGo = UIHelper.Clone(template.gameObject, template.transform.parent);
                cloneGo.name = $"{template.name}_FPE_{summaries.Count}";

                var newSummary = cloneGo.GetComponent<EncounterSummaryCharacter>();
                var newCharacter = cloneGo.GetComponentInChildren<EncounterCharacter>(true);

                if (newSummary != null)
                {
                    FixClonedMaterials(newSummary);
                    summaries.Add(newSummary);
                }

                if (newCharacter != null && !string.IsNullOrEmpty(charactersFieldName))
                {
                    characterList.Add(newCharacter);
                }
            }

            // Write back the expanded collections
            Safe.SetField(panelInstance, summariesFieldName, summaries);
            
            if (!string.IsNullOrEmpty(charactersFieldName) && characterList.Count > 0)
            {
                Safe.SetField(panelInstance, charactersFieldName, characterList.ToArray());
            }

            return true;
        }

        /// <summary>
        /// Gets the player-controlled character count from EncounterManager.
        /// Helper method for encounter-based panels.
        /// </summary>
        public static int GetPlayerControlledCount()
        {
            var playerCharacters = EncounterManager.Instance?.GetPlayerCharacters();
            if (playerCharacters == null) return 0;

            int count = 0;
            foreach (var character in playerCharacters)
            {
                if (character != null && character.isPlayerControlled)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Applies a 2x2 grid layout for 3-4 member parties.
        /// This should be called in a Harmony Postfix patch.
        /// </summary>
        /// <param name="panelInstance">The panel instance</param>
        /// <param name="memberCount">Number of active party members</param>
        /// <param name="summariesFieldName">Name of the List&lt;EncounterSummaryCharacter&gt; field</param>
        /// <param name="gridFieldName">Name of the UIGrid field</param>
        /// <param name="layout">Layout configuration</param>
        public static void ApplyCustomLayout(
            object panelInstance,
            int memberCount,
            string summariesFieldName = "characterSummaries",
            string gridFieldName = "member_grid",
            SummaryPanelLayout layout = null)
        {
            if (panelInstance == null) return;

            // Use default layout if none provided
            layout = layout ?? SummaryPanelLayout.Default;

            // Get required fields
            if (!Safe.TryGetField(panelInstance, summariesFieldName, out List<EncounterSummaryCharacter> summaries)) return;
            if (!Safe.TryGetField(panelInstance, gridFieldName, out UIGrid grid) || grid == null) return;

            // For 1-2 members, use vanilla grid layout
            if (memberCount <= 2)
            {
                grid.enabled = true;
                return;
            }

            // Disable vanilla grid for custom layout
            grid.enabled = false;

            // Calculate positions for 2x2 grid
            var positions = layout.CalculatePositions(memberCount);

            // Apply positions and visibility
            for (int i = 0; i < summaries.Count; i++)
            {
                var summary = summaries[i];
                if (summary == null) continue;

                if (i < memberCount && i < positions.Length)
                {
                    summary.gameObject.SetActive(true);
                    summary.transform.localPosition = positions[i];

                    // Boost depth to ensure visibility
                    UIHelper.SetChildDepths(summary.transform, layout.BaseDepth + (i * layout.DepthIncrement));

                    // Fix material instances for clones
                    if (summary.name.Contains("_FPE_"))
                    {
                        FixClonedMaterials(summary);
                    }
                }
                else
                {
                    summary.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Fixes material instances for cloned UI elements to prevent rendering issues.
        /// </summary>
        private static void FixClonedMaterials(EncounterSummaryCharacter summary)
        {
            var sprite = summary.GetComponentInChildren<UI2DSprite>(true);
            if (sprite != null && sprite.material != null && !sprite.material.name.Contains("(Clone)"))
            {
                sprite.material = new Material(sprite.material);
            }
        }

        /// <summary>
        /// Fixes the close button depth to ensure it's clickable.
        /// </summary>
        public static void FixCloseButton(GameObject panelGo)
        {
            if (panelGo == null) return;

            var candidates = panelGo.GetComponentsInChildren<UIButton>(true)
                                    .Where(b => b.name == "X" || b.name.ToLower().Contains("close"))
                                    .ToList();

            foreach (var btn in candidates)
            {
                var widgets = btn.GetComponentsInChildren<UIWidget>(true);
                foreach (var w in widgets)
                {
                    w.depth = 8000;
                }

                var col = btn.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.center = new Vector3(col.center.x, col.center.y, -10f);
                }
            }
        }
    }

    /// <summary>
    /// Configuration for summary panel layout.
    /// Follows Open/Closed Principle - can be extended without modifying SummaryPanelExpander.
    /// </summary>
    public class SummaryPanelLayout
    {
        public float LeftX { get; set; }
        public float RightX { get; set; }
        public float TopY { get; set; }
        public float VerticalSpacing { get; set; }
        public float XOffset { get; set; }
        public int BaseDepth { get; set; }
        public int DepthIncrement { get; set; }

        /// <summary>
        /// Default layout using positions from FourPersonUIPositions.
        /// </summary>
        public static SummaryPanelLayout Default => new SummaryPanelLayout
        {
            LeftX = FourPersonUIPositions.EncounterSummaryLeftX,
            RightX = FourPersonUIPositions.EncounterSummaryRightX,
            TopY = FourPersonUIPositions.EncounterSummaryTopY,
            VerticalSpacing = FourPersonUIPositions.EncounterSummaryVerticalSpacing,
            XOffset = FourPersonUIPositions.EncounterSummaryXOffset,
            BaseDepth = 1000,
            DepthIncrement = 10
        };

        /// <summary>
        /// Calculates positions for a 2x2 grid layout.
        /// Applies XOffset to all horizontal positions (matching original implementation).
        /// </summary>
        public Vector3[] CalculatePositions(int memberCount)
        {
            float bottomY = TopY - VerticalSpacing;

            return new Vector3[]
            {
                new Vector3(LeftX + XOffset, TopY, 0),      // Top-left
                new Vector3(RightX + XOffset, TopY, 0),     // Top-right
                new Vector3(LeftX + XOffset, bottomY, 0),   // Bottom-left
                new Vector3(RightX + XOffset, bottomY, 0)   // Bottom-right
            };
        }
    }
}
