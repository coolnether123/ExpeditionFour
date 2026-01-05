using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

internal static class MMLogger
{
    public static void Log(string msg)
    {
        Debug.Log($"[FourPersonExpeditions] {msg}");
    }
}

internal static class FpeDebug
{
    // Toggle via code; keep false in release builds.
    public static bool Enabled = false;
}

internal static class FPELog
{
    // Info-level logs respect the debug toggle to keep release build output clean
    public static void Info(string msg)
    {
        if (!FpeDebug.Enabled) return;
        MMLog.Write("[FourPersonExpeditions] " + msg);
    }

    // Warnings always print; used for state reconciliation or potential issues
    public static void Warn(string msg)
    {
        MMLog.Write("[FourPersonExpeditionsWarning] " + msg);
    }
}

internal static class UICloneUtil
{
    public static ExpeditionPartySetup.MemberAvatar CloneAvatar(
        ExpeditionPartySetup.MemberAvatar src,
        Transform defaultParent = null
    )
    {
        var dst = new ExpeditionPartySetup.MemberAvatar();
        if (src == null) return dst;

        if (src.background != null)
        {
            var go = UnityEngine.Object.Instantiate(src.background.gameObject) as GameObject;
            go.name = src.background.gameObject.name + "_Clone";
            var p = defaultParent ?? src.background.transform.parent;
            go.transform.SetParent(p, false);
            go.transform.localScale = src.background.transform.localScale;
            go.transform.localRotation = src.background.transform.localRotation;
            go.transform.localPosition = src.background.transform.localPosition;
            dst.background = go.GetComponent<UISprite>();
            StripAnchors(go);
        }
        if (src.polaroid != null)
        {
            var go = UnityEngine.Object.Instantiate(src.polaroid.gameObject) as GameObject;
            go.name = src.polaroid.gameObject.name + "_Clone";
            var p = defaultParent ?? src.polaroid.transform.parent;
            go.transform.SetParent(p, false);
            go.transform.localScale = src.polaroid.transform.localScale;
            go.transform.localRotation = src.polaroid.transform.localRotation;
            go.transform.localPosition = src.polaroid.transform.localPosition;
            dst.polaroid = go.GetComponent<UISprite>();
            StripAnchors(go);
        }
        if (src.avatar != null)
        {
            var go = UnityEngine.Object.Instantiate(src.avatar.gameObject) as GameObject;
            go.name = src.avatar.gameObject.name + "_Clone";
            var p = defaultParent ?? src.avatar.transform.parent;
            go.transform.SetParent(p, false);
            go.transform.localScale = src.avatar.transform.localScale;
            go.transform.localRotation = src.avatar.transform.localRotation;
            go.transform.localPosition = src.avatar.transform.localPosition;
            dst.avatar = go.GetComponent<UI2DSprite>();
            StripAnchors(go);
        }
        if (src.name != null)
        {
            var go = UnityEngine.Object.Instantiate(src.name.gameObject) as GameObject;
            go.name = src.name.gameObject.name + "_Clone";
            var p = defaultParent ?? src.name.transform.parent;
            go.transform.SetParent(p, false);
            go.transform.localScale = src.name.transform.localScale;
            go.transform.localRotation = src.name.transform.localRotation;
            go.transform.localPosition = src.name.transform.localPosition;
            dst.name = go.GetComponent<UILabel>();
            StripAnchors(go);
        }

        return dst;
    }

    public static void OffsetAvatar(ExpeditionPartySetup.MemberAvatar avatar, Vector3 targetLocalPosOfBackground, ExpeditionPartySetup.MemberAvatar reference)
    {
        if (avatar == null) return;
        Vector3 refBg = reference != null && reference.background != null ? reference.background.transform.localPosition : Vector3.zero;
        Vector3 delta = targetLocalPosOfBackground - refBg;

        if (avatar.background != null) avatar.background.transform.localPosition = (reference.background != null ? reference.background.transform.localPosition : Vector3.zero) + delta;
        if (avatar.polaroid  != null) avatar.polaroid.transform.localPosition  = (reference.polaroid  != null ? reference.polaroid.transform.localPosition  : Vector3.zero) + delta;
        if (avatar.avatar    != null) avatar.avatar.transform.localPosition    = (reference.avatar    != null ? reference.avatar.transform.localPosition    : Vector3.zero) + delta;
        if (avatar.name      != null) avatar.name.transform.localPosition      = (reference.name      != null ? reference.name.transform.localPosition      : Vector3.zero) + delta;
    }

    private static void StripAnchors(GameObject go)
    {
        var anchor = go.GetComponent<UIAnchor>();
        if (anchor != null) UnityEngine.Object.Destroy(anchor);
        var stretch = go.GetComponent<UIStretch>();
        if (stretch != null) UnityEngine.Object.Destroy(stretch);
        var widget = go.GetComponent<UIWidget>();
        if (widget != null)
        {
            try { widget.SetAnchor((Transform)null); } catch { }
        }
    }

    public static void SetAvatarActive(ExpeditionPartySetup.MemberAvatar avatar, bool active)
    {
        if (avatar == null) return;
        if (avatar.background != null) avatar.background.gameObject.SetActive(active);
        if (avatar.polaroid  != null) avatar.polaroid.gameObject.SetActive(active);
        if (avatar.avatar    != null) avatar.avatar.gameObject.SetActive(active);
        if (avatar.name      != null) avatar.name.gameObject.SetActive(active);
    }
}
