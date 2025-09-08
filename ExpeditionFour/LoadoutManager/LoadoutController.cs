using HarmonyLib;
using UnityEngine;
using System.Linq;

public class LoadoutController : MonoBehaviour
{
    private FourPersonPartyLogic _partyLogic;
    private ExpeditionLoadout _loadoutPanel;
    private ExpeditionMainPanelNew _mainPanel;

    public void Initialize(FourPersonPartyLogic partyLogic)
    {
        this._partyLogic = partyLogic;
        this._loadoutPanel = GetComponent<ExpeditionLoadout>();
        this._mainPanel = ExpeditionMainPanelNew.Instance;
    }

    public void OnConfirmClicked()
    {
        if (_partyLogic == null || _loadoutPanel == null || _mainPanel == null)
        {
            FPELog.Warn("LoadoutController is not initialized! Aborting click.");
            return;
        }

        // *** FIX: Use Traverse to access the private m_displayedMember field ***
        var displayedMember = Traverse.Create(_loadoutPanel).Field("m_displayedMember").GetValue<PartyMember>();
        if (displayedMember == null)
        {
            FPELog.Warn("Could not get displayed member from Loadout panel.");
            return;
        }

        int currentMemberSlot = _partyLogic.AllPartyMembers.IndexOf(displayedMember);
        FPELog.Info($"[DEBUG] OnConfirmClicked: currentMemberSlot = {currentMemberSlot}");
        FPELog.Info($"[DEBUG] OnConfirmClicked: SelectedMemberIndices = [{string.Join(", ", _partyLogic.SelectedMemberIndices.Select(x => x.ToString()).ToArray())}]");

        if (currentMemberSlot < 0)
        {
            FPELog.Warn($"Could not determine current member slot for {displayedMember.person?.firstName}.");
            return;
        }

        int nextMemberSlot = -1;
        for (int i = currentMemberSlot + 1; i < _partyLogic.MaxPartySize; i++)
        {
            FPELog.Info($"[DEBUG] OnConfirmClicked: Checking i = {i}, SelectedMemberIndices[i] = {_partyLogic.SelectedMemberIndices[i]}");
            if (_partyLogic.SelectedMemberIndices[i] != -1)
            {
                nextMemberSlot = i;
                break;
            }
        }

        FPELog.Info($"[DEBUG] OnConfirmClicked: Final nextMemberSlot = {nextMemberSlot}");

        if (nextMemberSlot != -1)
        {
            var nextPartyMember = _partyLogic.AllPartyMembers[nextMemberSlot];
            FPELog.Info($"Loadout button clicked for slot {currentMemberSlot}. Advancing to slot {nextMemberSlot}. Initializing loadout for {nextPartyMember.person?.firstName}.");
            _loadoutPanel.InitializeLoadout(nextPartyMember, false);

            var audioSource = _mainPanel.GetComponent<AudioSource>() ?? _mainPanel.gameObject.AddComponent<AudioSource>();
            if (_mainPanel.selectMemberSound != null) audioSource.PlayOneShot(_mainPanel.selectMemberSound);
        }
        else
        {
            FPELog.Info($"Final loadout button clicked for slot {currentMemberSlot}. Finalizing expedition.");
            Traverse.Create(_mainPanel).Method("ConfirmExpeditionSettings").GetValue();
        }
    }
}