# Builds\Windows 의 빌드를 Microsoft Store 제출용 MSIX 로 묶는다. (먼저 .\Tools\build.ps1)
#   .\Tools\pack-store.ps1                      store.config.json 값으로 제출용 패키지 생성
#   .\Tools\pack-store.ps1 -AllowPlaceholder    Partner Center 값을 아직 안 넣었어도 묶어 본다(절차 확인용, 제출 불가)
# 결과: Builds\Store\DungeonTamheomga_<버전>_x64.msix
# 서명은 하지 않는다. 스토어가 인증 통과 후 Microsoft 인증서로 다시 서명한다.
# (이 파일의 출력 문자열은 PowerShell 5.1 인코딩 문제를 피하려고 영어로 둔다.)
param([switch]$AllowPlaceholder)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$project = Split-Path $PSScriptRoot -Parent
$build   = Join-Path $project "Builds\Windows"
$out     = Join-Path $project "Builds\Store"
$layout  = Join-Path $out "layout"
$exe     = Join-Path $build "DungeonTamheomga.exe"
$icon    = Join-Path $project "Assets\Icon\AppIcon.png"
if (-not (Test-Path $exe))  { throw "Build not found: $exe  (run .\Tools\build.ps1 first)" }
if (-not (Test-Path $icon)) { throw "Icon not found: $icon  (run .\Tools\build.ps1 first)" }

# ── 설정과 버전 ──
$config = Get-Content (Join-Path $PSScriptRoot "store\store.config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$placeholder = "$($config.IdentityName)$($config.Publisher)$($config.PublisherDisplayName)" -match "REPLACE_ME"
if ($placeholder -and -not $AllowPlaceholder) {
    throw "Tools\store\store.config.json still has REPLACE_ME values. Fill in the Partner Center product identity, or pass -AllowPlaceholder for a dry run."
}
if ($placeholder) {
    # makeappx 는 Publisher 가 올바른 X.500 이름이어야 하고 Name 은 영숫자·점·대시만 허용한다.
    $config.IdentityName = "Placeholder.DungeonTamheomga"
    $config.Publisher = "CN=Placeholder"
    $config.PublisherDisplayName = "Placeholder"
}
$settings = Get-Content (Join-Path $project "ProjectSettings\ProjectSettings.asset") -Raw
if ($settings -notmatch "(?m)^\s*bundleVersion:\s*(\d+)\.(\d+)(?:\.(\d+))?\s*$") { throw "bundleVersion not found in ProjectSettings.asset" }
$patch = if ($Matches[3]) { $Matches[3] } else { "0" }
$version = "$($Matches[1]).$($Matches[2]).$patch.0"   # 넷째 자리는 스토어 몫이라 0 이어야 한다
if ([int]$Matches[1] -eq 0) { throw "Store packages need a major version of 1 or higher (bundleVersion is $version)" }

# ── Windows SDK 도구 ──
$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
if (-not $sdkBin) { throw "makeappx.exe not found. Install the Windows SDK (https://developer.microsoft.com/windows/downloads/windows-sdk/)." }
$makeappx = $sdkBin.FullName
$makepri  = Join-Path $sdkBin.Directory.FullName "makepri.exe"

# ── 패키지 내용 ──
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item $layout -ItemType Directory -Force | Out-Null
# 예전 이름의 빌드 산출물과 디버그 정보는 넣지 않는다.
robocopy $build $layout /E /NFL /NDL /NJH /NJS /NP /XF "DungeonCrawler.exe" "*.pdb" /XD "DungeonCrawler_Data" "*_DoNotShip" "*_BackUpThisFolder_ButDontShipItWithYourGame" | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

# 아이콘 한 장에서 필요한 크기를 만든다.
function Save-Scaled([string]$source, [int]$size, [string]$path) {
    $src = [System.Drawing.Image]::FromFile($source)
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose(); $src.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
$assets = Join-Path $layout "Assets"
New-Item $assets -ItemType Directory -Force | Out-Null
Save-Scaled $icon 50  (Join-Path $assets "StoreLogo.png")
Save-Scaled $icon 150 (Join-Path $assets "Square150x150Logo.png")
Save-Scaled $icon 44  (Join-Path $assets "Square44x44Logo.png")
foreach ($s in 16, 24, 32, 48, 256) {   # 작업 표시줄·시작 메뉴용(바탕판 없는 아이콘)
    Save-Scaled $icon $s (Join-Path $assets "Square44x44Logo.targetsize-$s.png")
    Save-Scaled $icon $s (Join-Path $assets "Square44x44Logo.targetsize-${s}_altform-unplated.png")
}

# 매니페스트
$manifest = Get-Content (Join-Path $PSScriptRoot "store\AppxManifest.template.xml") -Raw -Encoding UTF8
$values = @{
    IdentityName = $config.IdentityName; Publisher = $config.Publisher; PublisherDisplayName = $config.PublisherDisplayName
    DisplayName = $config.DisplayName; Description = $config.Description; Version = $version
}
foreach ($key in $values.Keys) {
    $manifest = $manifest.Replace("{{$key}}", [System.Security.SecurityElement]::Escape([string]$values[$key]))
}
[System.IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding $false))

# 리소스 색인(크기별 아이콘을 찾는 데 필요)
$priConfig = Join-Path $out "priconfig.xml"
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed" }
& $makepri new /pr $layout /cf $priConfig /of (Join-Path $layout "resources.pri") /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri new failed" }

# ── 묶기 ──
$package = Join-Path $out "DungeonTamheomga_${version}_x64.msix"
$log = & $makeappx pack /d $layout /p $package /o 2>&1
if ($LASTEXITCODE -ne 0) { $log | Select-Object -Last 15; throw "makeappx pack failed" }

"Package: $package  ({0:N1} MB, version $version)" -f ((Get-Item $package).Length / 1MB)
if ($placeholder) { "DRY RUN: placeholder identity. Do NOT upload this file. Fill in Tools\store\store.config.json and run again." }
else { "Identity: $($config.IdentityName) / $($config.Publisher) / $($config.PublisherDisplayName)" }
