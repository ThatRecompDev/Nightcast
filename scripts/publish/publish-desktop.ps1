# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    Nightcast's desktop build: Phantom.Relay.Desktop, Release, self-contained for 64-bit Windows, as one exe.

.DESCRIPTION
    Self-contained, so the machine needs no .NET; one compressed file with the native libraries inside (unpacked to
    the user's .NET bundle cache on its first start), without debug information; ReadyToRun, so it starts
    fast. Lossless Scaling and NVIDIA's Optical Flow SDK are not in it: each loads from the user's own install at run
    time. The fast tests run first; a failed build or test stops the run.

    The build is made apart (artifacts/release) with this machine's paths mapped away, then every binary that goes
    into the exe is scanned for this machine's user name, profile and repository paths and git e-mail; a match stops
    the run, so nothing of the owner's leaves with a build (docs/how-we-work.md 1a).

    -Release also stamps the version (year.month.day.hourminute), and publishes it as a public GitHub release of
    Nightcast's release repository (the exe and its SHA-256) through the GitHub CLI, which the app updates from. The
    source is never pushed.

.PARAMETER Output
    The folder to publish into; artifacts/publish/desktop in the repository when not given.

.PARAMETER Release
    Stamp a version and publish the build as a GitHub release.
#>
[CmdletBinding()]
param(
    [string] $Output,
    [switch] $Zip,
    [switch] $Release
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$repository = 'ThatRecompDev/Nightcast'
$exeName = 'Nightcast.exe'

if (-not $Output) {
    $Output = Join-Path $root 'artifacts/publish/desktop'
}

& (Join-Path $root 'scripts/test/test-fast.ps1')

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# The version: a release's from the clock, a local build's stays 1.0.0.0 and never updates itself.
$now = Get-Date
$version = '1.0.0.0'

if ($Release) {
    $version = '{0}.{1}.{2}.{3}' -f $now.Year, $now.Month, $now.Day, ($now.Hour * 100 + $now.Minute)
}

$artifacts = Join-Path $root 'artifacts/release'
$project = Join-Path $root 'src/Phantom.Relay.Desktop/Phantom.Relay.Desktop.csproj'
$settings = @(
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    # No debug information: embedded, it is compressed, and the scan below could not see a path inside it.
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-p:PublishDocumentationFiles=false',
    '-p:PublishReferencesDocumentationFiles=false',
    '-p:PublishReadyToRun=true',
    "-p:Version=$version",
    "-p:ArtifactsPath=$artifacts",
    "-p:PathMap=$root=/nightcast"
)
dotnet publish $project -c Release -r win-x64 --self-contained true @settings -o $Output

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Nothing of this machine's may be in a binary: its user name, profile, repository path, git e-mail.
$forbidden = @($env:USERNAME, $env:USERPROFILE, $root, (git -C $root config user.email)) | Where-Object { $_ -and $_.Length -ge 4 }
$binaries = Get-ChildItem (Join-Path $artifacts 'bin') -Recurse -Include *.dll, *.exe | Where-Object { $_.Name -like 'Phantom.*' }

foreach ($binary in $binaries) {
    $bytes = [IO.File]::ReadAllBytes($binary.FullName)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $wide = [Text.Encoding]::Unicode.GetString($bytes)

    foreach ($text in $forbidden) {
        if ($ascii.IndexOf($text, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $wide.IndexOf($text, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$($binary.Name) holds something of this machine's (one of: user name, profile, repository path, e-mail); not published."
        }
    }
}

Write-Output "Scanned $($binaries.Count) binaries: nothing of this machine's."

# The exe under its public name, and its SHA-256 as sha256sum writes it.
$built = Join-Path $Output 'Phantom.Relay.Desktop.exe'
$exe = Join-Path $Output $exeName
Move-Item $built $exe -Force
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$exe.sha256" -Value "$hash  $exeName" -Encoding ascii -NoNewline

if ($Zip) {
    $archive = "$($Output.TrimEnd('\', '/')).zip"
    Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $archive -Force
    Write-Output "Zipped: $archive"
}

Write-Output "Published: $exe ($version)"

if ($Release) {
    $tag = "v$version"
    $notes = "Nightcast $version. Download $exeName and run it; it updates itself from here."
    gh release create $tag $exe "$exe.sha256" --repo $repository --title "Nightcast $version" --notes $notes

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Output "Released: https://github.com/$repository/releases/tag/$tag"
}
