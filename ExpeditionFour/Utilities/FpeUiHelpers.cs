using System;
using System.Text;
using UnityEngine;

namespace FourPersonExpeditions
{
    internal static class UIHelper
    {
        public static GameObject Clone(GameObject template, Transform parent)
        {
            return Clone(template, parent, true);
        }

        public static GameObject Clone(GameObject template, Transform parent, bool stripAnchors)
        {
            if (template == null) return null;

            GameObject clone = UnityEngine.Object.Instantiate(template) as GameObject;
            if (clone == null) return null;

            clone.name = template.name + "_Clone";
            if (parent != null)
            {
                clone.transform.SetParent(parent, false);
                clone.layer = parent.gameObject.layer;
                NGUITools.SetLayer(clone, parent.gameObject.layer);
            }

            clone.transform.localScale = template.transform.localScale;
            clone.transform.localRotation = template.transform.localRotation;

            if (stripAnchors)
            {
                StripAnchors(clone);
            }

            return clone;
        }

        public static void StripAnchors(GameObject go)
        {
            if (go == null) return;

            UIAnchor[] anchors = go.GetComponentsInChildren<UIAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i] != null) UnityEngine.Object.Destroy(anchors[i]);

            UIStretch[] stretches = go.GetComponentsInChildren<UIStretch>(true);
            for (int i = 0; i < stretches.Length; i++)
                if (stretches[i] != null) UnityEngine.Object.Destroy(stretches[i]);

            UIWidget[] widgets = go.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                try
                {
                    widgets[i].SetAnchor((Transform)null);
                }
                catch
                {
                }
            }
        }

        public static void SetChildDepths(Transform root, int baseDepth)
        {
            if (root == null) return;

            UIWidget[] widgets = root.GetComponentsInChildren<UIWidget>(true);
            if (widgets == null || widgets.Length == 0) return;

            int minDepth = int.MaxValue;
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null && widgets[i].depth < minDepth)
                    minDepth = widgets[i].depth;
            }

            if (minDepth == int.MaxValue) return;

            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null)
                    widgets[i].depth = baseDepth + (widgets[i].depth - minDepth);
            }
        }

        public static UILabel CreateLabel(Transform parent, string text, int fontSize)
        {
            return CreateLabel(parent, text, fontSize, TextAnchor.MiddleLeft);
        }

        public static UILabel CreateLabel(Transform parent, string text, int fontSize, TextAnchor anchor)
        {
            if (parent == null) return null;

            GameObject go = new GameObject("FPE_Label");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            UILabel label = go.AddComponent<UILabel>();
            UILabel sample = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sample != null)
            {
                label.bitmapFont = sample.bitmapFont;
                label.trueTypeFont = sample.trueTypeFont;
            }
            else
            {
                label.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            ApplyTextAnchor(label, anchor);
            return label;
        }

        private static void ApplyTextAnchor(UILabel label, TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    label.pivot = UIWidget.Pivot.TopLeft;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
                case TextAnchor.UpperCenter:
                    label.pivot = UIWidget.Pivot.Top;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.UpperRight:
                    label.pivot = UIWidget.Pivot.TopRight;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
                case TextAnchor.MiddleCenter:
                    label.pivot = UIWidget.Pivot.Center;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.MiddleRight:
                    label.pivot = UIWidget.Pivot.Right;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
                case TextAnchor.LowerLeft:
                    label.pivot = UIWidget.Pivot.BottomLeft;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
                case TextAnchor.LowerCenter:
                    label.pivot = UIWidget.Pivot.Bottom;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.LowerRight:
                    label.pivot = UIWidget.Pivot.BottomRight;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
                default:
                    label.pivot = UIWidget.Pivot.Left;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
            }
        }
    }

    internal static class UIUtil
    {
        public static GameObject CloneAndReposition(GameObject template, Vector3 localOffset, Transform parent)
        {
            if (template == null) return null;

            Transform targetParent = parent != null ? parent : template.transform.parent;
            GameObject clone = UIHelper.Clone(template, targetParent, true);
            if (clone == null) return null;

            clone.SetActive(false);
            clone.transform.localPosition = template.transform.localPosition + localOffset;
            clone.SetActive(template.activeSelf);
            return clone;
        }
    }

    internal static class UIDebug
    {
        public static bool Enabled = false;

        public static void TakeSnapshot(GameObject go, string label)
        {
            if (!Enabled || go == null) return;

            StringBuilder builder = new StringBuilder();
            builder.Append("[UIDebug] ");
            builder.Append(label ?? string.Empty);
            builder.Append(" Object=");
            builder.Append(go.name);
            builder.Append(" Layer=");
            builder.Append(go.layer);
            builder.Append(" Active=");
            builder.Append(go.activeInHierarchy);
            builder.Append(" LocalPosition=");
            builder.Append(go.transform.localPosition);
            FPELog.Debug(builder.ToString());
        }
    }
}
