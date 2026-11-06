# Controller loadout routing

The loadout page writes passed an integer to a private enum field. With the supplied SMM 2.0 framework, `Safe.SetField` returns false for that conversion. The screen becomes visible but the main panel stays on RouteSetup. Controller selection never reaches the inventory. This proves a mod defect; the reported return to the map has not been reproduced live.

## Vanilla path

Paths below are relative to `A:/Dev/GitHub/Coolnether123/Sheltered/shelteredmodmanager-Decompiled/ShelteredCode`.

- `Platform/PlatformInput.cs:36` selects the Xbox input implementation for gamepad input. `Platform/PlatformInput_XBoxOne.cs:103` maps UIselect to joystick button 0, the A button. The action is named UIselect in this game, rather than UIaccept.
- `Managers/UIPanelManager.cs:397` handles keyboard cancellation separately. Lines 399 to 404 dispatch gamepad UIselect and UIcancel releases to the current panel's OnSelect and OnCancel.
- `UI/ExpeditionMainPanelNew.cs:724` sets PartySetup, and `:738` sets RouteSetup. Vanilla `ShowMember1Loadout` initializes the grids at `:761` and assigns the typed LoadoutMember1 enum at `:762`. Member two uses LoadoutMember2 at `:779`.
- `UI/ExpeditionMainPanelNew.cs:893` dispatches OnSelect by page. Only the loadout cases forward to `ExpeditionLoadout.OnSelect` at `:911`. RouteSetup has no selection case. OnCancel at `:916` returns from the first loadout to the map, from the second to the first, and from the map to party selection. OnExtra1 at `:953` advances loadouts or confirms the route.
- `Expeditions/ExpeditionLoadout.cs:75` initializes storage callbacks and `:76` selects the first storage button. `UI/ItemGrid.cs:218` sets UICamera.selectedObject. Grid button selection reaches `ItemGrid.OnItemSelected` at `:664`, then `ExpeditionLoadout.OnItemSelected` at `:266`, storing the selected grid, type and slot. `UI/UIKeyNavigation.cs:107` handles directional navigation.
- `UI/UICamera.cs:785` processes controller submit through its configured submitKey and selected object. `UI/BasePanel.cs:44` restores selection after a popup. The panel-level UIselect dispatch above is the route that depends on the expedition page.
- `Expeditions/ExpeditionLoadout.cs:127` handles selection, opens the equip/transfer context menu at `:154`, or transfers ordinary items at `:159`. Mouse clicks reach this method directly through `OnItemClicked` at `:214`, bypassing the main panel's page dispatch. This explains why mouse clicks still work.

## Mod change and correction

`ExpeditionFour/LoadoutManager/LoadoutPatches.cs:135` replaces the two-member transition with sequential initialization for each selected member. It reuses the same vanilla grids and navigation. The writes at `:194` and `:304` now construct the actual private LoadoutMember1 enum before calling Safe.SetField. Previously both passed integer 1. Keeping all four members on this page preserves vanilla selection dispatch while the mod tracks the displayed member in ActiveLoadoutIndex. Forward and backward navigation retain InitializeLoadout and DelayedPageChange.

The framework source at `A:/Dev/Worktrees/smm-integration/ModAPI/Reflection/Safe.cs:103` tries manual conversion and then Convert.ChangeType. `:323` supports assignable types and vector conversions, but no integer-to-enum conversion. The regression check also exercises the supplied DLL directly, so this conclusion does not rely solely on matching framework source.

No input bindings, click handlers, focus order, grids or item transfer behavior changed. About.json, assembly metadata and the README now use 1.1.5. The Release DLL reports 1.1.5.0.

## Verification and remaining gate

Release rebuild passed against `A:/Dev/Worktrees/smm-integration/Dist/SMM` and the project's Epic Managed references. All four test scripts passed: 16 controller checks, 26 resource checks, 6 selection/save checks and 9 recovery contracts. The controller check uses the actual framework DLL and extracted production page writes with a synthetic panel, plus extracted member navigation loops. It proves enum conversion, selection dispatch and member order, not Unity navigation or item operations. Full local logs are in the ignored `logs` folder.

Live check with a controller: choose four survivors while leaving someone home, plot a valid route, then enter loadout. For each survivor, use directional navigation to reach storage, carried inventory and each equipment slot. Press A to add an ordinary item, open an equippable item's menu, equip it, and remove or transfer it again. Use Next Person through all four members, then B back through members four, three, two and one to the map. Reenter and send the party. Repeat with two members and with an optional Nobody slot. Repeat the item operations and navigation with mouse and keyboard. A must not change expedition sections while selecting an item. No game was launched or installed during this change.
