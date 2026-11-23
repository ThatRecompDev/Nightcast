# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    Builds the FFmpeg libraries (libavcodec, libavutil) of Nightcast's Linux hardware H.264 decoder, through WSL.

.DESCRIPTION
    Runs build-ffmpeg-linux.sh in a WSL distribution (Ubuntu 22.04 by default: its glibc 2.35 is older than
    SteamOS's): the pinned FFmpeg release from ffmpeg.org, SHA-256-checked, configured as a minimal LGPL shared build
    (the h264 decoder and parser, the VA-API hwaccel, nothing else), libva linked dynamically. The libraries, FFmpeg's
    licence and a build note land in artifacts/native/linux-x64/ffmpeg, which is never committed; the linux-x64
    publish picks them up from there (docs/architecture/linux-decode.md). The distribution needs
    build-essential, nasm, pkg-config, curl and libva-dev.

.PARAMETER Distribution
    The WSL distribution to build in.
#>
[CmdletBinding()]
param(
    [string] $Distribution = 'Ubuntu-22.04'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script = Join-Path $PSScriptRoot 'build-ffmpeg-linux.sh'
$output = Join-Path $root 'artifacts/native/linux-x64/ffmpeg'

New-Item -ItemType Directory -Force $output | Out-Null

$linuxScript = (wsl -d $Distribution -- wslpath -a ($script -replace '\\', '/')).Trim()
$linuxOutput = (wsl -d $Distribution -- wslpath -a ($output -replace '\\', '/')).Trim()

wsl -d $Distribution -- bash $linuxScript $linuxOutput

exit $LASTEXITCODE
