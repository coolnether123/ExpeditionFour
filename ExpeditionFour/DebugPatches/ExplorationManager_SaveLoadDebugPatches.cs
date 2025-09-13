using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using ExpeditionFour.SavePatches;

namespace FourPersonExpeditions.DebugPatches
{
    [HarmonyPatch(typeof(ExplorationManager), nameof(ExplorationManager.SaveLoad))]
    internal static class ExplorationManager_SaveLoadDebugPatches
    {
        static void Prefix(ExplorationManager __instance, SaveData data)
        {
            try
            {
                FPELog.Info($"[FPE/TRACE] ExplorationManager.SaveLoad({(data?.isLoading == true ? "loading" : "saving")})");
                var tr = Traverse.Create(__instance);
                float radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
                float radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();

                // Safe access to private radio object
                Obj_Radio shelterRadio = null;
                try { shelterRadio = Traverse.Create(__instance).Method("GetShelterRadio").GetValue<Obj_Radio>(); }
                catch { /* best effort */ }
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

                FPELog.Info($"[FPE/TRACE]   Radio State (Prefix): Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");
            }
            catch { /* tracing should never break load */ }
        }

        static void Postfix(ExplorationManager __instance, SaveData data)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                float radioWaitTimer = tr.Field("m_radioWaitTimer").GetValue<float>();
                float radioTimeoutTimer = tr.Field("m_radioTimeoutTimer").GetValue<float>();
                
                // Use Traverse to call GetShelterRadio, as it's private
                Obj_Radio shelterRadio = null;
                try { shelterRadio = Traverse.Create(__instance).Method("GetShelterRadio").GetValue<Obj_Radio>(); }
                catch { /* best effort */ }
                bool incomingTransmission = (UnityEngine.Object)shelterRadio != (UnityEngine.Object)null && shelterRadio.incomingTransmission;

                FPELog.Info($"[FPE/TRACE]   Radio State (Postfix): Wait={radioWaitTimer}, Timeout={radioTimeoutTimer}, Incoming={incomingTransmission}");

                var dict = AccessTools.Field(typeof(ExplorationManager), "m_parties")
                                      .GetValue(__instance) as Dictionary<int, ExplorationParty>;
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        FPELog.Info($"[FPE/TRACE] {FpeDebugFmt.PartyLine(kv.Value)}");
                        // Log individual FamilyMember positions and flags
                        var memberList = AccessTools.Field(typeof(ExplorationParty), "m_partyMembers")
                                                    .GetValue(kv.Value) as List<PartyMember>;
                        if (memberList != null)
                        {
                            foreach (var pm in memberList)
                            {
                                var fm = pm?.person;
                                if (fm != null)
                                {
                                    Vector3? pos = null;
                                    try { if (fm.transform != null) pos = fm.transform.position; } catch { }
                                    FPELog.Info($"[FPE/TRACE]     Member: {fm.firstName} (isAway={fm.isAway}, finishedLeavingShelter={fm.finishedLeavingShelter}, pos={(pos.HasValue ? pos.Value.ToString() : "n/a")})");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                FPELog.Warn($"[FPE/TRACE] ExplorationManager.SaveLoad post-trace error: {e}");
            }
        }
    }
}
