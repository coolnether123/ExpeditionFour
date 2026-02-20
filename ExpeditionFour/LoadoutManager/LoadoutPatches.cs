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
        int pageValue = (int)page;

        FPELog.Debug($"[FPE] OnExtra1 Prefix: Page={pageValue}, ActiveLoadoutIndex={logic.ActiveLoadoutIndex}, LoadoutScreen.active={__instance.LoadoutScreen.activeInHierarchy}");

        // CRITICAL: Ensure characters are assigned to the correct PartyMember components before any loadout logic
        AssignPeopleToPartyMembers(__instance, logic);

        // If ActiveLoadoutIndex >= 0 AND the LoadoutScreen is visible, we're already in loadout mode.
        if (logic.ActiveLoadoutIndex >= 0 && __instance.LoadoutScreen.activeInHierarchy)
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
                FPELog.Debug("[FPE] OnExtra1: Final loadout stage completed. Finalizing expedition settings.");
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
                FPELog.Warn("[FPE] OnExtra1: Not ready to go (RouteSetup).");
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
    private static void AssignPeopleToPartyMembers(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        EnsurePartyMemberSlots(panel, logic);

        var elig = panel.eligiblePeople;
        int max = Mathf.Min(logic.MaxPartySize, logic.AllPartyMembers.Count);
        
        for (int i = 0; i < max; i++)
        {
            var pm = logic.AllPartyMembers[i];
            if (pm == null) continue;

            int charIdx = (i < logic.SelectedMemberIndices.Count) ? logic.SelectedMemberIndices[i] : -1;
            if (charIdx >= 0 && charIdx < elig.Count)
            {
                pm.person = elig[charIdx];
            }
            else
            {
                pm.person = null;
            }
        }
    }

    private static void EnsurePartyMemberSlots(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
    {
        if (panel == null || logic == null || ExplorationManager.Instance == null) return;
        if (!Safe.TryGetField(panel, "m_partyId", out int partyId)) return;

        var party = ExplorationManager.Instance.GetParty(partyId);
        if (party == null) return;

        int requiredSlots = 0;
        for (int i = 0; i < logic.MaxPartySize && i < logic.SelectedMemberIndices.Count; i++)
        {
            if (logic.SelectedMemberIndices[i] != -1)
                requiredSlots = i + 1;
        }

        requiredSlots = Mathf.Max(requiredSlots, 2);

        var components = party.GetComponents<PartyMember>();
        for (int i = components.Length; i < requiredSlots; i++)
        {
            var added = ExplorationManager.Instance.AddMemberToParty(partyId);
            if (added == null)
            {
                FPELog.Warn($"[FPE] EnsurePartyMemberSlots: Failed to add slot {i} for party {partyId}.");
                break;
            }
        }

        components = party.GetComponents<PartyMember>();
        logic.AllPartyMembers.Clear();
        logic.AllPartyMembers.AddRange(components);
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
            AudioManager.Instance.PlayUI(audio);
        }
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
                ModAPI.UI.UIDebug.TakeSnapshot(hovered, "Click Target");
            }
            else
            {
                FPELog.Debug("[UIDebug] Mouse Click: (None)");
            }
        }
    }
}
