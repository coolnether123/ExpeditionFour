# Four Person Expeditions

Version 1.1.5 by Coolnether123.

Four Person Expeditions raises Sheltered's expedition limit from two survivors to four. It extends party setup, loadouts, resource calculations, combat, the expedition map, and summary screens to handle the extra members.

## What the mod changes

- Select up to four survivors for an expedition.
- Configure each survivor's loadout separately.
- Leave optional slots empty with the **Nobody** portrait.
- Scale water, petrol, and Stasis suit battery costs for the selected party.
- Include all selected survivors in combat encounters.
- Page through party members on the expedition map with the on-screen controls.
- Show all party members on exploration, combat, and dialogue summary screens.

## Requirements

- Sheltered Mod Manager 2.0
- ModAPI 2.0
- ShelteredAPI 2.0

## Install the mod

1. Install Sheltered Mod Manager and its required APIs.
2. Download the latest Four Person Expeditions release.
3. Extract the mod folder into the game's `mods` directory.
4. Enable **Four Person Expeditions** in Sheltered Mod Manager.
5. Start the game.

## Start an expedition

Use the radio and create an expedition as usual. The setup panel advances through as many as four survivor slots, followed by one loadout screen for each selected survivor.

## Save compatibility

Vanilla saves can load with the mod enabled. An expedition that already exists keeps its current members until you create a new one.

Before you disable or remove the mod, recall every expedition that has three or four members. Loading such a save without the mod can leave those survivors in an invalid expedition state.

## Known issue

Combatants can overlap slightly when a four-person party enters combat. The overlap is visual and does not change combat rules.

## Build and checks

From a Visual Studio developer shell, build against the installed game's Managed assemblies and the SMM 2.0 framework:

```powershell
msbuild ExpeditionFour/ExpeditionFour.csproj /t:Rebuild /p:Configuration=Release /p:ShelteredSmmRoot=A:\Dev\Worktrees\smm-integration\Dist\SMM /verbosity:minimal
```

The project locates the game's Managed folder, or accepts `/p:ShelteredManagedRoot=<path>`.
Output is `ExpeditionFour/bin/Release/ExpeditionFour.dll`. This command does not install the mod.
If MSBuild reports MSB4242, set `$env:MSBuildEnableWorkloadResolver = 'false'` in that shell and retry.

Run each `tests/Test-*.ps1` in a separate PowerShell process. `Test-ControllerLoadout.ps1` accepts `-SmmRoot` and `-ManagedRoot` for alternate reference locations. It uses the real framework with a synthetic panel to check page conversion and selection dispatch. It does not run Unity input or transfer real items.

## License

Four Person Expeditions is available under the [MIT License](LICENSE).
