using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.MapPatches
{
    /// <summary>
    /// Patches the inventory/equipment window opening methods to account for paging.
    /// Without these patches, clicking on members on page 2 would open the inventories
    /// of members on page 1 instead.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OpenWeaponEquipWindowP1))]
    public static class PartyMapPanel_OpenWeaponEquipWindowP1_Patch
    {
        public static bool Prefix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true; // No paging logic, use vanilla behavior

            // Get necessary fields
            if (!Safe.TryGetField(__instance, "m_editPartyLoadoutPanel", out EditPartyLoadoutPanel editPanel)) return true;
            if (!Safe.TryGetField(__instance, "m_allParties", out List<ExplorationParty> allParties)) return true;
            if (!Safe.TryGetField(__instance, "m_currentPartyIndex", out int currentPartyIndex)) return true;

            if (editPanel == null || allParties == null || currentPartyIndex >= allParties.Count) return true;

            // Calculate the actual member index based on the current page
            int actualMemberIndex = logic.mapScreenPage * 2; // Page 0 -> 0, Page 1 -> 2

            FPELog.Info($"Opening inventory for member at actual index {actualMemberIndex} (page {logic.mapScreenPage}, slot 0)");

            // Open the equipment window with the correct member index
            editPanel.SetUpPanel(allParties[currentPartyIndex], actualMemberIndex);
            UIPanelManager.Instance().PushPanel(editPanel);

            return false; // Skip original method
        }
    }

    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OpenWeaponEquipWindowP2))]
    public static class PartyMapPanel_OpenWeaponEquipWindowP2_Patch
    {
        public static bool Prefix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true; // No paging logic, use vanilla behavior

            // Get necessary fields
            if (!Safe.TryGetField(__instance, "m_editPartyLoadoutPanel", out EditPartyLoadoutPanel editPanel)) return true;
            if (!Safe.TryGetField(__instance, "m_allParties", out List<ExplorationParty> allParties)) return true;
            if (!Safe.TryGetField(__instance, "m_currentPartyIndex", out int currentPartyIndex)) return true;

            if (editPanel == null || allParties == null || currentPartyIndex >= allParties.Count) return true;

            // Calculate the actual member index based on the current page
            int actualMemberIndex = (logic.mapScreenPage * 2) + 1; // Page 0 -> 1, Page 1 -> 3

            FPELog.Info($"Opening inventory for member at actual index {actualMemberIndex} (page {logic.mapScreenPage}, slot 1)");

            // Open the equipment window with the correct member index
            editPanel.SetUpPanel(allParties[currentPartyIndex], actualMemberIndex);
            UIPanelManager.Instance().PushPanel(editPanel);

            return false; // Skip original method
        }
    }
}
