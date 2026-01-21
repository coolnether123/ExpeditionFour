using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Core logic controller for the four-person expedition mod.
    /// Manages the state for party selection, loadouts, and map paging.
    /// This component is typically attached to the ExpeditionMainPanelNew GameObject.
    /// </summary>
    public class FourPersonPartyLogic : MonoBehaviour
    {
        // --- Configuration ---
        public int MaxPartySize = 4;

        // --- Expedition Setup State ---
        public List<int> SelectedMemberIndices = new List<int>();
        public List<int> HighlightedIndices = new List<int>();
        public int ActiveSelectionSlot = 0;
        public int ActiveLoadoutIndex = -1;
        public bool isInitialized = false;

        // --- Map Panel State ---
        public int mapScreenPage = 0;
        public bool isMapUIInitialized = false; 
        public GameObject pageLeftArrow;
        public GameObject pageRightArrow;
        public UILabel pageIndicatorLabel;

        // --- Object References ---
        public List<PartyMember> AllPartyMembers = new List<PartyMember>();
        public List<ExpeditionPartySetup.MemberAvatar> AllMemberAvatars = new List<ExpeditionPartySetup.MemberAvatar>();
        public Vector3 AvatarStepOffset = Vector3.zero;

        public void Awake()
        {
            // Initialize party size from config
            MaxPartySize = Mathf.Max(2, FourPersonConfig.MaxPartySize);
            EnsureStateListsSize();
        }

        /// <summary>
        /// Resets the internal selection state.
        /// </summary>
        public void ResetState()
        {
            ActiveSelectionSlot = 0;
            ActiveLoadoutIndex = -1;
            mapScreenPage = 0; 
            EnsureStateListsSize();
            for (int i = 0; i < MaxPartySize; i++)
            {
                SelectedMemberIndices[i] = -1;
                HighlightedIndices[i] = -1;
            }
        }

        /// <summary>
        /// Checks if a character is already selected in another slot.
        /// </summary>
        public bool IsIndexSelected(int characterIndex)
        {
            if (characterIndex == -1) return false;
            for (int i = 0; i < this.SelectedMemberIndices.Count; i++)
            {
                if (i == this.ActiveSelectionSlot) continue;
                if (this.SelectedMemberIndices[i] == characterIndex) return true;
            }
            return false;
        }

        /// <summary>
        /// Maintains state list sizes consistent with MaxPartySize.
        /// </summary>
        private void EnsureStateListsSize()
        {
            while (SelectedMemberIndices.Count < MaxPartySize) SelectedMemberIndices.Add(-1);
            while (HighlightedIndices.Count < MaxPartySize) HighlightedIndices.Add(-1);
        }

        // --- Paging logic for the Map screen ---

        /// <summary>
        /// Advances to the next page of members on the map summary.
        /// </summary>
        public void NextMapPage()
        {
            var panel = FindObjectOfType<PartyMapPanel>();
            if (panel == null) return;
            
            if (Safe.TryGetField(panel, "m_allParties", out List<ExplorationParty> allParties) &&
                Safe.TryGetField(panel, "m_currentPartyIndex", out int currentIndex))
            {
                if (allParties == null || currentIndex >= allParties.Count) return;

                int memberCount = allParties[currentIndex].membersCount;
                int maxPages = Mathf.CeilToInt(memberCount / 2.0f);

                if (mapScreenPage < maxPages - 1)
                {
                    mapScreenPage++;
                    Safe.InvokeMethod(panel, "UpdateUI");
                }
            }
        }

        /// <summary>
        /// Returns to the previous page of members on the map summary.
        /// </summary>
        public void PreviousMapPage()
        {
            var panel = FindObjectOfType<PartyMapPanel>();
            if (panel == null) return;

            if (mapScreenPage > 0)
            {
                mapScreenPage--;
                Safe.InvokeMethod(panel, "UpdateUI");
            }
        }
    }

    /// <summary>
    /// Global configuration constants for the mod.
    /// </summary>
    public static class FourPersonConfig
    {
        public const int MaxPartySize = 4;
    }
}
