[CmdletBinding()]
param(
    [switch]$Offline,
    [string]$GameAssembly = 'C:\Program Files (x86)\Steam\steamapps\common\REPO\REPO_Data\Managed\Assembly-CSharp.dll'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $root 'thunderstore\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$notesSource = Join-Path $root "docs\releases\$($manifest.version_number).md"
if (-not (Test-Path -LiteralPath $notesSource -PathType Leaf)) { throw "Release notes are required: $notesSource" }
$receipt = Join-Path $root "artifacts\HellParty-$($manifest.version_number).release.json"
if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt }
$testArguments = @{ GameAssembly = $GameAssembly; RequireGameAssembly = $true }
if ($Offline) { $testArguments.Offline = $true }
& (Join-Path $PSScriptRoot 'Test-Dev.ps1') @testArguments
& (Join-Path $PSScriptRoot 'Pack-Thunderstore.ps1') -NoRestore
& (Join-Path $root 'tests\Test-PackageValidation.ps1')
# Packaging also calls Test-Package. This receipt is written only after every check passes.
$zip = Join-Path $root "artifacts\HellParty-$($manifest.version_number).zip"
$dll = Join-Path $root 'src\HellParty\bin\Release\netstandard2.1\HellParty.dll'
$notes = Join-Path $root "artifacts\HellParty-$($manifest.version_number).release-notes.md"
$checksum = $zip + '.sha256'
$packageHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Copy-Item -LiteralPath $notesSource -Destination $notes -Force
[IO.File]::WriteAllText($checksum, "$packageHash  $([IO.Path]::GetFileName($zip))`n", (New-Object Text.UTF8Encoding($false)))
$metadata = [ordered]@{
    version = $manifest.version_number
    configuration = 'Release'
    verified_utc = [DateTime]::UtcNow.ToString('o')
    package_sha256 = $packageHash
    release_notes_sha256 = (Get-FileHash -LiteralPath $notes -Algorithm SHA256).Hash
    plugin_sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    game_assembly_sha256 = (Get-FileHash -LiteralPath $GameAssembly -Algorithm SHA256).Hash
    checks = @('Debug/Release build', 'isolated opt-in Debug scenario', 'Release debug-code exclusion and optimization', 'deterministic rules', 'installed-game contracts', 'package contents and hashes', 'invalid package rejection')
    in_game_multiplayer_verified = $false
}
[IO.File]::WriteAllText($receipt, ($metadata | ConvertTo-Json -Depth 4) + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Release candidate: $zip"
Write-Host "Verification receipt: $receipt"
Write-Host "Release notes: $notes"
Write-Host "Package checksum: $checksum"
