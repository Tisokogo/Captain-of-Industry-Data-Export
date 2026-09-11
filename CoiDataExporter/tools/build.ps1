[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $env:COI_ROOT) {
    throw "COI_ROOT is not set. Set it to the Captain of Industry installation directory."
}

$managed = Join-Path $env:COI_ROOT 'Captain of Industry_Data\Managed'
if (-not (Test-Path (Join-Path $managed 'Mafi.Core.dll'))) {
    throw "Mafi.Core.dll was not found below COI_ROOT: $env:COI_ROOT"
}

$deploy = if ($SkipDeploy) { 'false' } else { 'true' }

Push-Location $root
try {
    dotnet build '.\CoiDataExporter.sln' `
        --configuration $Configuration `
        -p:DeployToModsFolder=$deploy
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
