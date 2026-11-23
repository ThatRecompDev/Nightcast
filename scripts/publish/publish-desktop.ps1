# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    The owner's own desktop build: Phantom.Relay.Desktop, Release, self-contained for 64-bit Windows, in one folder.

.DESCRIPTION
    Personal use only: the folder is copied to the owner's own machines (the desktop, the handheld), never published.
    Self-contained, so the machine needs no .NET; ReadyToRun, so it starts fast. Lossless Scaling and NVIDIA's
    Optical Flow SDK are not in it: each loads from its own install at run time where there is one. The fast tests run
    first; a failed build or test stops the run. With -Zip a zip of the folder is written beside it.

.PARAMETER Output
    The folder to publish into; artifacts/publish/desktop in the repository when not given.
#>
[CmdletBinding()]
param(
    [string] $Output,
    [switch] $Zip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not $Output) {
    $Output = Join-Path $root 'artifacts/publish/desktop'
}

& (Join-Path $root 'scripts/test/test-fast.ps1')

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$project = Join-Path $root 'src/Phantom.Relay.Desktop/Phantom.Relay.Desktop.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o $Output

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($Zip) {
    $archive = "$($Output.TrimEnd('\', '/')).zip"
    Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $archive -Force
    Write-Output "Zipped: $archive"
}

Write-Output "Published: $Output"
