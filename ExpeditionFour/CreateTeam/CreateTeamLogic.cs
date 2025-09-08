using System.Collections.Generic;
using UnityEngine;

public class FourPersonPartyLogic : MonoBehaviour
{
    // --- Configuration ---
    public int MaxPartySize = 4;

    // --- State Management ---
    public List<int> SelectedMemberIndices = new List<int>();
    public List<int> HighlightedIndices = new List<int>();
    public int ActiveSelectionSlot = 0;
    public int ActiveLoadoutIndex = -1;
    public bool isInitialized = false;

    // --- Object References ---
    public UILabel TitleLabel;
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
        EnsureStateListsSize();
        for (int i = 0; i < MaxPartySize; i++)
        {
            SelectedMemberIndices[i] = -1;
            HighlightedIndices[i] = -1;
        }
    }

    public bool IsIndexSelected(int characterIndex)
    {
        // A character with index -1 ("Nobody") is never considered "selected" by another slot.
        if (characterIndex == -1) return false;

        for (int i = 0; i < this.SelectedMemberIndices.Count; i++)
        {
            // Ignore the slot we are currently editing.
            if (i == this.ActiveSelectionSlot) continue;

            // Check if another slot has this character.
            if (this.SelectedMemberIndices[i] == characterIndex)
            {
                return true; // Found it, this character is taken.
            }
        }
        return false; // Character is not selected in any other slot.
    }


    private void EnsureStateListsSize()
    {
        while (SelectedMemberIndices.Count < MaxPartySize) SelectedMemberIndices.Add(-1);
        while (HighlightedIndices.Count < MaxPartySize) HighlightedIndices.Add(-1);
    }
}

public static class FourPersonConfig
{
    public static int MaxPartySize = 4;
}
