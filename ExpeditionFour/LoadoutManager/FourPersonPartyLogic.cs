using System.Collections.Generic;
using UnityEngine;

public class FourPersonPartyLogic : MonoBehaviour
{
    public int MaxPartySize = 4;
    public List<int> SelectedMemberIndices = new List<int>();
    public List<int> HighlightedIndices = new List<int>();
    public int ActiveSelectionSlot = 0;
    public bool isInitialized = false;
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
}

public static class FourPersonConfig { public static int MaxPartySize = 4; }