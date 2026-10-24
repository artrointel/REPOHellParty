[CmdletBinding()]
param()

# Resize the reviewed cartoon master for the package; do not regenerate artwork.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root "assets\branding\hellparty-icon-source.png"
$outputPath = Join-Path $root "thunderstore\icon.png"
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile($sourcePath)
$bitmap = $null
$graphics = $null
try {
    if ($source.Width -ne $source.Height) { throw "The icon master must be square; automatic cropping is not allowed." }
    $bitmap = New-Object Drawing.Bitmap(256, 256, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($source, (New-Object Drawing.Rectangle(0, 0, 256, 256)))
    $bitmap.Save($outputPath, [Drawing.Imaging.ImageFormat]::Png)
} finally {
    if ($null -ne $graphics) { $graphics.Dispose() }
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    $source.Dispose()
}
Write-Host "Exported cartoon icon at 256x256: $outputPath"
