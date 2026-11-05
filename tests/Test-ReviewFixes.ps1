$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw (Join-Path $root 'ExpeditionFour/CreateTeam/PanelPatches.cs')
$start = $source.IndexOf('var oldSelections =')
$end = $source.IndexOf('        FPELog.Debug($"OnSelect Patch: Stored selection.')
if ($start -lt 0 -or $end -le $start) { throw 'Selection transaction not found.' }
$transaction = $source.Substring($start, $end - $start).Replace('ValidateSelection(__instance, logic)', 'reject')
Add-Type @"
using System.Collections.Generic;
public static class Safe {
    public static void SetField(SelectionFixture panel, string name, bool value) {
        if (name != "m_routeIsDirty") throw new System.Exception("Unexpected field");
        panel.RouteIsDirty = value;
    }
}
public class SelectionFixture {
    public bool RouteIsDirty;
    public List<int> SelectedMemberIndices = new List<int>();
    public List<int> HighlightedIndices = new List<int>();
    public static bool Select(SelectionFixture logic, int activeSlot, int index, bool reject) {
        var __instance = logic;
        $transaction
        return true;
    }
}
"@
$passed = 0
function Check-Selection($initial, $slot, $index, $reject, $expected, $highlights) {
    $fixture = New-Object SelectionFixture
    $fixture.SelectedMemberIndices.AddRange([int[]]$initial)
    $fixture.HighlightedIndices.AddRange([int[]]@(0,1,2,3))
    [void][SelectionFixture]::Select($fixture, $slot, $index, $reject)
    if (($fixture.SelectedMemberIndices -join ',') -ne ($expected -join ',')) { throw 'Incorrect selected members.' }
    if (($fixture.HighlightedIndices -join ',') -ne ($highlights -join ',')) { throw 'Incorrect highlights.' }
    if ($fixture.RouteIsDirty -ne (-not $reject)) { throw 'Only accepted selections should invalidate route costs.' }
    $script:passed++
}
Check-Selection @(0,1,2,-1) 1 -1 $false @(0,-1,-1,-1) @(0,1,-1,-1)
Check-Selection @(0,1,2,3) 2 -1 $false @(0,1,-1,-1) @(0,1,2,-1)
Check-Selection @(0,1,2,3) 1 -1 $true @(0,1,2,3) @(0,1,2,3)
Check-Selection @(0,1,2,3) 1 4 $false @(0,4,2,3) @(0,1,2,3)
Check-Selection @(0,1,2,3) 0 -1 $false @(-1,-1,-1,-1) @(0,-1,-1,-1)
$repair = Get-Content -Raw (Join-Path $root 'ExpeditionFour/SavePatches/PatchBrokenSaves.cs')
if ($repair -match 'AddMemberToParty|\.person\s*=|\.isAway\s*=') { throw 'Orphan reporting must not mutate membership or away flags.' }
if ($repair -notmatch 'party ownership is unknown') { throw 'Missing orphan diagnostic.' }
$passed++
Write-Output "PASS: $passed checks, 0 failed. Selection uses the extracted production transaction; orphan protection is a source contract."
