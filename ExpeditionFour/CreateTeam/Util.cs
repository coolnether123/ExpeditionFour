using ModAPI.UI;
using UnityEngine;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Wraps ModAPI.UI.UIHelper for convenience.
    /// Most methods now delegate to UIHelper.
    /// </summary>
    internal static class UICloneUtil
    {
        // This method is CUSTOM to FPE (not in ModAPI) - KEEP IT
        public static ExpeditionPartySetup.MemberAvatar CloneAvatar(
            ExpeditionPartySetup.MemberAvatar src,
            Transform defaultParent = null)
        {
            var dst = new ExpeditionPartySetup.MemberAvatar();
            if (src == null) return dst;

            // Reuse UIHelper.Clone for GameObject cloning
            if (src.background != null)
            {
                var go = UIHelper.Clone(src.background.gameObject, 
                    defaultParent ?? src.background.transform.parent, 
                    stripAnchors: true);
                go.name = src.background.gameObject.name + "_Clone";
                dst.background = go.GetComponent<UISprite>();
            }
            
            if (src.polaroid != null)
            {
                var go = UIHelper.Clone(src.polaroid.gameObject, 
                    defaultParent ?? src.polaroid.transform.parent, 
                    stripAnchors: true);
                go.name = src.polaroid.gameObject.name + "_Clone";
                dst.polaroid = go.GetComponent<UISprite>();
            }
            
            if (src.avatar != null)
            {
                var go = UIHelper.Clone(src.avatar.gameObject, 
                    defaultParent ?? src.avatar.transform.parent, 
                    stripAnchors: true);
                go.name = src.avatar.gameObject.name + "_Clone";
                dst.avatar = go.GetComponent<UI2DSprite>();
            }
            
            if (src.name != null)
            {
                var go = UIHelper.Clone(src.name.gameObject, 
                    defaultParent ?? src.name.transform.parent, 
                    stripAnchors: true);
                go.name = src.name.gameObject.name + "_Clone";
                dst.name = go.GetComponent<UILabel>();
            }

            return dst;
        }

        // This method is CUSTOM to FPE - KEEP IT
        public static void OffsetAvatar(
            ExpeditionPartySetup.MemberAvatar avatar, 
            Vector3 targetLocalPosOfBackground, 
            ExpeditionPartySetup.MemberAvatar reference)
        {
            if (avatar == null) return;
            Vector3 refBg = reference != null && reference.background != null 
                ? reference.background.transform.localPosition 
                : Vector3.zero;
            Vector3 delta = targetLocalPosOfBackground - refBg;

            if (avatar.background != null) 
                avatar.background.transform.localPosition = 
                    (reference?.background != null ? reference.background.transform.localPosition : Vector3.zero) + delta;
            if (avatar.polaroid != null) 
                avatar.polaroid.transform.localPosition = 
                    (reference?.polaroid != null ? reference.polaroid.transform.localPosition : Vector3.zero) + delta;
            if (avatar.avatar != null) 
                avatar.avatar.transform.localPosition = 
                    (reference?.avatar != null ? reference.avatar.transform.localPosition : Vector3.zero) + delta;
            if (avatar.name != null) 
                avatar.name.transform.localPosition = 
                    (reference?.name != null ? reference.name.transform.localPosition : Vector3.zero) + delta;
        }

        // REPLACE with ModAPI's UIHelper.StripAnchors
        public static void StripAnchors(GameObject go) => UIHelper.StripAnchors(go);

        // This method is CUSTOM to FPE - KEEP IT
        public static void SetAvatarActive(ExpeditionPartySetup.MemberAvatar avatar, bool active)
        {
            if (avatar == null) return;
            if (avatar.background != null) avatar.background.gameObject.SetActive(active);
            if (avatar.polaroid != null) avatar.polaroid.gameObject.SetActive(active);
            if (avatar.avatar != null) avatar.avatar.gameObject.SetActive(active);
            if (avatar.name != null) avatar.name.gameObject.SetActive(active);
        }
    }
}
