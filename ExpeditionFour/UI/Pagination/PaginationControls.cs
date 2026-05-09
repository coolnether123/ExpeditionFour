using System;
using FourPersonExpeditions;
using UnityEngine;

namespace FourPersonExpeditions.UI.Pagination
{
    internal enum PaginationClickBinding
    {
        UIButton,
        UIEventListener
    }

    internal sealed class PaginationArrowButtonOptions
    {
        public string Name;
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public UISprite TemplateSprite;
        public UIAtlas Atlas;
        public string SpriteName;
        public int Width;
        public int Height;
        public int Depth;
        public Color Color = Color.white;
        public Vector3 ColliderSize;
        public Vector3 ColliderCenter = Vector3.zero;
        public bool CopyTemplateCollider;
        public Action OnClick;
        public PaginationClickBinding ClickBinding;
    }

    internal sealed class PaginationControlSet
    {
        public PaginationControlSet(GameObject leftArrow, GameObject rightArrow, UILabel indicator)
        {
            LeftArrow = leftArrow;
            RightArrow = rightArrow;
            Indicator = indicator;
        }

        public GameObject LeftArrow { get; private set; }
        public GameObject RightArrow { get; private set; }
        public UILabel Indicator { get; private set; }

        public void Update(int currentPage, int itemCount, int itemsPerPage, bool hideIndicatorWhenSinglePage)
        {
            int maxPages = PaginationMath.GetMaxPages(itemCount, itemsPerPage);
            int clampedPage = PaginationMath.ClampPage(currentPage, itemCount, itemsPerPage);
            bool hasMultiplePages = maxPages > 1;

            SetActive(LeftArrow, hasMultiplePages && clampedPage > 0);
            SetActive(RightArrow, hasMultiplePages && clampedPage < maxPages - 1);

            if (Indicator != null)
            {
                Indicator.text = string.Format("{0}/{1}", clampedPage + 1, maxPages);
                Indicator.gameObject.SetActive(!hideIndicatorWhenSinglePage || hasMultiplePages);
            }
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }
    }

    internal static class PaginationControlFactory
    {
        public static UILabel CreateLabelIndicator(Transform parent, string name, Vector3 position, int fontSize, int depth, Color color)
        {
            UILabel label = UIHelper.CreateLabel(parent, "1/1", fontSize, TextAnchor.MiddleCenter);
            if (label == null)
            {
                return null;
            }

            label.gameObject.name = name;
            label.transform.localPosition = position;
            label.depth = depth;
            label.color = color;
            return label;
        }

        public static UILabel CreateClonedIndicator(GameObject template, Transform parent, string name, Vector3 position)
        {
            GameObject indicator = UIUtil.CloneAndReposition(template, Vector3.zero, parent);
            if (indicator == null)
            {
                return null;
            }

            indicator.transform.localPosition = position;
            indicator.name = name;
            if (parent != null)
            {
                NGUITools.SetLayer(indicator, parent.gameObject.layer);
            }

            UILabel label = indicator.GetComponent<UILabel>();
            if (label != null)
            {
                label.text = "1/1";
            }

            return label;
        }

        public static GameObject CreateSpriteArrow(PaginationArrowButtonOptions options)
        {
            if (options == null || options.Parent == null)
            {
                return null;
            }

            UIAtlas atlas = options.Atlas;
            string spriteName = options.SpriteName;
            if (options.TemplateSprite != null)
            {
                if (atlas == null) atlas = options.TemplateSprite.atlas;
                if (string.IsNullOrEmpty(spriteName)) spriteName = options.TemplateSprite.spriteName;
            }

            if (atlas == null || string.IsNullOrEmpty(spriteName))
            {
                return null;
            }

            GameObject arrow = new GameObject(string.IsNullOrEmpty(options.Name) ? "FPE_PageArrow" : options.Name);
            arrow.transform.parent = options.Parent;
            arrow.transform.localPosition = options.LocalPosition;
            arrow.transform.localScale = Vector3.one;
            arrow.transform.localRotation = options.LocalRotation;
            arrow.layer = options.Parent.gameObject.layer;

            UISprite sprite = arrow.AddComponent<UISprite>();
            sprite.atlas = atlas;
            sprite.spriteName = spriteName;
            sprite.width = ResolveSize(options.Width, options.TemplateSprite != null ? options.TemplateSprite.width : 0);
            sprite.height = ResolveSize(options.Height, options.TemplateSprite != null ? options.TemplateSprite.height : 0);
            sprite.depth = options.Depth;
            sprite.color = options.Color;

            if (options.TemplateSprite != null)
            {
                sprite.type = options.TemplateSprite.type;
                sprite.flip = options.TemplateSprite.flip;
            }

            BoxCollider collider = arrow.AddComponent<BoxCollider>();
            ApplyCollider(collider, sprite, options);
            BindClick(arrow, options.OnClick, options.ClickBinding);
            arrow.SetActive(true);
            return arrow;
        }

        public static GameObject CreateTextArrow(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            int fontSize,
            int depth,
            Color color,
            Vector3 colliderSize,
            Action onClick,
            PaginationClickBinding clickBinding)
        {
            UILabel label = UIHelper.CreateLabel(parent, text, fontSize, TextAnchor.MiddleCenter);
            if (label == null)
            {
                return null;
            }

            label.gameObject.name = name;
            label.depth = depth;
            label.transform.localPosition = position;
            label.color = color;

            BoxCollider collider = label.gameObject.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = Vector3.zero;

            BindClick(label.gameObject, onClick, clickBinding);
            return label.gameObject;
        }

        private static int ResolveSize(int requestedSize, int templateSize)
        {
            if (requestedSize > 0) return requestedSize;
            return Mathf.Max(1, templateSize);
        }

        private static void ApplyCollider(BoxCollider collider, UISprite sprite, PaginationArrowButtonOptions options)
        {
            if (collider == null || sprite == null || options == null)
            {
                return;
            }

            if (options.ColliderSize != Vector3.zero)
            {
                collider.size = options.ColliderSize;
                collider.center = options.ColliderCenter;
                return;
            }

            BoxCollider templateCollider = null;
            if (options.CopyTemplateCollider && options.TemplateSprite != null)
            {
                templateCollider = options.TemplateSprite.GetComponent<BoxCollider>();
                if (templateCollider == null)
                {
                    templateCollider = options.TemplateSprite.GetComponentInParent<BoxCollider>();
                }
            }

            if (templateCollider != null)
            {
                collider.size = new Vector3(
                    Mathf.Max(1f, Mathf.Abs(templateCollider.size.x)),
                    Mathf.Max(1f, Mathf.Abs(templateCollider.size.y)),
                    Mathf.Max(1f, Mathf.Abs(templateCollider.size.z)));
                collider.center = templateCollider.center;
                return;
            }

            collider.size = new Vector3(Mathf.Max(1f, sprite.width), Mathf.Max(1f, sprite.height), 1f);
            collider.center = Vector3.zero;
        }

        private static void BindClick(GameObject target, Action onClick, PaginationClickBinding clickBinding)
        {
            if (target == null || onClick == null)
            {
                return;
            }

            if (clickBinding == PaginationClickBinding.UIEventListener)
            {
                UIEventListener listener = UIEventListener.Get(target);
                listener.onClick = delegate { onClick(); };
                return;
            }

            UIButton button = target.AddComponent<UIButton>();
            button.tweenTarget = target;
            EventDelegate.Add(button.onClick, new EventDelegate(delegate { onClick(); }));
        }
    }
}
