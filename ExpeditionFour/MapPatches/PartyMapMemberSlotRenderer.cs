using FourPersonExpeditions;
using ModAPI.Reflection;
using UnityEngine;

namespace FourPersonExpeditions.MapPatches
{
    internal static class PartyMapMemberSlotRenderer
    {
        public static void Update(PartyMapPanel panel, int slotIndex, PartyMember member)
        {
            try
            {
                string suffix = slotIndex == 0 ? "1" : "2";
                GameObject memberObject = GetMemberObject(panel, suffix);

                if (member == null || member.person == null || memberObject == null)
                {
                    if (memberObject != null)
                    {
                        memberObject.SetActive(false);
                    }

                    return;
                }

                memberObject.SetActive(true);
                var person = member.person;

                object imageObject;
                if (Safe.TryGetField(panel, "m_member" + suffix + "Image", out imageObject))
                {
                    UI2DSprite image = imageObject as UI2DSprite;
                    if (image != null)
                    {
                        person.ColorizeAvatarSprite(image);
                    }
                }

                object nameObject;
                if (Safe.TryGetField(panel, "m_member" + suffix + "Name", out nameObject))
                {
                    UILabel nameLabel = nameObject as UILabel;
                    if (nameLabel != null)
                    {
                        nameLabel.text = person.firstName;
                    }
                }

                object healthBarObject;
                if (Safe.TryGetField(panel, "m_member" + suffix + "HealthBar", out healthBarObject))
                {
                    UIProgressBar healthBar = healthBarObject as UIProgressBar;
                    if (healthBar != null && person.maxHealth > 0)
                    {
                        healthBar.value = (float)person.health / person.maxHealth;
                    }
                }

                object bleedObject;
                if (Safe.TryGetField(panel, "m_member" + suffix + "BleedIcon", out bleedObject))
                {
                    GameObject bleedIcon = bleedObject as GameObject;
                    if (bleedIcon != null)
                    {
                        bleedIcon.SetActive(person.illness != null
                            && person.illness.bleeding != null
                            && person.illness.bleeding.isActive);
                    }
                }
            }
            catch (System.Exception ex)
            {
                FPELog.Debug(string.Format("UpdateMemberSlot Slot {0} error: {1}", slotIndex, ex.Message));
            }
        }

        private static GameObject GetMemberObject(PartyMapPanel panel, string suffix)
        {
            object value;
            if (!Safe.TryGetField(panel, "member" + suffix, out value))
            {
                return null;
            }

            return value as GameObject;
        }
    }
}
