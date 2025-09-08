using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

[HarmonyPatch(typeof(EncounterManager), nameof(EncounterManager.StartManager))]
public static class EncounterManager_StartManager_Patch
{
    public static void Postfix(EncounterManager __instance)
    {
        if (__instance == null) return;
        var tr = Traverse.Create(__instance);
        var list = tr.Field("player_characters").GetValue<List<EncounterCharacter>>();
        if (list == null || list.Count == 0) return;

        int target = Mathf.Max(list.Count, FourPersonConfig.MaxPartySize);
        if (list.Count >= target) return;

        FPELog.Info($"EncounterManager Patch: Expanding player character slots from {list.Count} to {target}.");

        var last = list[list.Count - 1];
        var parent = last.transform.parent;
        Vector3 offset = new Vector3(1f, 0f, 0f);
        if (list.Count >= 2)
        {
            var prev = list[list.Count - 2];
            offset = last.transform.localPosition - prev.transform.localPosition;
            if (offset == Vector3.zero) offset = new Vector3(1f, 0f, 0f);
        }

        Vector3 basePos = last.transform.localPosition;
        for (int i = list.Count; i < target; i++)
        {
            var go = UnityEngine.Object.Instantiate(last.gameObject) as GameObject;
            go.name = last.gameObject.name + "_Clone_" + i;
            go.transform.SetParent(parent, false);
            go.transform.localScale = last.transform.localScale;
            go.transform.localRotation = last.transform.localRotation;
            go.transform.localPosition = basePos + offset * (i - (list.Count - 1));
            var enc = go.GetComponent<EncounterCharacter>();
            if (enc != null)
            {
                enc.Initialise();
                go.SetActive(false);
                list.Add(enc);
            }
        }
    }
}

[HarmonyPatch(typeof(EncounterCombatPanel), "Awake")]
public static class EncounterCombatPanel_Awake_Patch
{
    public static void Postfix(EncounterCombatPanel __instance)
    {
        if (__instance == null) return;
        var tr = Traverse.Create(__instance);
        var bars = tr.Field("mini_health_bars").GetValue<List<MiniHealthBar>>();
        var root = tr.Field("mini_healthbar_root").GetValue<Transform>();
        if (bars == null || root == null) return;
        if (bars.Count == 0) return;

        int currentBars = bars.Count;
        int neededExtra = Mathf.Max(0, FourPersonConfig.MaxPartySize * 2 - currentBars); // *2 for players and enemies
        if (neededExtra == 0) return;

        FPELog.Info($"EncounterCombatPanel Patch: Expanding mini health bars from {currentBars} to {currentBars + neededExtra}.");

        var last = bars[bars.Count - 1];
        var lastTf = last.transform;
        Vector3 offset = new Vector3(80f, 0f, 0f);
        if (bars.Count >= 2)
        {
            var prevTf = bars[bars.Count - 2].transform;
            offset = lastTf.localPosition - prevTf.localPosition;
            if (offset == Vector3.zero) offset = new Vector3(80f, 0f, 0f);
        }
        Vector3 basePos = lastTf.localPosition;

        for (int i = 0; i < neededExtra; i++)
        {
            var cloneGo = UnityEngine.Object.Instantiate(last.gameObject) as GameObject;
            cloneGo.name = last.gameObject.name + "_Clone_" + (bars.Count + i);
            cloneGo.transform.SetParent(root, false);
            cloneGo.transform.localScale = lastTf.localScale;
            cloneGo.transform.localRotation = lastTf.localRotation;
            cloneGo.transform.localPosition = basePos + offset * (i + 1);
            var bar = cloneGo.GetComponent<MiniHealthBar>();
            if (bar != null)
            {
                bar.SetInactive();
                bars.Add(bar);
            }
        }
    }
}

[HarmonyPatch(typeof(EncounterCombatPanel), nameof(EncounterCombatPanel.OnShow))]
public static class EncounterCombatPanel_OnShow_Patch
{
    public static void Postfix(EncounterCombatPanel __instance)
    {
        if (__instance == null) return;
        var tr = Traverse.Create(__instance);
        var initiative = tr.Field("initiative_list").GetValue<List<EncounterCharacter>>();
        var players = tr.Field("player_characters").GetValue<List<EncounterCharacter>>();
        var npcs = tr.Field("npc_characters").GetValue<List<EncounterCharacter>>();
        if (initiative == null || players == null || npcs == null) return;
        int expected = players.Count + npcs.Count;
        if (initiative.Count != expected)
        {
            Debug.LogWarning($"[FourPersonExpeditions] Initiative count mismatch. expected={expected} actual={initiative.Count}");
        }
    }
}
