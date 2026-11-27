$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw (Join-Path $root 'ExpeditionFour/ExpeditionFourPatches.cs')
$start = $source.IndexOf('            int selectedCount =')
$end = $source.IndexOf('            // Update UI Labels', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Resource calculation not found.' }
$calculation = $source.Substring($start, $end - $start)
$formatStart = $source.IndexOf('        private static string FormatSupplyTotal(')
$layoutStart = $source.IndexOf('        private static int CalculateSupplyLabelWidth(')
$layout = $source.Substring($layoutStart, $formatStart - $layoutStart)
$formatEnd = $source.IndexOf('        internal static bool HasSufficientLaunchResources(', $formatStart)
$formatter = $source.Substring($formatStart, $formatEnd - $formatStart)
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
public static class Mathf {
    public const float Epsilon = float.Epsilon;
    public static float Min(float a, float b) { return Math.Min(a, b); }
    public static int FloorToInt(float value) { return (int)Math.Floor(value); }
    public static float Ceil(float value) { return (float)Math.Ceiling(value); }
}
public static class Safe {
    public static T GetFieldOrDefault<T>(Panel panel, string name, T fallback) { return (T)typeof(Panel).GetField(name).GetValue(panel); }
    public static void SetField(Panel panel, string name, object value) { typeof(Panel).GetField(name).SetValue(panel, value); }
}
public static class ResourceFixture {
    $formatter
    $layout
    public static int SidebarWidth(float left, float mapLeft) {
        return CalculateSupplyLabelWidth(left, mapLeft, 0.5f);
    }
    public static string Format(float amount, float available, float shortage, string supply) {
        return FormatSupplyTotal(amount, available, shortage, supply);
    }
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
foreach ($case in @(
    @{ Amount = 12; Available = 9; Shortage = 3; Supply = 'water'; Expected = '12/9  Need 3 more water' }
    @{ Amount = 5; Available = 3; Shortage = 2; Supply = 'petrol'; Expected = '5/3  Need 2 more petrol' }
    @{ Amount = 0; Available = 8; Shortage = 0.25; Supply = 'battery'; Expected = '0/8  Need 1 more battery' }
    @{ Amount = 9; Available = 9; Shortage = 0; Supply = 'water'; Expected = '9/9' }
    @{ Amount = 4; Available = 8; Shortage = -4; Supply = 'petrol'; Expected = '4/8' }
    @{ Amount = 2; Available = 8; Shortage = -2; Supply = 'battery'; Expected = '2/8' }
)) {
    $actual = [ResourceFixture]::Format($case.Amount, $case.Available, $case.Shortage, $case.Supply)
    if ($actual -cne $case.Expected) { throw "Incorrect supply total: expected '$($case.Expected)', got '$actual'." }
    $passed++
}
# Scale the photographed map geometry with height and center it for both aspect ratios.
foreach ($height in @(720,900,1080,1440)) {
    foreach ($aspectHeight in @(9,10)) {
        $width = [int]($height * 16 / $aspectHeight)
        $scale = $height / 900.0
        $offset = ($width - 1600 * $scale) / 2.0
        $center = $offset + 386 * $scale
        $sidebarLeft = $offset + 322 * $scale
        $mapLeft = $offset + 512 * $scale
        $instructionsLeft = $offset + 520 * $scale
        $labelWidth = [ResourceFixture]::SidebarWidth($sidebarLeft - $center, $mapLeft - $center)
        $label = @{ Left = $center - $labelWidth / 2.0; Right = $center + $labelWidth / 2.0
                    Top = 222 * $scale; Bottom = 254 * $scale }
        $instructions = @{ Left = $instructionsLeft; Right = $offset + 1050 * $scale
                           Top = 198 * $scale; Bottom = 284 * $scale }
        $intersects = $label.Left -lt $instructions.Right -and $label.Right -gt $instructions.Left -and
                      $label.Top -lt $instructions.Bottom -and $label.Bottom -gt $instructions.Top
        if ($labelWidth -le 0 -or $label.Left -le $sidebarLeft -or $label.Right -ge $mapLeft -or $intersects) {
            throw "Supply label exceeds its sidebar at ${width}x${height}."
        }
        $passed++
    }
}
Write-Output "PASS: $passed checks, 0 failed. Production resource calculation and shortage text with fixture managers, plus selection and launch wiring contracts."
