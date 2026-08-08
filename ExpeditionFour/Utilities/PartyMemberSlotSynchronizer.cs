using UnityEngine;
using ModAPI.Reflection;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Keeps the party's PartyMember components and the setup controller in the
    /// same slot order. The vanilla panel creates only two components, while
    /// the four-person UI can require all four during loadout and save flows.
    /// </summary>
    internal static class PartyMemberSlotSynchronizer
    {
        public static bool EnsureSlots(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
        {
            if (panel == null || logic == null || ExplorationManager.Instance == null)
                return false;

            int partyId;
            if (!Safe.TryGetField(panel, "m_partyId", out partyId))
                return false;

            var party = ExplorationManager.Instance.GetParty(partyId);
            if (party == null)
                return false;

            int requiredSlots = 0;
            for (int i = 0; i < logic.MaxPartySize && i < logic.SelectedMemberIndices.Count; i++)
            {
                if (logic.SelectedMemberIndices[i] != -1)
                    requiredSlots = i + 1;
            }

            requiredSlots = Mathf.Max(requiredSlots, 2);

            var components = party.GetComponents<PartyMember>();
            for (int i = components.Length; i < requiredSlots; i++)
            {
                var added = ExplorationManager.Instance.AddMemberToParty(partyId);
                if (added == null)
                {
                    FPELog.Warn(string.Format(
                        "[FPE] PartyMemberSlotSynchronizer: failed to add slot {0} for party {1}.",
                        i,
                        partyId));
                    break;
                }
            }

            components = party.GetComponents<PartyMember>();
            logic.AllPartyMembers.Clear();
            logic.AllPartyMembers.AddRange(components);
            SyncVanillaMemberFields(panel, logic);

            return logic.AllPartyMembers.Count >= requiredSlots;
        }

        public static void SyncVanillaMemberFields(ExpeditionMainPanelNew panel, FourPersonPartyLogic logic)
        {
            if (panel == null || logic == null)
                return;

            Safe.SetField(
                panel,
                "m_partyMember1",
                logic.AllPartyMembers.Count > 0 ? logic.AllPartyMembers[0] : null);
            Safe.SetField(
                panel,
                "m_partyMember2",
                logic.AllPartyMembers.Count > 1 ? logic.AllPartyMembers[1] : null);
        }
    }
}
