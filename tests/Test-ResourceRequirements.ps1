$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw (Join-Path $root 'ExpeditionFour/ExpeditionFourPatches.cs')
$start = $source.IndexOf('            int selectedCount =')
$end = $source.IndexOf('            // Update UI Labels', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Resource calculation not found.' }
$calculation = $source.Substring($start, $end - $start)
Add-Type @"
using System;
using System.Linq;
using System.Collections.Generic;
public class Logic { public List<int> SelectedMemberIndices = new List<int>(); }
public class Panel { public float m_routeDistance, m_waterRequired; public int m_petrolRequired; public bool useVehicle, useHorse; public Obj_CamperVan m_vehicle = new Obj_CamperVan(); }
public class Obj_CamperVan { public float PetrolPerPersonPerMile = 0.5f; }
public class ExplorationManager {
    public static ExplorationManager Instance = new ExplorationManager();
    public float worldUnitsPerMile = 10f, m_batteryPerMile = 3f, waterPerPersonPerMile = 2f, RVWaterModifier = 0.5f, HorseWaterModifier = 0.75f;
}
public static class FpeRuntimeGuards { public static bool Stasis; public static bool IsStasisMode() { return Stasis; } }
public static class FPELog { public static void Warn(string text) {} }
public static class Mathf { public const float Epsilon = float.Epsilon; public static int FloorToInt(float value) { return (int)Math.Floor(value); } }
public static class Safe {
    public static T GetFieldOrDefault<T>(Panel panel, string name, T fallback) { return (T)typeof(Panel).GetField(name).GetValue(panel); }
    public static void SetField(Panel panel, string name, object value) { typeof(Panel).GetField(name).SetValue(panel, value); }
}
public static class ResourceFixture {
    public static void Calculate(Panel panel, Logic logic) {
        $calculation
    }
}
"@
$passed = 0
# Keep the plotted route unchanged while growing and shrinking the party.
$panel = New-Object Panel
$panel.m_routeDistance = 100
foreach ($count in @(1,2,3,4,2,1)) {
    $logic = New-Object Logic
    $logic.SelectedMemberIndices.AddRange([int[]](0..($count - 1)))
    foreach ($mode in @('foot','vehicle','horse','stasis')) {
        $panel.useVehicle = $mode -eq 'vehicle'
        $panel.useHorse = $mode -eq 'horse'
        [FpeRuntimeGuards]::Stasis = $mode -eq 'stasis'
        [ResourceFixture]::Calculate($panel, $logic)
        $expected = switch ($mode) { foot {20 * $count} vehicle {10 * $count} horse {15 * $count} stasis {30 * $count} }
        $fuel = if ($mode -eq 'vehicle') { if ($count -le 2) {5} else {5 * $count} } else {0}
        if ($panel.m_waterRequired -ne $expected -or $panel.m_petrolRequired -ne $fuel) { throw "Incorrect $mode cost for $count members." }
        $passed++
    }
}
$selection = Get-Content -Raw (Join-Path $root 'ExpeditionFour/CreateTeam/PanelPatches.cs')
$dirty = $selection.IndexOf('Safe.SetField(__instance, "m_routeIsDirty", true)')
if ($dirty -lt $selection.IndexOf('if (ValidateSelection') -or $dirty -gt $selection.IndexOf('__instance.ShowMapMenu()')) { throw 'Accepted selections must invalidate costs before opening the map.' }
$passed++
$launch = $source.Substring($source.IndexOf('public static class FinaliseExpedition_Patch'))
$refresh = $launch.IndexOf('Safe.InvokeMethod(panel, "CalculateRouteDistance")')
if ($refresh -lt 0 -or $refresh -gt $launch.IndexOf('!HasSufficientLaunchResources(panel)')) { throw 'Launch must refresh costs before checking resources.' }
$passed++
Write-Output "PASS: $passed checks, 0 failed. Production resource calculation with fixture managers, plus selection and launch wiring contracts."
