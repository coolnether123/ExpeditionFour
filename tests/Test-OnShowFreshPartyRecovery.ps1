$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot 'ExpeditionFour\PartySetupFour_InitPatch.cs'
$source = Get-Content -Raw $sourcePath

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch $Pattern) {
        throw "FAIL: $Message"
    }
}

Assert-Contains $source 'party == null && partyId == -1' 'The recovery path must be restricted to the no-active-party sentinel.'
Assert-Contains $source 'Safe\.InvokeMethod\(__instance, "CreateNewParty"\)' 'Recovery must use the vanilla CreateNewParty seam.'
Assert-Contains $source 'partyId >= 0' 'Recovery must verify that vanilla created a real party before touching the UI.'
Assert-Contains $source 'AssignStasisPartyMapNumber\(partyId\)' 'Recovery must preserve the vanilla party-map initialization step.'
Assert-Contains $source 'Safe\.InvokeMethod\(__instance, "ShowPartySetup"\)' 'Recovery must reopen setup through the vanilla ShowPartySetup seam.'
Assert-Contains $source 'Safe\.InvokeMethod\(__instance, "ClearAllWaypoints"\)' 'Recovery must preserve the vanilla fresh-party waypoint reset.'

$recoveryStart = $source.IndexOf('if (party == null && partyId == -1)', [StringComparison]::Ordinal)
$existingPartyStart = $source.IndexOf('if (party != null)', $recoveryStart, [StringComparison]::Ordinal)
if ($recoveryStart -lt 0 -or $existingPartyStart -lt 0 -or $existingPartyStart -le $recoveryStart) {
    throw 'FAIL: Could not locate the recovery/valid-party control-flow boundary.'
}

$recoveryBlock = $source.Substring($recoveryStart, $existingPartyStart - $recoveryStart)
if ($recoveryBlock -notmatch 'CreateNewParty') {
    throw 'FAIL: The no-party branch does not retry party creation.'
}

$validPartyBlock = $source.Substring($existingPartyStart)
if ($validPartyBlock -notmatch 'Existing components on party') {
    throw 'FAIL: Existing valid-party synchronization path is missing.'
}

Write-Output 'PASS: no-party radio reopen recovery contracts'
