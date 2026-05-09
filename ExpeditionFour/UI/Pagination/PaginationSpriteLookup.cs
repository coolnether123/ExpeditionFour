using System;
using UnityEngine;

namespace FourPersonExpeditions.UI.Pagination
{
    internal sealed class PaginationSpriteChoice
    {
        public UIAtlas Atlas;
        public string SpriteName;
    }

    internal static class PaginationSpriteLookup
    {
        private static readonly string[] ControllerSpriteTokens =
        {
            "rb",
            "lb",
            "rt",
            "lt",
            "dpad",
            "button_a",
            "button_b",
            "button_x",
            "button_y"
        };

        public static PaginationSpriteChoice FindInChildrenAtlas(Transform root, string[] preferredNames)
        {
            if (root == null)
            {
                return null;
            }

            UISprite existingSprite = root.GetComponentInChildren<UISprite>(true);
            UIAtlas atlas = existingSprite != null ? existingSprite.atlas : null;
            if (atlas == null)
            {
                return null;
            }

            string spriteName = FindSpriteName(atlas, preferredNames);
            if (string.IsNullOrEmpty(spriteName))
            {
                return null;
            }

            return new PaginationSpriteChoice
            {
                Atlas = atlas,
                SpriteName = spriteName
            };
        }

        public static UISprite FindSpriteAtPathOrFallback(Transform root, string path, GameObject fallback)
        {
            GameObject target = null;
            if (root != null && !string.IsNullOrEmpty(path))
            {
                Transform targetTransform = root.Find(path);
                if (targetTransform != null)
                {
                    target = targetTransform.gameObject;
                }
            }

            if (target == null)
            {
                target = fallback;
            }

            return PickChevron(target);
        }

        public static UISprite PickChevron(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            UISprite[] sprites = target.GetComponentsInChildren<UISprite>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                UISprite sprite = sprites[i];
                if (sprite == null)
                {
                    continue;
                }

                string name = GetComparableName(sprite);
                if (ContainsControllerToken(name))
                {
                    continue;
                }

                if (name.Contains("arrow") || name.Contains("chevron"))
                {
                    return sprite;
                }
            }

            UISprite best = null;
            for (int i = 0; i < sprites.Length; i++)
            {
                UISprite sprite = sprites[i];
                if (sprite == null)
                {
                    continue;
                }

                if (best == null || (sprite.width > best.width && sprite.width != sprite.height))
                {
                    best = sprite;
                }
            }

            return best ?? (sprites.Length > 0 ? sprites[0] : null);
        }

        private static string FindSpriteName(UIAtlas atlas, string[] preferredNames)
        {
            if (atlas == null || atlas.spriteList == null)
            {
                return null;
            }

            if (preferredNames != null)
            {
                for (int preferredIndex = 0; preferredIndex < preferredNames.Length; preferredIndex++)
                {
                    string preferred = preferredNames[preferredIndex];
                    for (int spriteIndex = 0; spriteIndex < atlas.spriteList.Count; spriteIndex++)
                    {
                        UISpriteData sprite = atlas.spriteList[spriteIndex];
                        if (sprite != null && string.Equals(sprite.name, preferred, StringComparison.OrdinalIgnoreCase))
                        {
                            return sprite.name;
                        }
                    }
                }
            }

            for (int spriteIndex = 0; spriteIndex < atlas.spriteList.Count; spriteIndex++)
            {
                UISpriteData sprite = atlas.spriteList[spriteIndex];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                {
                    continue;
                }

                string name = sprite.name.ToLowerInvariant();
                if (name.Contains("arrow") || name.Contains("chevron"))
                {
                    return sprite.name;
                }
            }

            return null;
        }

        private static string GetComparableName(UISprite sprite)
        {
            string name = null;
            if (sprite != null)
            {
                name = sprite.spriteName;
                if (string.IsNullOrEmpty(name))
                {
                    name = sprite.name;
                }
            }

            return (name ?? string.Empty).ToLowerInvariant();
        }

        private static bool ContainsControllerToken(string name)
        {
            for (int i = 0; i < ControllerSpriteTokens.Length; i++)
            {
                if (name.Contains(ControllerSpriteTokens[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
