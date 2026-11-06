param(
    [string]$SmmRoot = 'A:\Dev\Worktrees\smm-integration\Dist\SMM',
    [string]$ManagedRoot = 'D:\Epic Games Games\Sheltered\ShelteredWindows64_EOS_Data\Managed'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw (Join-Path $root 'ExpeditionFour/LoadoutManager/LoadoutPatches.cs')
$writes = [regex]::Matches($source, 'Safe\.SetField\(panel, "m_page",[\s\S]*?;')
if ($writes.Count -ne 2) { throw 'Expected forward and backward loadout page writes.' }
$nextStart = $source.IndexOf('            int nextSlot = -1;')
$nextEnd = $source.IndexOf('            if (nextSlot != -1)', $nextStart)
$previousStart = $source.IndexOf('        int previousLoadoutSlot = -1;')
$previousEnd = $source.IndexOf('        if (previousLoadoutSlot == -1)', $previousStart)
if ($nextStart -lt 0 -or $nextEnd -le $nextStart -or $previousStart -lt 0 -or $previousEnd -le $previousStart) {
    throw 'Member navigation loops not found.'
}
$next = $source.Substring($nextStart, $nextEnd - $nextStart)
$previous = $source.Substring($previousStart, $previousEnd - $previousStart)

# Use the supplied framework, not a permissive mock of its conversion rules.
$api = Join-Path $SmmRoot 'ModAPI.dll'
$harmony = Join-Path $SmmRoot 'bin/0Harmony.dll'
[void][Reflection.Assembly]::LoadFrom((Join-Path $ManagedRoot 'UnityEngine.dll'))
[void][Reflection.Assembly]::LoadFrom($api)
[void][Reflection.Assembly]::LoadFrom($harmony)
Add-Type -IgnoreWarnings -WarningAction SilentlyContinue -ReferencedAssemblies @($api, $harmony) @"
using HarmonyLib;
using ModAPI.Reflection;
using System.Collections.Generic;
public class PartyLogicFixture {
    public int ActiveLoadoutIndex;
    public int MaxPartySize = 4;
    public List<int> SelectedMemberIndices = new List<int>(new int[] { 0, 1, 2, 3 });
}
public class ExpeditionMainPanelNew {
    private enum eCurrentPage { PartySetup, LoadoutMember1, LoadoutMember2, RouteSetup }
    private eCurrentPage m_page = eCurrentPage.RouteSetup;
    public int SelectedItems;
    public int CurrentPage { get { return (int)m_page; } }
    public void Select() {
        switch (m_page) {
            case eCurrentPage.LoadoutMember1:
            case eCurrentPage.LoadoutMember2: SelectedItems++; break;
        }
    }
}
public static class LoadoutPageFixture {
    public static void Forward(ExpeditionMainPanelNew panel) { $($writes[0].Value) }
    public static void Backward(ExpeditionMainPanelNew panel) { $($writes[1].Value) }
    public static int Next(PartyLogicFixture logic) { $next return nextSlot; }
    public static int Previous(PartyLogicFixture logic) { $previous return previousLoadoutSlot; }
}
"@
$passed = 0
$panel = New-Object ExpeditionMainPanelNew
if ([ModAPI.Reflection.Safe]::SetField($panel, 'm_page', 1)) { throw 'Framework unexpectedly accepts an integer enum write.' }
$panel.Select()
if ($panel.SelectedItems -ne 0 -or $panel.CurrentPage -ne 3) { throw 'Original route-page failure was not reproduced.' }
$passed++
foreach ($direction in @('Forward', 'Backward')) {
    foreach ($slot in 0..3) {
        $panel = New-Object ExpeditionMainPanelNew
        if ($direction -eq 'Forward') { [LoadoutPageFixture]::Forward($panel) }
        else { [LoadoutPageFixture]::Backward($panel) }
        $panel.Select()
        if ($panel.CurrentPage -ne 1 -or $panel.SelectedItems -ne 1) { throw "Select did not reach loadout for $direction slot $slot." }
        $logic = New-Object PartyLogicFixture
        $logic.ActiveLoadoutIndex = $slot
        if ($direction -eq 'Forward') {
            $expected = if ($slot -eq 3) { -1 } else { $slot + 1 }
            if ([LoadoutPageFixture]::Next($logic) -ne $expected) { throw "Incorrect next member from $slot." }
        } else {
            if ([LoadoutPageFixture]::Previous($logic) -ne ($slot - 1)) { throw "Incorrect previous member from $slot." }
        }
        $passed++
    }
}
$logic = New-Object PartyLogicFixture
$logic.SelectedMemberIndices[1] = -1
if ([LoadoutPageFixture]::Next($logic) -ne 2) { throw 'Forward navigation did not skip Nobody.' }
$passed++
$logic.ActiveLoadoutIndex = 2
if ([LoadoutPageFixture]::Previous($logic) -ne 0) { throw 'Backward navigation did not skip Nobody.' }
$passed++
# All members share vanilla grids; retain initialization, selection and delayed transitions.
foreach ($contract in @('loadout.InitializeLoadout(pm, isFirst)',
    'loadout.InitializeLoadout(logic.AllPartyMembers[slot], false)',
    'i < logic.MaxPartySize', 'i = logic.ActiveLoadoutIndex - 1',
    'Safe.InvokeMethod(panel, "DelayedPageChange", pageAction)')) {
    if (-not $source.Contains($contract)) { throw "Missing navigation contract: $contract" }
    $passed++
}
Write-Output "PASS: $passed checks, 0 failed. Real framework enum writes and synthetic vanilla Select dispatch; production member navigation loops and source contracts. Unity input and item operations require live checks."
