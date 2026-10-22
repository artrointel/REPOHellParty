[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Offline,
    [switch]$NoRestore,
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\HellParty\HellParty.csproj"
$localDotnet = Join-Path $projectRoot ".dotnet\dotnet.exe"
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}
if ([string]::IsNullOrWhiteSpace($dotnetCommand)) {
    throw ".NET 9 SDK is required. Install it on PATH or in .dotnet."
}
if ($Deploy -and (Get-Process -Name "REPO" -ErrorAction SilentlyContinue)) {
    throw "Close R.E.P.O. before deploying the plugin."
}
if ($Deploy -and -not (Test-Path -LiteralPath (Join-Path $projectRoot "Directory.Repo.props"))) {
    throw "Create Directory.Repo.props from an example before deploying."
}

$env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $projectRoot ".nuget\packages"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Push-Location $projectRoot
try {
    if (-not $NoRestore) {
        $restoreArguments = @("restore", $projectFile)
        if ($Offline) {
            if (-not (Test-Path -LiteralPath $env:NUGET_PACKAGES -PathType Container)) {
                throw "Offline restore requires a populated .nuget/packages cache."
            }
            $restoreArguments += @("--source", $env:NUGET_PACKAGES, "-p:RestoreAdditionalProjectSources=", "-p:NuGetAudit=false")
        }
        & $dotnetCommand @restoreArguments
        if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
    }
    $deployValue = if ($Deploy) { "true" } else { "false" }
    & $dotnetCommand build $projectFile --configuration $Configuration --no-restore "-p:CopyFilesToPluginOutputDirectoryOnBuild=$deployValue"
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
} finally {
    Pop-Location
}
