using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

// This component is attached to ExpeditionMainPanelNew GameObject.
// It is extended to also manage state for the PartyMapPanel UI.
public class FourPersonPartyLogic : MonoBehaviour
{
    // --- Configuration ---
    public int MaxPartySize = 4;

    // --- Expedition Setup State ---
    public List<int> SelectedMemberIndices = new List<int>();
    public List<int> HighlightedIndices = new List<int>();
    public int ActiveSelectionSlot = 0;
    public bool isInitialized = false;

    // --- Map Panel State ---
    public int mapScreenPage = 0;
    public bool isMapUIInitialized = false; // Flag to ensure we only create UI once.
    public GameObject pageLeftArrow;
    public GameObject pageRightArrow;
    public UILabel pageIndicatorLabel;

    // --- Object References ---
    public List<PartyMember> AllPartyMembers = new List<PartyMember>();
    public List<ExpeditionPartySetup.MemberAvatar> AllMemberAvatars = new List<ExpeditionPartySetup.MemberAvatar>();
    public Vector3 AvatarStepOffset = Vector3.zero;

    public void Awake()
    {
        MaxPartySize = Mathf.Max(2, FourPersonConfig.MaxPartySize);
        EnsureStateListsSize();
    }

    public void ResetState()
    {
        ActiveSelectionSlot = 0;
        mapScreenPage = 0; // Reset page on show
        EnsureStateListsSize();
        for (int i = 0; i < MaxPartySize; i++)
        {
            SelectedMemberIndices[i] = -1;
            HighlightedIndices[i] = -1;
        }
    }

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

    private void EnsureStateListsSize()
    {
        while (SelectedMemberIndices.Count < MaxPartySize) SelectedMemberIndices.Add(-1);
        while (HighlightedIndices.Count < MaxPartySize) HighlightedIndices.Add(-1);
    }

    // --- Public methods for UI events ---
    public void NextMapPage()
    {
        var panel = FindObjectOfType<PartyMapPanel>();
        if (panel == null) return;
        var party = Traverse.Create(panel).Field("m_allParties").GetValue<List<ExplorationParty>>();
        int partyIndex = Traverse.Create(panel).Field("m_currentPartyIndex").GetValue<int>();
        if (party == null || partyIndex >= party.Count) return;

        int memberCount = party[partyIndex].membersCount;
        int maxPages = Mathf.CeilToInt(memberCount / 2.0f);

        if (mapScreenPage < maxPages - 1)
        {
            mapScreenPage++;
            Traverse.Create(panel).Method("UpdateUI").GetValue(); // Force UI refresh
        }
    }

    public void PreviousMapPage()
    {
        var panel = FindObjectOfType<PartyMapPanel>();
        if (panel == null) return;

        if (mapScreenPage > 0)
        {
            mapScreenPage--;
            Traverse.Create(panel).Method("UpdateUI").GetValue(); // Force UI refresh
        }
    }
}

public static class FourPersonConfig { public static int MaxPartySize = 4; }