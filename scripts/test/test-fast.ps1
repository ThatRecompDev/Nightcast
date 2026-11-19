# Copyright (c) 2026 Phantom.Relay. All rights reserved.
<#
.SYNOPSIS
    The per-commit check: builds the solution and runs the fast tests, the architecture rules included.

.DESCRIPTION
    A failed build stops the run; tests never run on the binaries of an earlier build. Tests tagged Category=Live
    (a console, an account or the network) do not run here.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solution = Join-Path $root 'Phantom.Relay.slnx'

dotnet build $solution

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# A test that hangs fails the run after two minutes, with a dump of where it hung, instead of stalling it.
dotnet test $solution --no-build --filter 'Category!=Live' --blame-hang-timeout 2m

exit $LASTEXITCODE
