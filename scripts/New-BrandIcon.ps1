# Converts the master brand PNG into the multi-size .ico that the app, the tray and the
# installer embed. The artwork itself is authored by hand and lives at the source path
# below; this script only derives the icon from it.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../docs/assets'))
$sourcePath = Join-Path $assets 'peeklism-icon-fluent.png'
if (!(Test-Path -LiteralPath $sourcePath)) {
    throw "找不到品牌圖：$sourcePath"
}

# Every frame is stored as PNG, which Windows has accepted inside .ico since Vista.
$frames = @()
$source = [Drawing.Bitmap]::new($sourcePath)
foreach ($dimension in @(16, 24, 32, 48, 64, 256)) {
    $frame = [Drawing.Bitmap]::new($dimension, $dimension)
    $graphics = [Drawing.Graphics]::FromImage($frame)
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($source, [Drawing.Rectangle]::new(0, 0, $dimension, $dimension))
    $stream = [IO.MemoryStream]::new()
    $frame.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += @{ Size = $dimension; Bytes = $stream.ToArray() }
    $stream.Dispose(); $graphics.Dispose(); $frame.Dispose()
}
$source.Dispose()

$output = Join-Path $assets 'peeklism.ico'
$fileStream = [IO.File]::Create($output)
$writer = [IO.BinaryWriter]::new($fileStream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        # 256 is stored as 0: a directory entry has only one byte per dimension.
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $fileStream.Dispose() }
Write-Output $output
