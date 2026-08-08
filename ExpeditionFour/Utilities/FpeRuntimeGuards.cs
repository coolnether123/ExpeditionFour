using UnityEngine;

namespace FourPersonExpeditions
{
    internal static class FpeRuntimeGuards
    {
        public static bool IsFoodPoisoned(FamilyMember person)
        {
            return person != null
                && person.illness != null
                && person.illness.foodPoisoning != null
                && person.illness.foodPoisoning.isActive;
        }

        public static bool TryGetStasisHazmatSuit(out Obj_HazmatSuit_Stasis suit)
        {
            suit = null;

            if (ObjectManager.Instance == null)
            {
                return false;
            }

            var objects = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.HazmatSuits_Stasis);
            if (objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                suit = objects[i] as Obj_HazmatSuit_Stasis;
                if (suit != null)
                {
                    return true;
                }
            }

            suit = null;
            return false;
        }
    }
}
