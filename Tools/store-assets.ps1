# Microsoft Store 등록 정보(Store listing)에 올릴 그림을 Builds\Store\listing 에 만든다. (먼저 .\Tools\build.ps1)
#   .\Tools\store-assets.ps1              로고·포스터 + 한국어/영어 스크린샷(1920x1080)
#   .\Tools\store-assets.ps1 -NoShots     로고·포스터만 (게임을 실행하지 않음)
# 스크린샷은 봇이 창 모드로 플레이하며 찍으므로 몇 분 걸리고, 그동안 게임 창이 뜬다.
# 규격(Microsoft Learn "App screenshots, images, and trailers"): 스크린샷 PNG 1366x768 이상,
#   1:1 앱 타일 300x300, 1:1 박스 아트 1080x1080, 2:3 포스터 720x1080 / 1440x2160 (글자는 위쪽 2/3 안에).
# (이 파일의 출력 문자열은 PowerShell 5.1 인코딩 문제를 피하려고 영어로 둔다.)
param([switch]$NoShots)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$project = Split-Path $PSScriptRoot -Parent
$icon    = Join-Path $project "Assets\Icon\AppIcon.png"
$listing = Join-Path $project "Builds\Store\listing"
if (-not (Test-Path $icon)) { throw "Icon not found: $icon  (run .\Tools\build.ps1 first)" }
New-Item $listing -ItemType Directory -Force | Out-Null

$background = [System.Drawing.Color]::FromArgb(0x1b, 0x18, 0x2c)
$gold       = [System.Drawing.Color]::FromArgb(0xff, 0xd9, 0x66)
$grey       = [System.Drawing.Color]::FromArgb(0xb0, 0xb0, 0xb8)

# 어두운 바탕 위에 아이콘을 그린다. $title 을 주면 위쪽에 게임 이름과 부제를 쓴다.
function New-Art([int]$width, [int]$height, [string]$path, [switch]$Title) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($background)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $src = [System.Drawing.Image]::FromFile($icon)
    if ($Title) {
        $unit = $width / 720.0
        $center = New-Object System.Drawing.StringFormat
        $center.Alignment = [System.Drawing.StringAlignment]::Center
        $titleFont = New-Object System.Drawing.Font "Segoe UI", (62 * $unit), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $subFont   = New-Object System.Drawing.Font "Segoe UI", (26 * $unit), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
        $goldBrush = New-Object System.Drawing.SolidBrush $gold
        $greyBrush = New-Object System.Drawing.SolidBrush $grey
        $g.FillRectangle($goldBrush, 0, 0, $width, [int](10 * $unit))
        $g.DrawString("DungeonTamheomga", $titleFont, $goldBrush, (New-Object System.Drawing.RectangleF 0, (90 * $unit), $width, (90 * $unit)), $center)
        $g.DrawString("A turn-based dungeon roguelike", $subFont, $greyBrush, (New-Object System.Drawing.RectangleF 0, (182 * $unit), $width, (44 * $unit)), $center)
        $size = [int](440 * $unit)
        $g.DrawImage($src, [int](($width - $size) / 2), [int](250 * $unit), $size, $size)   # 아래쪽 1/3 은 비워 둔다
    } else {
        $g.DrawImage($src, 0, 0, $width, $height)
    }
    $src.Dispose(); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    "  $path"
}

"Logos:"
New-Art 300 300   (Join-Path $listing "logo_1x1_300x300.png")
New-Art 1080 1080 (Join-Path $listing "boxart_1x1_1080x1080.png")
New-Art 720 1080  (Join-Path $listing "poster_2x3_720x1080.png") -Title
New-Art 1440 2160 (Join-Path $listing "poster_2x3_1440x2160.png") -Title
if ($NoShots) { return }

# 스크린샷: 언어마다 1층부터 한 판(타이틀·직업 선택·전투·방·상점), 5층 보스전 한 판.
$builds = Join-Path $project "Builds"
foreach ($lang in "ko", "en") {
    $dest = Join-Path $listing "screenshots\$lang"
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item $dest -ItemType Directory -Force | Out-Null

    & (Join-Path $PSScriptRoot "test.ps1") shot -Lang $lang -Width 1920 -Height 1080 | Out-Null
    $n = 1
    foreach ($name in "title", "select", "combat", "room", "shop", "meta") {
        $file = Join-Path $builds "shot_${lang}_$name.png"
        if (Test-Path $file) { Copy-Item $file (Join-Path $dest ("{0:00}_$name.png" -f $n)); $n++ }
    }
    & (Join-Path $PSScriptRoot "test.ps1") shot -Lang $lang -Width 1920 -Height 1080 -StartDepth 5 -BossOnly | Out-Null
    foreach ($name in "boss", "slam") {
        $file = Join-Path $builds "shot_${lang}_$name.png"
        if (Test-Path $file) { Copy-Item $file (Join-Path $dest ("{0:00}_$name.png" -f $n)); $n++ }
    }
    "Screenshots ($lang):"
    Get-ChildItem $dest -Filter *.png | ForEach-Object {
        $img = [System.Drawing.Image]::FromFile($_.FullName)
        "  {0}  {1}x{2}" -f $_.FullName, $img.Width, $img.Height
        $img.Dispose()
    }
}
"Pick the best 4-8 per language. Check each image before uploading."
