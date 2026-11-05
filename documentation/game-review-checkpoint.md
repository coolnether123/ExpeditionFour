# Game behavior review checkpoint

The second pass is incomplete. No game was launched or deployed.

## Confirmed save/load defect

Removed the ExplorationParty.SaveLoad postfix that forced away and finishedLeavingShelter flags, cleared both job queues, and repositioned survivors.

Traced in ShelteredCode:
- Character/FamilyMember.cs:SaveLoad saves and restores away and finishedLeaving.
- Expeditions/ExplorationParty.cs:SaveLoad rebuilds PartyMembers through ExplorationManager.AddMemberToParty, then rebuilds the state stack. EnteringShelter calls Begin_EnteringShelter during reconstruction.
- ExplorationParty.Begin_EnteringShelter queues ReturnedFromExpedition movement callbacks and resets m_membersReadyCount.
- Jobs/Job_GoToLocation.cs:BeginJob invokes FamilyMember.OnGoToLocationFinished.
- FamilyMember.OnGoToLocationFinished calls ExplorationManager.OnMemberReturnedToShelter or OnMemberLeftShelter. ExplorationParty.OnShelterEntered and OnShelterLeft increment the readiness count.
- ExplorationParty.Update_LeavingShelter only recreates missing movement jobs when finishedLeavingShelter is false. Forcing that flag true suppresses recovery before readiness reaches the member count.

Clearing restored return jobs removes the callbacks required to finish returning. Forcing departure flags can suppress the jobs needed to finish departure. Vanilla already persists the flags; party size does not require overriding them.

The AddMemberToParty replacement retains the vanilla component creation and registration behavior with an increased capacity. Vanilla SaveLoad iterates its member list, rather than using a fixed two-member serializer.

## Remaining work

Complete the requested inventory and caller traces for setup, selection, costs, launch, loadouts, combat, map paging, summaries, reflection and lifecycle handling. Those systems have not received a complete second-pass audit.

Build submission was refused with `Validated build caller unavailable`. No build or tests ran for this candidate. The host requires its command runner, which conflicts with the requested direct shell build. Resume with a validated caller or a host that permits direct shell gates. Run the supplied Release command and both tests/*.ps1 scripts, report exact counts, and then perform the remaining audit.

Live checks for this fix: save and reload a four-member party while leaving on foot, in a vehicle and on a horse; verify departure completes. Save and reload while walking back into the shelter; verify all return callbacks complete, the summary opens and the party disbands. In Stasis, verify suit jobs survive departure load. Reload a traveling party and verify flags and membership remain correct.
