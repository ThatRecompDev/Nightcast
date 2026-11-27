# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    Copies the Phantom.Engine packages Relay builds on into vendor/nuget, so a clone restores without the engine's
    checkout.

.DESCRIPTION
    Reads the engine version Directory.Packages.props pins, follows each pinned Phantom.Engine package's own
    dependencies through its nuspec, and copies exactly that set from the engine's local feed into vendor/nuget,
    removing any other engine package there. nuget.config restores Phantom.Engine.* from vendor/nuget only. Run it in
    the commit that moves the pin (docs/how-we-work.md, 2), and commit vendor/nuget with it.

.PARAMETER Feed
    The engine's package feed; the engine checkout's artifacts/packages beside the user's repositories by default.
#>
[CmdletBinding()]
param(
    [string] $Feed = (Join-Path $env:USERPROFILE 'Documents/Repositories/Phantom.Engine/artifacts/packages')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$props = Join-Path $root 'Directory.Packages.props'
$vendor = Join-Path $root 'vendor/nuget'
$prefix = 'Phantom.Engine.'

# The pinned packages and their one version.
$pins = (Select-String -Path $props -Pattern 'Include="(Phantom\.Engine[^"]+)" Version="([^"]+)"' -AllMatches).Matches
$versions = @($pins | ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique)

if ($versions.Count -ne 1) {
    throw "Directory.Packages.props pins Phantom.Engine at more than one version: $($versions -join ', ')."
}

$version = $versions[0]

# Every package the pinned ones need, through their nuspecs.
$queue = [System.Collections.Generic.Queue[string]]::new()
$needed = [System.Collections.Generic.HashSet[string]]::new()
$pins | ForEach-Object { $queue.Enqueue($_.Groups[1].Value) }

while ($queue.Count -gt 0) {
    $id = $queue.Dequeue()

    if (-not $needed.Add($id)) {
        continue
    }

    $package = Join-Path $Feed "$id.$version.nupkg"

    if (-not (Test-Path $package)) {
        throw "The engine feed has no $id $version (looked in $Feed)."
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($package)

    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        $reader = New-Object IO.StreamReader($entry.Open())
        $nuspec = [xml]$reader.ReadToEnd()
        $reader.Dispose()
    }
    finally {
        $zip.Dispose()
    }

    $nuspec.SelectNodes("//*[local-name()='dependency']") |
        ForEach-Object { $_.id } |
        Where-Object { $_ -like "$prefix*" } |
        ForEach-Object { $queue.Enqueue($_) }
}

# Exactly that set in vendor/nuget: other versions and packages Relay no longer needs are removed.
New-Item -ItemType Directory -Force $vendor | Out-Null
$keep = $needed | ForEach-Object { "$_.$version.nupkg" }

Get-ChildItem $vendor -Filter "$prefix*.nupkg" | Where-Object { $_.Name -notin $keep } | Remove-Item -Force

foreach ($name in $keep) {
    Copy-Item (Join-Path $Feed $name) (Join-Path $vendor $name) -Force
}

$megabytes = (Get-ChildItem $vendor -Filter "$prefix*.nupkg" | Measure-Object Length -Sum).Sum / 1MB
Write-Output ("Vendored {0} engine packages at {1} ({2:N1} MB) into vendor/nuget." -f $keep.Count, $version, $megabytes)
