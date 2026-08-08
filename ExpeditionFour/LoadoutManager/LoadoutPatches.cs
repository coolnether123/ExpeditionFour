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
        if (__instance == null || __instance.gameObject == null)
            return true;

        var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null) return true;

        if (!Safe.TryGetField(__instance, "m_page", out object page)) return true;
        int pageValue = (int)page;

        bool loadoutScreenActive = __instance.LoadoutScreen != null && __instance.LoadoutScreen.activeInHierarchy;
        FPELog.Debug($"[FPE] OnExtra1 Prefix: Page={pageValue}, ActiveLoadoutIndex={logic.ActiveLoadoutIndex}, LoadoutScreen.active={loadoutScreenActive}");

        // CRITICAL: Ensure characters are assigned to the correct PartyMember components before any loadout logic
        if (!AssignPeopleToPartyMembers(__instance, logic))
            return false;

        // If ActiveLoadoutIndex >= 0 AND the LoadoutScreen is visible, we're already in loadout mode.
        if (logic.ActiveLoadoutIndex >= 0 && loadoutScreenActive)
        {
            FPELog.Debug($"[FPE] OnExtra1: Detected active loadout mode (slot {logic.ActiveLoadoutIndex}).");
            
            // Find the next selected member slot
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
                FPELog.Debug($"[FPE] OnExtra1: Transitioning to next loadout slot index {logic.ActiveLoadoutIndex}.");
                ShowLoadoutForSlot(__instance, logic, logic.ActiveLoadoutIndex, false);
            }
            else
            {
                FPELog.Debug("[FPE] OnExtra1: Final loadout stage completed. " + ExpeditionFourPatches.DescribeExpeditionState(__instance, logic));
                logic.ActiveLoadoutIndex = -1; // Reset state
                Safe.InvokeMethod(__instance, "ConfirmExpeditionSettings");
            }
            return false;
        }

        // Handle route setup -> loadout transition
        if (pageValue == 3) // RouteSetup
        {
            if (Safe.GetFieldOrDefault(__instance, "m_isReadyToGo", false))
            {
                FPELog.Debug("[FPE] OnExtra1: Route confirmed. Commencing loadout sequence.");
                
                // Identify the first selected member to initialize loadout
                logic.ActiveLoadoutIndex = logic.SelectedMemberIndices.FindIndex(idx => idx != -1);
                if (logic.ActiveLoadoutIndex == -1)
                {
                    FPELog.Warn("[FPE] OnExtra1: No characters selected despite ReadyToGo. Finalizing anyway.");
                    Safe.InvokeMethod(__instance, "ConfirmExpeditionSettings");
                }
                else
                {
                    ShowLoadoutForSlot(__instance, logic, logic.ActiveLoadoutIndex, true);
                }
            }
            else
            {
                FPELog.Warn("[FPE] OnExtra1: Not ready to go (RouteSetup). " + ExpeditionFourPatches.DescribeExpeditionState(__instance, logic));
            }
            return false;
        }

        // For any other page, let vanilla handle it
        FPELog.Debug($"[FPE] OnExtra1: Unhandled page {pageValue}. Falling back to vanilla.");
        return true;
    }

    /// <summary>
    /// Synchronizes the FamilyMember references from our selection logic into the actual PartyMember components.
    /// This ensures that the loadout screen and final expedition registration see the correct characters.
    /// </summary>
    private static bool AssignPeopleToPartyMembers(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        if (!PartyMemberSlotSynchronizer.EnsureSlots(panel, logic))
        {
            FPELog.Warn("[FPE] AssignPeopleToPartyMembers: Could not provision the selected party slots.");
            return false;
        }

        var elig = panel.eligiblePeople;
        if (elig == null)
        {
            FPELog.Warn("[FPE] AssignPeopleToPartyMembers: eligible character list is unavailable.");
            return false;
        }
        int max = Mathf.Min(logic.MaxPartySize, logic.AllPartyMembers.Count);
        
        for (int i = 0; i < max; i++)
        {
            var pm = logic.AllPartyMembers[i];
            if (pm == null) continue;

            int charIdx = (i < logic.SelectedMemberIndices.Count) ? logic.SelectedMemberIndices[i] : -1;
            FamilyMember nextPerson = charIdx >= 0 && charIdx < elig.Count ? elig[charIdx] : null;
            if ((UnityEngine.Object)pm.person != (UnityEngine.Object)nextPerson)
            {
                pm.ClearAllEquipment();
                pm.person = nextPerson;
            }
        }

        PartyMemberSlotSynchronizer.SyncVanillaMemberFields(panel, logic);
        return true;
    }

    /// <summary>
    /// Configures and displays the loadout screen for a specific party member slot.
    /// Uses vanilla's DelayedPageChange system to ensure proper state transitions.
    /// </summary>
    private static void ShowLoadoutForSlot(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic, int slot, bool isFirst)
    {
        FPELog.Debug($"[FPE] ShowLoadoutForSlot: slot={slot}, isFirst={isFirst}");

        // Use the vanilla DelayedPageChange system to ensure proper timing
        System.Action pageAction = () =>
        {
            if (panel.LoadoutScreen == null || panel.PartySetup == null || panel.MapScreen == null)
            {
                FPELog.Error("[FPE] ShowLoadoutForSlot: required expedition screens are missing.");
                return;
            }

            if (!panel.LoadoutScreen.activeInHierarchy)
            {
                panel.LoadoutScreen.SetActive(true);
                panel.PartySetup.SetActive(false);
                panel.MapScreen.SetActive(false);
            }

            if (Safe.TryGetField(panel, "m_loadoutScript", out ExpeditionLoadout loadout))
            {
                if (slot < 0 || slot >= logic.AllPartyMembers.Count)
                {
                    FPELog.Error($"[FPE] ShowLoadoutForSlot: FATAL - Slot index {slot} is out of range for AllPartyMembers (count={logic.AllPartyMembers.Count})");
                    return;
                }

                var pm = logic.AllPartyMembers[slot];
                if (pm == null)
                {
                    FPELog.Error($"[FPE] ShowLoadoutForSlot: FATAL - PartyMember at slot {slot} is null.");
                    return;
                }

                if (pm.person == null)
                {
                    FPELog.Warn($"[FPE] ShowLoadoutForSlot: Warning - No FamilyMember assigned to PartyMember at slot {slot}. Attempting re-sync.");
                    AssignPeopleToPartyMembers(panel, logic);
                }

                // Pass isFirst to InitializeLoadout to control item reset behavior
                FPELog.Debug($"[FPE] ShowLoadoutForSlot: Initializing loadout UI for {pm.person?.firstName ?? "Unknown"} (slot {slot})");
                loadout.InitializeLoadout(pm, isFirst);

                // Update button text based on whether more members need loadout configuration
                bool hasAnother = false;
                for (int i = slot + 1; i < logic.MaxPartySize; i++)
                {
                    if (logic.SelectedMemberIndices[i] != -1)
                    {
                        hasAnother = true;
                        break;
                    }
                }
                loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));
            }

            // CRITICAL: Set the page AFTER activating screens and initializing loadout
            Safe.SetField(panel, "m_page", 1); // LoadoutMember1
            FPELog.Debug($"[FPE] ShowLoadoutForSlot: Page set to 1 for slot {slot}");
        };

        // Invoke vanilla's delayed page change system
        Safe.InvokeMethod(panel, "DelayedPageChange", pageAction);

        // Play appropriate sound
        var audio = isFirst ? panel.acceptRouteSound : panel.selectMemberSound;
        if (audio != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(audio);
        }
    }
}

[HarmonyPatch(typeof(ExpeditionMainPanelNew), nameof(ExpeditionMainPanelNew.OnCancel))]
internal static class ExpeditionMainPanelNew_OnCancel_LoadoutPatch
{
    public static bool Prefix(ExpeditionMainPanelNew __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return true;

        FourPersonPartyLogic logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
        if (logic == null)
            return true;

        int activeSelectionSlot = logic.NormalizeActiveSelectionSlot();
        if (__instance.PartySetup != null && __instance.PartySetup.activeInHierarchy
            && activeSelectionSlot > 0)
        {
            int previousSlot = activeSelectionSlot - 1;
            logic.ActiveSelectionSlot = previousSlot;
            logic.HighlightedIndices[previousSlot] = logic.SelectedMemberIndices[previousSlot];
            __instance.partySetupScript?.SendMessage("UpdatePage", SendMessageOptions.DontRequireReceiver);
            FPELog.Debug($"[FPE] OnCancel: returning to party selection slot {previousSlot}.");
            return false;
        }

        if (__instance.LoadoutScreen == null || !__instance.LoadoutScreen.activeInHierarchy)
            return true;

        object page;
        if (!Safe.TryGetField(__instance, "m_page", out page) || page == null || (int)page != 1)
            return true;

        int previousLoadoutSlot = -1;
        for (int i = logic.ActiveLoadoutIndex - 1; i >= 0; i--)
        {
            if (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1)
            {
                previousLoadoutSlot = i;
                break;
            }
        }

        if (previousLoadoutSlot == -1)
        {
            // Returning from the first loadout goes back to the map. Match the
            // vanilla cleanup for slots three and four before it does so.
            for (int i = 2; i < logic.AllPartyMembers.Count; i++)
            {
                if (logic.AllPartyMembers[i] != null)
                    logic.AllPartyMembers[i].ClearAllEquipment();
            }

            logic.ActiveLoadoutIndex = -1;
            return true;
        }

        logic.ActiveLoadoutIndex = previousLoadoutSlot;
        ShowPreviousLoadout(__instance, logic, previousLoadoutSlot);
        return false;
    }

    private static void ShowPreviousLoadout(
        ExpeditionMainPanelNew panel,
        FourPersonPartyLogic logic,
        int slot)
    {
        FPELog.Debug($"[FPE] OnCancel: returning to loadout slot {slot}.");

        System.Action pageAction = () =>
        {
            if (panel.LoadoutScreen == null || panel.PartySetup == null || panel.MapScreen == null)
                return;

            panel.LoadoutScreen.SetActive(true);
            panel.PartySetup.SetActive(false);
            panel.MapScreen.SetActive(false);

            ExpeditionLoadout loadout;
            if (!Safe.TryGetField(panel, "m_loadoutScript", out loadout) || loadout == null)
                return;
            if (slot < 0 || slot >= logic.AllPartyMembers.Count || logic.AllPartyMembers[slot] == null)
                return;

            loadout.InitializeLoadout(logic.AllPartyMembers[slot], false);
            bool hasAnother = false;
            for (int i = slot + 1; i < logic.MaxPartySize; i++)
            {
                if (i < logic.SelectedMemberIndices.Count && logic.SelectedMemberIndices[i] != -1)
                {
                    hasAnother = true;
                    break;
                }
            }
            loadout.SetConfirmText(Localization.Get(hasAnother ? "UI.NextPerson" : "UI.SendParty"));
            Safe.SetField(panel, "m_page", 1);
        };

        Safe.InvokeMethod(panel, "DelayedPageChange", pageAction);
    }
}

/// <summary>
/// Debug patch to trace all UI clicks and identify blocking elements.
/// </summary>
[HarmonyPatch(typeof(UICamera), "Update")]
public static class UIDebug_ClickTracker_Patch
{
    public static void Postfix()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GameObject hovered = UICamera.hoveredObject;
            if (hovered != null)
            {
                FPELog.Debug($"[UIDebug] Mouse Click: {hovered.name} (Layer={hovered.layer})");
                UIDebug.TakeSnapshot(hovered, "Click Target");
            }
            else
            {
                FPELog.Debug("[UIDebug] Mouse Click: (None)");
            }
        }
    }
}
