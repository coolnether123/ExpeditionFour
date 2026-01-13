using HarmonyLib;
using System.Linq;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;

// This single Prefix patch acts as a complete state machine for the OnExtra1 button.
[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnExtra1))]
public static class ExpeditionMainPanelNew_OnExtra1_StateMachine_Patch
{
    /// <summary>
    /// Intercepts the OnExtra1 button click to manage the transition between route selection and member loadouts.
    /// This allows for multiple loadout screens to be shown sequentially for parties with more than two members.
    /// </summary>
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        if (!Safe.TryGetField(__instance, "m_page", out object page)) return true;
        string pageName = page.ToString();

        switch (pageName)
        {
            case "RouteSetup":
                if (Safe.GetFieldOrDefault(__instance, "m_isReadyToGo", false))
                {
                    FPELog.Info("OnExtra1: Route confirmed. Commencing loadout sequence.");
                    
                    // Identify the first selected member to initialize loadout
                    logic.ActiveLoadoutIndex = logic.SelectedMemberIndices.FindIndex(idx => idx != -1);
                    if (logic.ActiveLoadoutIndex == -1)
                    {
                        FPELog.Info("OnExtra1: No characters selected. Finalizing expedition immediately.");
                        Safe.InvokeMethod(__instance, "ConfirmExpeditionSettings");
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
                    FPELog.Info($"OnExtra1: Transitioning to loadout slot index {logic.ActiveLoadoutIndex}.");
                    ShowLoadoutForSlot(__instance, logic, logic.ActiveLoadoutIndex, false);
                }
                else
                {
                    FPELog.Info("OnExtra1: Final loadout stage completed. Finalizing expedition settings.");
                    Safe.InvokeMethod(__instance, "ConfirmExpeditionSettings");
                }
                return false;

            default:
                return true;
        }
    }

    /// <summary>
    /// Configures and displays the loadout screen for a specific party member slot.
    /// </summary>
    private static void ShowLoadoutForSlot(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic, int slot, bool isFirst)
    {
        if (!panel.LoadoutScreen.activeInHierarchy)
        {
            panel.LoadoutScreen.SetActive(true);
            panel.PartySetup.SetActive(false);
            panel.MapScreen.SetActive(false);
        }

        // Set page to LoadoutMember1 (assuming enum value 1 corresponds to it)
        Safe.SetField(panel, "m_page", 1);

        if (Safe.TryGetField(panel, "m_loadoutScript", out ExpeditionLoadout loadout))
        {
            var pm = logic.AllPartyMembers[slot];
            loadout.InitializeLoadout(pm, isFirst);

            // Update button text based on whether more members need loadout configuration
            bool hasAnother = logic.SelectedMemberIndices.Skip(slot + 1).Any(idx => idx != -1);
            loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));
        }

        var audio = isFirst ? panel.acceptRouteSound : panel.selectMemberSound;
        panel.GetComponent<AudioSource>()?.PlayOneShot(audio);
    }
}
