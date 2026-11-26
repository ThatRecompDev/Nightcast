# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    Nightcast's desktop build: Phantom.Relay.Desktop, Release, self-contained for 64-bit Windows (Nightcast.exe) or
    64-bit Linux (Nightcast-linux-x64, for SteamOS), as one file.

.DESCRIPTION
    Self-contained, so the machine needs no .NET; one compressed file with the native libraries inside (unpacked to
    the user's .NET bundle cache on its first start), without debug information; ReadyToRun, so it starts
    fast. Lossless Scaling and NVIDIA's Optical Flow SDK are not in it: each loads from the user's own install at run
    time. The fast tests run first; a failed build or test stops the run.

    The build is made apart (artifacts/release) with this machine's paths mapped away, then every binary that goes
    into the file is scanned for this machine's user name, profile and repository paths and git e-mail; a match stops
    the run, so nothing of the owner's leaves with a build (docs/how-we-work.md 1a).

    The Linux build carries the engine's linux-x64 native libraries only once the engine's packages hold them (engine
    version 0.83.3-relay.2 and later); before that it builds, but its window, sound and decoder cannot load. It also
    carries FFmpeg's libavcodec and libavutil for the VA-API decoder (docs/architecture/linux-decode.md): when
    artifacts/native/linux-x64/ffmpeg lacks them, scripts/build/build-ffmpeg-linux.ps1 builds them first (in WSL). A
    release also carries FFmpeg's licence (COPYING.LGPLv2.1) and its build note (FFMPEG-BUILD.txt) beside the Linux
    file, for the LGPL (docs/architecture/steamos.md has the notice).

    -Release also stamps the version (year.month.day.hourminute), builds both systems, and publishes them as one
    GitHub release of Nightcast's release repository (each file and its SHA-256) through the GitHub CLI. The
    repository is private since 2026-11-26 and the app no longer updates itself: a build is copied to its device by
    hand. The source is never pushed.

.PARAMETER Output
    The folder to publish into; artifacts/publish/desktop (Windows) or artifacts/publish/desktop-linux-x64 (Linux) in
    the repository when not given. With -Release the Linux build goes into the Windows folder's name plus -linux-x64.

.PARAMETER Runtime
    win-x64 (the default) or linux-x64. -Release builds both whatever is given.

.PARAMETER Release
    Stamp a version and publish both builds as one GitHub release.

.PARAMETER TurnKey
    A private build only: the path of a Cloudflare TURN key file ({"keyId": ..., "apiToken": ...}, the devices
    gateway's turn.json), copied beside the build as turn.json, so the stream can relay when no direct path reaches
    the console. It goes beside the file, never into it, and never with -Release: a public release carries no key.
#>
[CmdletBinding()]
param(
    [string] $Output,
    [ValidateSet('win-x64', 'linux-x64')]
    [string] $Runtime = 'win-x64',
    [switch] $Zip,
    [switch] $Release,
    [string] $TurnKey
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$repository = 'ThatRecompDev/Nightcast'
$names = @{ 'win-x64' = 'Nightcast.exe'; 'linux-x64' = 'Nightcast-linux-x64' }
$builtNames = @{ 'win-x64' = 'Phantom.Relay.Desktop.exe'; 'linux-x64' = 'Phantom.Relay.Desktop' }
$linuxSuffix = '-linux-x64'
$ffmpeg = Join-Path $root 'artifacts/native/linux-x64/ffmpeg'
$ffmpegLibraries = @('libavcodec.so.62', 'libavutil.so.60')
$ffmpegNotices = @('COPYING.LGPLv2.1', 'FFMPEG-BUILD.txt')

$turnKeyName = 'turn.json'

if ($TurnKey -and $Release) {
    throw 'A TURN key goes only into a private build, never into a public release.'
}

if ($TurnKey -and -not (Test-Path $TurnKey -PathType Leaf)) {
    throw "No TURN key file at $TurnKey."
}

if (-not $Output) {
    $Output = Join-Path $root 'artifacts/publish/desktop'

    if ($Runtime -eq 'linux-x64' -and -not $Release) {
        $Output = $Output + $linuxSuffix
    }
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

# Nothing of this machine's may be in a binary: its user name, profile, repository path, git e-mail.
$forbidden = @($env:USERNAME, $env:USERPROFILE, $root, (git -C $root config user.email)) | Where-Object { $_ -and $_.Length -ge 4 }

function Test-Binaries {
    # Every Phantom binary either build put together: the assemblies, and Linux's host without an extension.
    $binaries = Get-ChildItem (Join-Path $artifacts 'bin') -Recurse -File |
        Where-Object { $_.Name -like 'Phantom.*' -and ($_.Extension -in '.dll', '.exe' -or $_.Name -eq 'Phantom.Relay.Desktop') }

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
}

# The Linux decoder's FFmpeg libraries, built once (in WSL) when they are not there yet.
function Initialize-Ffmpeg {
    $missing = $ffmpegLibraries | Where-Object { -not (Test-Path (Join-Path $ffmpeg $_)) }

    if (-not $missing) {
        return
    }

    & (Join-Path $root 'scripts/build/build-ffmpeg-linux.ps1') | Out-Host

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# One system's build into $folder, scanned, under its public name with its SHA-256 as sha256sum writes it.
function Publish-Build([string] $rid, [string] $folder) {
    if ($rid -eq 'linux-x64') {
        Initialize-Ffmpeg
    }

    # The build's own lines go to the console; the function returns only the published file.
    dotnet publish $project -c Release -r $rid --self-contained true @settings -o $folder | Out-Host

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Test-Binaries | Out-Host

    # A private build's TURN key beside it; any other build leaves none behind from an earlier private one.
    $keyFile = Join-Path $folder $turnKeyName

    if ($TurnKey) {
        Copy-Item $TurnKey $keyFile -Force
        Write-Host "TURN key copied beside the build (private: never release this folder)."
    }
    elseif (Test-Path $keyFile) {
        Remove-Item $keyFile -Force
    }

    $name = $names[$rid]
    $built = Join-Path $folder $builtNames[$rid]
    $file = Join-Path $folder $name
    Move-Item $built $file -Force
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$file.sha256" -Value "$hash  $name" -Encoding ascii -NoNewline

    if ($Zip) {
        $archive = "$($folder.TrimEnd('\', '/')).zip"
        Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $archive -Force
        Write-Host "Zipped: $archive"
    }

    Write-Host "Published: $file ($version)"

    return $file
}

if (-not $Release) {
    $null = Publish-Build $Runtime $Output
    exit 0
}

# A release: both systems, one GitHub release, each file with its SHA-256.
$windows = Publish-Build 'win-x64' $Output
$linux = Publish-Build 'linux-x64' ($Output.TrimEnd('\', '/') + $linuxSuffix)
$tag = "v$version"
$notes = "Nightcast $version. Windows: download $($names['win-x64']) and run it. SteamOS and Linux: download $($names['linux-x64']), make it executable and add it to Steam. Copy each new build over the old one. The Linux build uses FFmpeg's libavcodec and libavutil (LGPL v2.1 or later, https://ffmpeg.org): see COPYING.LGPLv2.1 and FFMPEG-BUILD.txt."
$notices = $ffmpegNotices | ForEach-Object { Join-Path $ffmpeg $_ }
gh release create $tag $windows "$windows.sha256" $linux "$linux.sha256" @notices --repo $repository --title "Nightcast $version" --notes $notes

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "Released: https://github.com/$repository/releases/tag/$tag"
