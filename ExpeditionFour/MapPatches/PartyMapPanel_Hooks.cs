using HarmonyLib;

namespace FourPersonExpeditions.MapPatches
{
    /// <summary>
    /// Connects PartyMapPanel lifecycle events to four-person paging behavior.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnShow))]
    public static class PartyMapPanel_OnShow_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            PartyMapPanelPaginationController.OnShow(__instance);
        }
    }

    /// <summary>
    /// Replaces the vanilla two-member map summary with the current paged view.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), "UpdateUI")]
    public static class PartyMapPanel_UpdateUI_Patch
    {
        public static bool Prefix(PartyMapPanel __instance)
        {
            return PartyMapPanelPaginationController.UpdateUi(__instance);
        }
    }

    /// <summary>
    /// Resets member paging when switching to the next expedition party.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnTabRight))]
    public static class PartyMapPanel_OnTabRight_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            PartyMapPanelPaginationController.ResetPage(__instance);
        }
    }

    /// <summary>
    /// Resets member paging when switching to the previous expedition party.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnTabLeft))]
    public static class PartyMapPanel_OnTabLeft_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            PartyMapPanelPaginationController.ResetPage(__instance);
        }
    }
}
