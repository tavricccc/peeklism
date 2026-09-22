# Synthetic local fixtures; output is ignored by Git. No user files are read or changed.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/viewer-fixtures'))
$ErrorActionPreference = 'Stop'
$dir = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $dir | Out-Null
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(6000, 4000)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([Drawing.Color]::FromArgb(238, 242, 248))
$font = [Drawing.Font]::new('Segoe UI', 96)
$graphics.DrawString('Peeklism - original pixels 6000 x 4000', $font, [Drawing.Brushes]::Navy, 180, 180)
for ($i = 0; $i -lt 60; $i++) {
    $graphics.DrawLine([Drawing.Pens]::SteelBlue, $i * 100, 500, $i * 100, 3900)
}
$graphics.FillRectangle([Drawing.Brushes]::Coral, 600, 1000, 1200, 1800)
$graphics.FillEllipse([Drawing.Brushes]::SteelBlue, 2600, 1100, 2400, 2400)
$bitmap.Save((Join-Path $dir '原圖 6000x4000.png'), [Drawing.Imaging.ImageFormat]::Png)
$font.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
@'
# 完整 Markdown 測試

這是 **粗體**、*斜體*、~~刪除線~~ 與 `inline code`。

- 第一層
  - 第二層
    - 第三層
- [x] 已完成
- [ ] 尚未完成

| 格式 | 驗證 |
| --- | --- |
| Markdown | 表格與完整解析 |
| PDF | 40 頁、搜尋、文字選取 |

```csharp
Console.WriteLine("<安全顯示>");
```

> 引言必須可閱讀。

[跳到結尾](#結尾) · [外部連結](https://example.com)

![本機圖片](%E5%8E%9F%E5%9C%96%206000x4000.png)

<script>alert('must not execute')</script>

## 結尾

文件尾端驗證標記 END-OF-DOCUMENT。
'@ | Set-Content (Join-Path $dir '完整 文件.md') -Encoding utf8
([string]::new('字', 300000) + "`nEND-AFTER-256KB") | Set-Content (Join-Path $dir 'large.txt') -Encoding utf8
'<svg xmlns="http://www.w3.org/2000/svg" width="800" height="200" viewBox="0 0 800 200"><rect width="800" height="200" fill="#005a9e"/><circle cx="400" cy="100" r="80" fill="#fff"/></svg>' | Set-Content (Join-Path $dir 'wide.svg') -Encoding utf8
# A genuine searchable 40-page PDF, deliberately beyond the preview's 25-page cap.
$objects = [Collections.Generic.List[string]]::new()
$objects.Add('<< /Type /Catalog /Pages 2 0 R >>')
$kids = (0..39 | ForEach-Object { "$(4 + $_ * 2) 0 R" }) -join ' '
$objects.Add("<< /Type /Pages /Kids [$kids] /Count 40 >>")
$objects.Add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
for ($page = 1; $page -le 40; $page++) {
    $contentId = 5 + ($page - 1) * 2
    $objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents $contentId 0 R >>")
    $content = "BT /F1 24 Tf 72 700 Td (Peeklism test - page $page of 40) Tj 0 -48 Td /F1 14 Tf (Searchable selectable full document. END-PAGE-$page) Tj ET`n"
    $objects.Add("<< /Length $([Text.Encoding]::ASCII.GetByteCount($content)) >>`nstream`n${content}endstream")
}
$stream = [IO.MemoryStream]::new()
function Add-Ascii([string]$value) { $bytes = [Text.Encoding]::ASCII.GetBytes($value); $stream.Write($bytes, 0, $bytes.Length) }
Add-Ascii "%PDF-1.4`n"
$offsets = [Collections.Generic.List[long]]::new()
for ($i = 0; $i -lt $objects.Count; $i++) { $offsets.Add($stream.Position); Add-Ascii "$($i + 1) 0 obj`n$($objects[$i])`nendobj`n" }
$xref = $stream.Position
Add-Ascii "xref`n0 $($objects.Count + 1)`n0000000000 65535 f `n"
foreach ($offset in $offsets) { Add-Ascii ($offset.ToString('0000000000') + " 00000 n `n") }
Add-Ascii "trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`nstartxref`n$xref`n%%EOF"
[IO.File]::WriteAllBytes((Join-Path $dir '40 pages.pdf'), $stream.ToArray()); $stream.Dispose()
# Silent PCM audio is safe to autoplay during verification.
$wav = [IO.File]::Create((Join-Path $dir 'silence.wav'))
$writer = [IO.BinaryWriter]::new($wav)
$samples = 44100 * 12
$writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $samples * 2))
$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
$writer.Write([short]1); $writer.Write([short]1); $writer.Write([int]44100); $writer.Write([int]88200)
$writer.Write([short]2); $writer.Write([short]16); $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
$writer.Write([int]($samples * 2)); $writer.Write([byte[]]::new($samples * 2)); $writer.Dispose()
'not a valid PDF' | Set-Content (Join-Path $dir 'corrupt.pdf')
Write-Output $dir
