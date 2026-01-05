using HarmonyLib;
using System.Linq;
using UnityEngine;

// This single Prefix patch acts as a complete state machine for the OnExtra1 button.
[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnExtra1))]
public static class ExpeditionMainPanelNew_OnExtra1_StateMachine_Patch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        var tr = Traverse.Create(__instance);
        var page = tr.Field("m_page").GetValue();
        string pageName = page.ToString();

        switch (pageName)
        {
            case "RouteSetup":
                if (tr.Field("m_isReadyToGo").GetValue<bool>())
                {
                    FPELog.Info("OnExtra1 State Machine: Route confirmed. Starting loadout sequence.");
                    // Find the very first selected member to start with.
                    logic.ActiveLoadoutIndex = logic.SelectedMemberIndices.FindIndex(idx => idx != -1);
                    if (logic.ActiveLoadoutIndex == -1)
                    {
                        FPELog.Info("No members selected. Finalizing immediately.");
                        tr.Method("ConfirmExpeditionSettings").GetValue();
                    }
                    else
                    {
                        ShowLoadoutForSlot(__instance, logic, logic.ActiveLoadoutIndex, true);
                    }
                }
                return false;

            case "LoadoutMember1":
            case "LoadoutMember2":
                int nextSlot = -1;
                for (int i = logic.ActiveLoadoutIndex + 1; i < logic.MaxPartySize; i++)
                {
                    if (logic.SelectedMemberIndices[i] != -1)
                    {
                        nextSlot = i;
                        break;
                    }
                }

                if (nextSlot != -1)
                {
                    logic.ActiveLoadoutIndex = nextSlot;
                    FPELog.Info($"Advancing to next loadout slot: {logic.ActiveLoadoutIndex}");
                    ShowLoadoutForSlot(__instance, logic, logic.ActiveLoadoutIndex, false);
                }
                else
                {
                    FPELog.Info("Final loadout confirmed. Finalizing expedition.");
                    tr.Method("ConfirmExpeditionSettings").GetValue();
                }
                return false;

            default:
                return true;
        }
    }

    private static void ShowLoadoutForSlot(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic, int slot, bool isFirst)
    {
        var tr = Traverse.Create(panel);

        if (!panel.LoadoutScreen.activeInHierarchy)
        {
            panel.LoadoutScreen.SetActive(true);
            panel.PartySetup.SetActive(false);
            panel.MapScreen.SetActive(false);
        }

        tr.Field("m_page").SetValue(1); // Set page to LoadoutMember1 enum value

        var loadout = tr.Field("m_loadoutScript").GetValue<ExpeditionLoadout>();
        var pm = logic.AllPartyMembers[slot];
        loadout.InitializeLoadout(pm, isFirst);

        bool hasAnother = logic.SelectedMemberIndices.Any(selIdx => logic.SelectedMemberIndices.IndexOf(selIdx) > slot && selIdx != -1);
        loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));

        var audio = isFirst ? panel.acceptRouteSound : panel.selectMemberSound;
        panel.GetComponent<AudioSource>()?.PlayOneShot(audio);
    }
}
