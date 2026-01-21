using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Reflection;
using FourPersonExpeditions;
using ExpeditionFour.SavePatches;

namespace FourPersonExpeditions.DebugPatches
{
    /// <summary>
    /// Provides additional debug output during the ExplorationManager.SaveLoad process.
    /// This is used to monitor radio state and party member status for troubleshooting.
    /// </summary>
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class ExplorationManager_SaveLoadDebugPatches
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            try
            {
                FPELog.Info($"Debug Trace: ExplorationManager.SaveLoad ({(data?.isLoading == true ? "Loading" : "Saving")}) - Prefix");
                
                float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
                float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);

                Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

                FPELog.Info($"Debug Trace: Radio Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
            }
            catch { /* Debug tracing should never interfere with critical game operations */ }
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            try
            {
                FPELog.Info($"Debug Trace: ExplorationManager.SaveLoad - Postfix");

                float radioWaitTimer = Safe.GetFieldOrDefault(__instance, "m_radioWaitTimer", 0f);
                float radioTimeoutTimer = Safe.GetFieldOrDefault(__instance, "m_radioTimeoutTimer", 0f);
                
                Safe.TryCall(__instance, "GetShelterRadio", out Obj_Radio shelterRadio);
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

                FPELog.Info($"Debug Trace: Radio Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");

                if (Safe.TryGetField(__instance, "m_parties", out Dictionary<int, ExplorationParty> dict) && dict != null)
                {
                    foreach (var kv in dict)
                    {
                        FPELog.Info($"Debug Trace: {FpeDebugFmt.PartyLine(kv.Value)}");
                        
                        if (Safe.TryGetField(kv.Value, "m_partyMembers", out List<PartyMember> memberList) && memberList != null)
                        {
                            foreach (var pm in memberList)
                            {
                                var fm = pm?.person;
                                if (fm != null)
                                {
                                    Vector3? pos = null;
                                    try { if (fm.transform != null) pos = fm.transform.position; } catch { }
                                    FPELog.Info($"Debug Trace: Member={fm.firstName}, isAway={fm.isAway}, left={fm.finishedLeavingShelter}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"Debug Trace: ExplorationManager.SaveLoad trace encounter error: {e}");
            }
        }
    }
}
