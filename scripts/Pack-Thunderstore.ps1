[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\HellParty\HellParty.csproj"
$metadataDirectory = Join-Path $projectRoot "thunderstore"
$iconPath = Join-Path $metadataDirectory "icon.png"
$localDotnet = Join-Path $projectRoot ".dotnet\dotnet.exe"
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $localDotnet
}
else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}

if ([string]::IsNullOrWhiteSpace($dotnetCommand)) {
    throw ".NET 9 SDK가 필요합니다: https://dotnet.microsoft.com/download/dotnet/9.0"
}

if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "Thunderstore용 256x256 PNG 아이콘을 thunderstore\icon.png에 추가하세요."
}

Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile($iconPath)
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256) {
        throw "Thunderstore 아이콘은 정확히 256x256 픽셀이어야 합니다. 현재: $($icon.Width)x$($icon.Height)"
    }
}
finally {
    $icon.Dispose()
}

[xml]$project = Get-Content -LiteralPath $projectFile -Encoding UTF8
$versionNode = $project.SelectSingleNode("/Project/PropertyGroup/Version")
$version = if ($null -eq $versionNode) { "" } else { $versionNode.InnerText.Trim() }
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "HellParty.csproj에서 Version을 찾을 수 없습니다."
}

$authorNode = $project.SelectSingleNode("/Project/PropertyGroup/Authors")
$author = if ($null -eq $authorNode) { "" } else { $authorNode.InnerText.Trim() }
if ([string]::IsNullOrWhiteSpace($author) -or $author -like "TODO*") {
    throw "HellParty.csproj의 Authors를 실제 Thunderstore 팀/작성자 이름으로 바꾸세요."
}

$manifestPath = Join-Path $metadataDirectory "manifest.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.version_number -ne $version) {
    throw "버전 불일치: csproj=$version, manifest=$($manifest.version_number)"
}
if ($manifest.website_url -like "*TODO*") {
    throw "manifest.json의 website_url을 실제 주소로 바꾸세요."
}

$pluginSource = Get-Content -LiteralPath (Join-Path $projectRoot "src\HellParty\Plugin.cs") -Raw -Encoding UTF8
$pluginVersionMatch = [regex]::Match($pluginSource, 'PluginVersion\s*=\s*"(?<version>[^"]+)"')
if (-not $pluginVersionMatch.Success -or $pluginVersionMatch.Groups["version"].Value -ne $version) {
    throw "버전 불일치: csproj=$version, Plugin.cs=$($pluginVersionMatch.Groups['version'].Value)"
}
if ($pluginSource -like "*TODO_AUTHOR*") {
    throw "Plugin.cs의 PluginGuid를 실제 작성자 식별자로 바꾸세요."
}

$env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $projectRoot ".nuget\packages"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$buildArguments = @("build", $projectFile, "--configuration", $Configuration, "-p:CopyFilesToPluginOutputDirectoryOnBuild=false")
if ($NoRestore) {
    $buildArguments += "--no-restore"
}
& $dotnetCommand @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "빌드에 실패했습니다."
}

$dllPath = Join-Path $projectRoot "src\HellParty\bin\$Configuration\netstandard2.1\HellParty.dll"
if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "빌드 결과 DLL을 찾을 수 없습니다: $dllPath"
}

$artifactsDirectory = Join-Path $projectRoot "artifacts"
$stagingDirectory = Join-Path $artifactsDirectory "thunderstore-staging"
$packagePath = Join-Path $artifactsDirectory "HellParty-$version.zip"

# Check absolute targets before removing a previous staging directory or package.
$artifactRoot = [IO.Path]::GetFullPath($artifactsDirectory).TrimEnd('\') + '\'
foreach ($cleanupPath in @($stagingDirectory, $packagePath)) {
    $resolvedPath = [IO.Path]::GetFullPath($cleanupPath)
    if (-not $resolvedPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the artifacts directory: $resolvedPath"
    }
}

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

$pluginDirectory = Join-Path $stagingDirectory "BepInEx\plugins\HellParty"
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination $pluginDirectory
Copy-Item -LiteralPath $manifestPath -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $metadataDirectory "README.md") -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $metadataDirectory "CHANGELOG.md") -Destination $stagingDirectory
Copy-Item -LiteralPath $iconPath -Destination $stagingDirectory

Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $packagePath
Write-Host "Thunderstore package created: $packagePath"
