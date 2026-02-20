using HarmonyLib;
using UnityEngine;
using System.Linq;
using ModAPI.Reflection;
using FourPersonExpeditions;

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

        if (!Safe.TryGetField(_loadoutPanel, "m_displayedMember", out PartyMember displayedMember))
        {
            FPELog.Warn("Could not get displayed member from Loadout panel.");
            return;
        }

        int currentMemberSlot = _partyLogic.AllPartyMembers.IndexOf(displayedMember);
        FPELog.Debug($"OnConfirmClicked: currentMemberSlot={currentMemberSlot}");
        FPELog.Debug($"OnConfirmClicked: SelectedMemberIndices=[{string.Join(", ", _partyLogic.SelectedMemberIndices.Select(x => x.ToString()).ToArray())}]");

        if (currentMemberSlot < 0)
        {
            FPELog.Warn($"Could not determine current member slot for {displayedMember.person?.firstName}.");
            return;
        }

        int nextMemberSlot = -1;
        for (int i = currentMemberSlot + 1; i < _partyLogic.MaxPartySize; i++)
        {
            FPELog.Debug($"OnConfirmClicked: checking i={i}, selected={_partyLogic.SelectedMemberIndices[i]}");
            if (_partyLogic.SelectedMemberIndices[i] != -1)
            {
                nextMemberSlot = i;
                break;
            }
        }

        FPELog.Debug($"OnConfirmClicked: final nextMemberSlot={nextMemberSlot}");

        if (nextMemberSlot != -1)
        {
            if (nextMemberSlot < 0 || nextMemberSlot >= _partyLogic.AllPartyMembers.Count)
            {
                FPELog.Error($"[FPE] OnConfirmClicked: nextMemberSlot {nextMemberSlot} is out of bounds for AllPartyMembers (count={_partyLogic.AllPartyMembers.Count}).");
                return;
            }

            var nextPartyMember = _partyLogic.AllPartyMembers[nextMemberSlot];
            FPELog.Debug($"OnConfirmClicked: advancing loadout {currentMemberSlot} -> {nextMemberSlot} for {nextPartyMember.person?.firstName}");
            _loadoutPanel.InitializeLoadout(nextPartyMember, false);

            var audioSource = _mainPanel.GetComponent<AudioSource>() ?? _mainPanel.gameObject.AddComponent<AudioSource>();
            if (_mainPanel.selectMemberSound != null) audioSource.PlayOneShot(_mainPanel.selectMemberSound);
        }
        else
        {
            FPELog.Info($"Finalizing expedition from loadout slot {currentMemberSlot}.");
            Safe.InvokeMethod(_mainPanel, "ConfirmExpeditionSettings");
        }
    }
}
