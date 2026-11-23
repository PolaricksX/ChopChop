$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'tools\ChopChop.LocalLauncher\ChopChop.LocalLauncher.csproj'
if ($args.Count -eq 0) {
    & dotnet run --project $project
}
else {
    & dotnet run --project $project -- @args
}
exit $LASTEXITCODE
