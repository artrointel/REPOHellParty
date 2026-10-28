[CmdletBinding()]
param(
    [switch]$EnableDebugger,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [ValidateSet("None", "CleanupCrew", "Headgrab")]
    [string]$TestScenario = "None",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
if ($TestScenario -ne "None" -and ($Configuration -ne "Debug" -or $NoBuild)) {
    throw "Use -Configuration Debug with a build to select -TestScenario. -NoBuild runs the already deployed DLL."
}

$gameDirectory = "C:\Program Files (x86)\Steam\steamapps\common\REPO"
$gameExecutable = Join-Path $gameDirectory "REPO.exe"
$profileDirectory = Join-Path $env:APPDATA "Thunderstore Mod Manager\DataFolder\REPO\profiles\REPO"
$preloader = Join-Path $profileDirectory "BepInEx\core\BepInEx.Preloader.dll"
$doorstopProxy = Join-Path $gameDirectory "winhttp.dll"

foreach ($requiredFile in @($gameExecutable, $preloader, $doorstopProxy)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required development file was not found: $requiredFile"
    }
}

if (Get-Process -Name "REPO" -ErrorAction SilentlyContinue) {
    throw "R.E.P.O. is already running. Close it before rebuilding or starting another test."
}

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot "Build-Dev.ps1") -Configuration $Configuration -TestScenario $TestScenario -NoRestore -Deploy
}

$launchArguments = @(
    "--doorstop-enabled", "true",
    "--doorstop-target-assembly", "`"$preloader`""
)

if ($EnableDebugger) {
    $launchArguments += @(
        "--doorstop-mono-debug-enabled", "true",
        "--doorstop-mono-debug-suspend", "false",
        "--doorstop-mono-debug-address", "127.0.0.1:10000"
    )
}

Start-Process `
    -FilePath $gameExecutable `
    -WindowStyle Normal `
    -WorkingDirectory $gameDirectory `
    -ArgumentList $launchArguments

if ($EnableDebugger) {
    Write-Host "R.E.P.O. started in a visible window with the Mono debugger at 127.0.0.1:10000 (no startup suspension)."
}
else {
    Write-Host "R.E.P.O. started with HellParty $Configuration in the Thunderstore REPO profile."
}
if ($TestScenario -eq "CleanupCrew") {
    Write-Host "Cleanup Crew test: difficulty 5 on every stage; one Cleanup Crew near start, shotgun and tranquilizer. Leave the truck if the enemy is waiting for a clear connected floor. Two players are needed to test head transfers."
}
