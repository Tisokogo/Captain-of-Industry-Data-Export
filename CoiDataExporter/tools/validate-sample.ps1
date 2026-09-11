[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    dotnet run --project '.\src\CoiDataExporter.Cli\CoiDataExporter.Cli.csproj' -- validate '.\samples\coi-data.sample.json'
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
