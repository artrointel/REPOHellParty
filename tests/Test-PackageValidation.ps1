[CmdletBinding()]
param([string]$PackagePath)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$validator = Join-Path $root 'scripts\Test-Package.ps1'
$manifest = Get-Content -LiteralPath (Join-Path $root 'thunderstore\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$package = if ([string]::IsNullOrWhiteSpace($PackagePath)) { Join-Path $root "artifacts\HellParty-$($manifest.version_number).zip" } else { [IO.Path]::GetFullPath($PackagePath) }
$artifacts = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')).TrimEnd('\') + '\'
$fixtures = Join-Path $artifacts ('package-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$utf8 = New-Object Text.UTF8Encoding($false)
$cases = @(
    @{ Name = 'missing manifest'; Path = 'manifest.json'; Error = 'Missing ZIP entry'; Delete = $true },
    @{ Name = 'invalid version'; Path = 'manifest.json'; Error = 'manifest version'; Text = '{"name":"HellParty","version_number":"9.9.9"}' },
    @{ Name = 'placeholder description'; Path = 'manifest.json'; Error = 'package description'; Text = ('{"name":"HellParty","version_number":"' + $manifest.version_number + '","description":"TODO"}') },
    @{ Name = 'invalid UTF-8'; Path = 'README.md'; Error = 'Unable to translate'; Bytes = [byte[]]@(255, 255) },
    @{ Name = 'non-PNG icon'; Path = 'icon.png'; Error = 'PNG data'; Bytes = [byte[]]@(0, 0, 0, 0, 0, 0, 0, 0) },
    @{ Name = 'tampered DLL'; Path = 'BepInEx/plugins/HellParty/HellParty.dll'; Error = 'differs from release source'; Bytes = [byte[]]@(1, 2, 3, 4) },
    @{ Name = 'extra game assembly'; Path = 'BepInEx/plugins/HellParty/Assembly-CSharp.dll'; Error = 'Unexpected or incorrectly cased'; Bytes = [byte[]]@(1) },
    @{ Name = 'traversal entry'; Path = '../unexpected.dll'; Error = 'Unexpected or incorrectly cased'; Bytes = [byte[]]@(1) },
    @{ Name = 'duplicate case-insensitive entry'; Path = 'readme.md'; Error = 'Duplicate ZIP entry'; Bytes = [byte[]]@(1) }
)
$passed = 0
try {
    & $validator -PackagePath $package
    $passed++
    foreach ($case in $cases) {
        $copy = Join-Path $fixtures ($passed.ToString() + '.zip')
        Copy-Item -LiteralPath $package -Destination $copy
        $zip = [IO.Compression.ZipFile]::Open($copy, [IO.Compression.ZipArchiveMode]::Update)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $case.Path } | Select-Object -First 1
            if ($null -ne $entry) { $entry.Delete() }
            if (-not $case.Delete) {
                $entry = $zip.CreateEntry($case.Path)
                $stream = $entry.Open()
                try {
                    $bytes = if ($case.ContainsKey('Text')) { $utf8.GetBytes($case.Text) } else { $case.Bytes }
                    $stream.Write($bytes, 0, $bytes.Length)
                } finally { $stream.Dispose() }
            }
        } finally { $zip.Dispose() }
        $rejected = $false
        try { & $validator -PackagePath $copy }
        catch {
            # Decoder messages are localized on some machines; keep the type check.
            $encodingError = $case.Name -eq 'invalid UTF-8' -and $_.Exception.ToString() -match 'DecoderFallbackException'
            if ($_.Exception.Message.Contains($case.Error) -or $encodingError) { $rejected = $true }
            else { throw }
        }
        if (-not $rejected) { throw "Validator accepted invalid package: $($case.Name)" }
        $passed++
        Write-Host "PASS: rejected $($case.Name)"
    }
} finally {
    $resolved = [IO.Path]::GetFullPath($fixtures)
    if (-not $resolved.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup outside artifacts.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host "All $passed package validation checks passed."
