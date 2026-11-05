using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// Contains formatting helpers for debug logging of mod-specific data.
    /// </summary>
    internal static class FpeDebugFmt
    {
        public static string MemberFlags(FamilyMember m)
        {
            if (m == null) return "<null>";
            var pos = m.transform != null ? (Vector3?)m.transform.position : null;
            return $"{m.firstName}(away={m.isAway}, left={m.finishedLeavingShelter}, active={(m.gameObject?.activeSelf ?? false)}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")})";
        }

        public static string PartyLine(ExplorationParty p)
        {
            if (p == null) return "party=<null>";
            var names = new List<string>();
            
            if (Safe.TryGetField(p, "m_partyMembers", out List<PartyMember> list) && list != null)
            {
                foreach (var pm in list)
                {
                    var fm = pm?.person;
                    names.Add(fm != null ? MemberFlags(fm) : "<null>");
                }
            }
            return $"party#{p.id} state={p.state} members=[{string.Join(", ", names.ToArray())}]";
        }
    }

    /// <summary>
    /// Provides detailed tracing for ExplorationManager SaveLoad operations when debug mode is enabled.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class Trace_ExplorationManager_SaveLoad
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Debug($"ExplorationManager.SaveLoad Trace ({(data?.isLoading == true ? "Loading" : "Saving")})");
            
            float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
            float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
            
            Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
            bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

            FPELog.Debug($"Radio State (Prefix): Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
                float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
                
                Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;
                
                RadioDialogPanel panel = Safe.GetFieldOrDefault<RadioDialogPanel>(__instance, "m_radioDialogPanel", null);
                bool radioDialogPanelShowing = panel != null && panel.IsShowing();
                bool anyPartiesCallingIn = __instance.AnyPartiesCallingIn();

                FPELog.Debug($"ExplorationManager State (Postfix): Timers=[{radioWaitTimer}/{radioTimeoutTimer}], Incoming=[{incomingTransmission}], DialogShowing=[{radioDialogPanelShowing}], PartiesCalling=[{anyPartiesCallingIn}]");

                if (Safe.TryGetField(__instance, "m_parties", out Dictionary<int, ExplorationParty> partiesDict) && partiesDict != null)
                {
                    foreach (var kv in partiesDict)
                    {
                        var party = kv.Value;
                        FPELog.Debug($"Party #{party.id} State: State={party.state}, Recalled={party.isRecalled}, Returning={party.isReturning}, WalkingToShelter={party.isWalkingToShelter}");

                        ExplorationManager.RadioDialogParams radioParams = Safe.GetFieldOrDefault<ExplorationManager.RadioDialogParams>(party, "m_radioParams", null);
                        if (radioParams != null)
                        {
                            FPELog.Debug($"Radio Params: Msg='{radioParams.questionTextId}', Caller='{radioParams.caller?.firstName}', Receiver='{radioParams.receiver?.firstName}'");
                        }
                        
                        var radioResponse = Safe.GetFieldOrDefault(party, "m_radioResponse", default(RadioDialogPanel.RadioResponse));
                        FPELog.Debug($"Radio Response: {radioResponse}");

                        if (Safe.TryGetField(party, "m_partyMembers", out List<PartyMember> memberList) && memberList != null)
                        {
                            foreach (var pm in memberList)
                            {
                                var fm = pm?.person;
                                if (fm != null)
                                {
                                    var pos = fm.transform != null ? (Vector3?)fm.transform.position : null;
                                    FPELog.Debug($"Party Member: {fm.firstName} (isAway={fm.isAway}, left={fm.finishedLeavingShelter}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")}, jobs={fm.job_queue?.size}, ai={fm.ai_queue?.size})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"ExplorationManager.SaveLoad trace error: {e}");
            }
        }
    }

    /// <summary>
    /// Logs when a party is recalled to the shelter.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationParty), nameof(ExplorationParty.RecallToShelter))]
    internal static class Trace_Recall
    {
        static void Postfix(ExplorationParty __instance)
        {
            if (!FpeDebug.Enabled) return;
            FPELog.Debug($"Party Recall Trace: {FpeDebugFmt.PartyLine(__instance)}");
        }
    }

    /// <summary>
    /// Traces the beginning of GoToLocation jobs to monitor character movement across state boundaries.
    /// </summary>
    [HarmonyPatch(typeof(Job_GoToLocation), "BeginJob")]
    internal static class Trace_GoToLocation_Begin
    {
        static void Postfix(Job_GoToLocation __instance)
        {
            if (!FpeDebug.Enabled) return;
            try
            {
                if (!Safe.TryGetField(__instance, "character", out FamilyMember ch) || ch == null) return;
                if (!Safe.TryGetField(__instance, "m_callbackAction", out object action)) return;
                
                FPELog.Debug($"Job Trace: GoToLocation.BeginJob for {ch.firstName}, Callback={action}");
            }
            catch { /* best effort trace */ }
        }
    }
}
