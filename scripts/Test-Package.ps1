[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ExpectedDllPath
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root "src\HellParty\HellParty.csproj") -Encoding UTF8
$version = $project.SelectSingleNode("/Project/PropertyGroup/Version").InnerText.Trim()
if ([string]::IsNullOrWhiteSpace($PackagePath)) { $PackagePath = Join-Path $root "artifacts\HellParty-$version.zip" }
if ([string]::IsNullOrWhiteSpace($ExpectedDllPath)) { $ExpectedDllPath = Join-Path $root "src\HellParty\bin\Release\netstandard2.1\HellParty.dll" }
if (-not (Test-Path -LiteralPath $ExpectedDllPath -PathType Leaf)) { throw "Expected Release DLL not found: $ExpectedDllPath" }
$assembly = [Reflection.AssemblyName]::GetAssemblyName($ExpectedDllPath)
if ($assembly.Name -cne "HellParty" -or $assembly.Version.ToString(3) -cne $version) { throw "Release DLL identity/version does not match project metadata." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Drawing
$utf8 = New-Object Text.UTF8Encoding($false, $true)
$expected = @{
    'manifest.json' = Join-Path $root 'thunderstore\manifest.json'
    'README.md' = Join-Path $root 'thunderstore\README.md'
    'CHANGELOG.md' = Join-Path $root 'thunderstore\CHANGELOG.md'
    'icon.png' = Join-Path $root 'thunderstore\icon.png'
    'BepInEx/plugins/HellParty/HellParty.dll' = $ExpectedDllPath
}
$seen = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
try {
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if (-not $seen.Add($name)) { throw "Duplicate ZIP entry: $name" }
        if ($name.EndsWith('/')) {
            if ($name -cnotin @('BepInEx/', 'BepInEx/plugins/', 'BepInEx/plugins/HellParty/') -or $entry.Length -ne 0) {
                throw "Unexpected ZIP directory: $name"
            }
            continue
        }
        if ($name -cnotin @($expected.Keys)) { throw "Unexpected or incorrectly cased ZIP entry: $name" }
        # Bound reads before expanding data, and never extract ZIP paths to disk.
        if ($entry.Length -le 0 -or $entry.Length -gt 16MB) { throw "Empty or oversized package entry: $name" }
        $stream = $entry.Open()
        $buffer = New-Object IO.MemoryStream
        try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() }
        finally { $stream.Dispose(); $buffer.Dispose() }
        if ($name.EndsWith('.md') -or $name.EndsWith('.json')) {
            $content = $utf8.GetString($bytes).TrimStart([char]0xFEFF)
            if ([string]::IsNullOrWhiteSpace($content)) { throw "Empty text: $name" }
        }
        if ($name -ceq 'manifest.json') {
            $manifest = $content | ConvertFrom-Json
            if ($manifest.name -cne 'HellParty' -or $manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$') { throw 'Invalid manifest name.' }
            if ($manifest.version_number -cne $version -or $manifest.version_number -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Invalid or mismatched manifest version.' }
            if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250 -or $manifest.description -match '(?i)scaffold|TODO') { throw 'Invalid or placeholder package description.' }
            if ($manifest.website_url) {
                $uri = $null
                if (-not [Uri]::TryCreate($manifest.website_url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('https', 'http')) { throw 'Invalid website URL.' }
            }
            if ($null -eq $manifest.dependencies -or $manifest.dependencies -is [string] -or @($manifest.dependencies).Count -eq 0) { throw 'Dependencies must be a nonempty array.' }
            foreach ($dependency in $manifest.dependencies) {
                if ($dependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-[0-9]+\.[0-9]+\.[0-9]+$') { throw "Invalid dependency: $dependency" }
            }
            if (@($manifest.dependencies | Where-Object { $_ -match '^BepInEx-BepInExPack-' }).Count -ne 1) { throw 'Exactly one BepInExPack dependency is required.' }
        }
        if ($name -ceq 'icon.png') {
            $pngHeader = [BitConverter]::ToString($bytes, 0, [Math]::Min(8, $bytes.Length))
            if ($pngHeader -cne '89-50-4E-47-0D-0A-1A-0A') { throw 'Icon must be PNG data.' }
            $imageStream = New-Object IO.MemoryStream(,$bytes)
            $image = $null
            try {
                $image = [Drawing.Image]::FromStream($imageStream)
                if ($image.Width -ne 256 -or $image.Height -ne 256) { throw 'Icon must be 256x256.' }
            } finally {
                if ($null -ne $image) { $image.Dispose() }
                $imageStream.Dispose()
            }
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') }
        finally { $sha.Dispose() }
        if ($hash -cne (Get-FileHash -LiteralPath $expected[$name] -Algorithm SHA256).Hash) { throw "Packaged file differs from release source: $name" }
    }
    foreach ($name in $expected.Keys) {
        if (-not $seen.Contains($name)) { throw "Missing ZIP entry: $name" }
    }
} finally { $archive.Dispose() }
Write-Host "Validated HellParty $version package: $($expected.Count) files; metadata, UTF-8, PNG and Release DLL hashes match."
