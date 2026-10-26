[CmdletBinding()]
param(
    [switch]$Offline,
    [switch]$RequireGameAssembly,
    [string]$GameAssembly = "C:\Program Files (x86)\Steam\steamapps\common\REPO\REPO_Data\Managed\Assembly-CSharp.dll"
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($RequireGameAssembly -and -not (Test-Path -LiteralPath $GameAssembly -PathType Leaf)) {
    throw "Release verification requires the installed game's Assembly-CSharp.dll: $GameAssembly"
}
$buildArgs = @{}
if ($Offline) { $buildArgs.Offline = $true }
& (Join-Path $PSScriptRoot "Build-Dev.ps1") @buildArgs -Configuration Debug
& (Join-Path $PSScriptRoot "Build-Dev.ps1") -NoRestore -Configuration Release
& (Join-Path $PSScriptRoot "Build-Dev.ps1") -NoRestore -Configuration Debug -TestScenario CleanupCrew
$localDotnet = Join-Path $projectRoot ".dotnet\dotnet.exe"
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet).Source }
$testProject = Join-Path $projectRoot "tests\HellParty.Tests\HellParty.Tests.csproj"
$restoreArgs = @("restore", $testProject)
if ($Offline) { $restoreArgs += @("--source", (Join-Path $projectRoot ".nuget\packages"), "-p:NuGetAudit=false") }
& $dotnetCommand @restoreArgs
if ($LASTEXITCODE -ne 0) { throw "Test restore failed." }
$runArgs = @("run", "--project", $testProject, "--no-restore", "--")
if (Test-Path -LiteralPath $GameAssembly -PathType Leaf) { $runArgs += $GameAssembly }
else { Write-Warning "Game assembly not found; installed-game contract checks are skipped. Use -RequireGameAssembly for release verification." }
& $dotnetCommand @runArgs
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
