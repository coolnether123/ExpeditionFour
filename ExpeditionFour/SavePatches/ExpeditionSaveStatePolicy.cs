using System.Collections.Generic;

namespace FourPersonExpeditions.SavePatches
{
    /// <summary>
    /// States whose party members remain away from the shelter across a save.
    /// </summary>
    internal static class ExpeditionSaveStatePolicy
    {
        public static readonly HashSet<ExplorationParty.ePartyState> AwayStates =
            new HashSet<ExplorationParty.ePartyState>
            {
                ExplorationParty.ePartyState.GettingReady,
                ExplorationParty.ePartyState.LeavingShelter,
                ExplorationParty.ePartyState.VehicleLeaving,
                ExplorationParty.ePartyState.Traveling,
                ExplorationParty.ePartyState.ReportingLocation,
                ExplorationParty.ePartyState.ReportingLocationWaitUser,
                ExplorationParty.ePartyState.ReportingDiversionsStart,
                ExplorationParty.ePartyState.ReportingDiversions,
                ExplorationParty.ePartyState.ReportingDiversionsWaitUser,
                ExplorationParty.ePartyState.SearchingLocation,
                ExplorationParty.ePartyState.EncounteredItemsStart,
                ExplorationParty.ePartyState.EncounteredItems,
                ExplorationParty.ePartyState.EncounteredItemsWaitUser,
                ExplorationParty.ePartyState.EncounteredItemsRequestTransferPanel,
                ExplorationParty.ePartyState.EncounteredItemsWaitItemTransfer,
                ExplorationParty.ePartyState.EncounteredNPCsStart,
                ExplorationParty.ePartyState.EncounteredNPCs,
                ExplorationParty.ePartyState.EncounteredNPCsWaitUser,
                ExplorationParty.ePartyState.EncounteredNPCsRequestNewEncounter,
                ExplorationParty.ePartyState.EncounteredNPCsWaitFinished,
                ExplorationParty.ePartyState.EncounteredNPCsAutoResolve,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterStart,
                ExplorationParty.ePartyState.OpenGroundNpcEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitUser,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterRequestNewEncounter,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterWaitFinished,
                ExplorationParty.ePartyState.OpenGroundNpcEncounterAutoResolve,
                ExplorationParty.ePartyState.EncounteredQuestNPCs,
                ExplorationParty.ePartyState.EncounteredQuestNPCsWaitUser,
                ExplorationParty.ePartyState.QuestEncounterStart,
                ExplorationParty.ePartyState.QuestEncounterWaitFinished,
                ExplorationParty.ePartyState.VehicleReturning,
                ExplorationParty.ePartyState.EnteringShelter,
                ExplorationParty.ePartyState.ReturnedShowExperienceGained,
                ExplorationParty.ePartyState.ReturnedRequestTransferPanel,
                ExplorationParty.ePartyState.ReturnedWaitItemTransfer,
                ExplorationParty.ePartyState.HorseLeaving,
                ExplorationParty.ePartyState.HorseReturning,
                ExplorationParty.ePartyState.EnteringShelterNextUpdate,
                ExplorationParty.ePartyState.ReturnedShowHazmatExperienceGained
            };
    }
}
