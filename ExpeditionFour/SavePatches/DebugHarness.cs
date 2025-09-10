using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ExpeditionFour.SavePatches
{
    internal static class DebugHarness
    {
        private static string Join<T>(IEnumerable<T> items) => string.Join(", ", items.Select(x => x?.ToString() ?? "null").ToArray());

        private static IEnumerable<FamilyMember> FamilyList()
        {
            var fm = FamilyManager.Instance;
            if (fm == null) yield break;
            var list = AccessTools.Field(typeof(FamilyManager), "m_familyMembers").GetValue(fm) as List<FamilyMember>;
            if (list == null) yield break;
            foreach (var m in list) if (m != null) yield return m;
        }

        private static IEnumerable<PartyMember> PartyMembers(ExplorationParty p)
        {
            if (p == null) yield break;
            var list = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                   .GetValue(p) as List<PartyMember>;
            if (list == null) yield break;
            foreach (var pm in list) if (pm != null) yield return pm;
        }

        private static string PersonLine(FamilyMember m)
        {
            if (m == null) return "<null>";
            var pos = m.transform != null ? (Vector3?)m.transform.position : null;
            return $"{m.firstName}(away={m.isAway}, left={m.finishedLeavingShelter}, active={(m.gameObject?.activeSelf ?? false)}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")})";
        }

        [HarmonyPatch(typeof(SaveManager), "SaveGame")]
        private static class Trace_Save_Start
        {
            static void Prefix()
            {
                var lines = FamilyList().Select(PersonLine).ToList();
                FPELog.Warn($"[FPE/TRACE] PRE-SAVE family: [{Join(lines)}]");
            }
        }

        [HarmonyPatch(typeof(SaveManager), "SaveGame")]
        private static class Trace_Save_End
        {
            static void Postfix()
            {
                var lines = FamilyList().Select(PersonLine).ToList();
                FPELog.Warn($"[FPE/TRACE] POST-SAVE family: [{Join(lines)}]");
            }
        }

        [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.SaveLoad))]
        private static class Trace_Party_SaveLoad
        {
            static void Prefix(ExplorationParty __instance, SaveData data)
            {
                if (data?.isLoading == true || data?.isSaving == true)
                {
                    var members = PartyMembers(__instance)
                                  .Select(pm => pm?.person)
                                  .Where(p => p != null)
                                  .Select(PersonLine)
                                  .ToList();
                    FPELog.Warn($"[FPE/TRACE] PARTY {(data.isLoading ? "LOAD" : "SAVE")} pre: id={__instance.id} state={__instance.state} members=[{Join(members)}]");
                }
            }

            static void Postfix(ExplorationParty __instance, SaveData data)
            {
                if (data?.isLoading == true || data?.isSaving == true)
                {
                    var members = PartyMembers(__instance)
                                  .Select(pm => pm?.person)
                                  .Where(p => p != null)
                                  .Select(PersonLine)
                                  .ToList();
                    FPELog.Warn($"[FPE/TRACE] PARTY {(data.isLoading ? "LOAD" : "SAVE")} post: id={__instance.id} state={__instance.state} members=[{Join(members)}]");
                }
            }
        }
    }
}
