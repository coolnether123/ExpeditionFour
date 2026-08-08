using System;
using UnityEngine;

namespace FourPersonExpeditions
{
    internal static class FpeRuntimeGuards
    {
        public static bool IsStasisMode()
        {
            var gameModeManager = GameModeManager.instance;
            return (UnityEngine.Object)gameModeManager != (UnityEngine.Object)null
                && gameModeManager.currentGameMode == GameModeManager.GameMode.Stasis;
        }

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

            try
            {
                var objectManager = ObjectManager.Instance;
                if ((UnityEngine.Object)objectManager == (UnityEngine.Object)null)
                {
                    return false;
                }

                var objects = objectManager.GetObjectsOfType(ObjectManager.ObjectType.HazmatSuits_Stasis);
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
            }
            catch (Exception ex)
            {
                FPELog.Warn("Stasis hazmat lookup failed: " + ex.Message);
            }

            suit = null;
            return false;
        }
    }
}
