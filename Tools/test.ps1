# 빌드된 게임을 자동 플레이 모드로 실행한다.
#   .\test.ps1 shot [-StartDepth 5]             봇 플레이 스크린샷을 Builds\shot_*.png 로 저장
#   .\test.ps1 sim [-Count 30] [-StartDepth 1]  화면 없이 여러 판을 돌려 밸런스 통계 출력
param(
    [ValidateSet("shot", "sim")] [string]$Mode = "shot",
    [int]$Count = 30,
    [int]$StartDepth = 1
)
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project "Builds\Windows\DungeonCrawler.exe"
$depthArgs = if ($StartDepth -gt 1) { "-startdepth $StartDepth" } else { "" }

if ($Mode -eq "shot") {
    $base = Join-Path $project "Builds\shot"
    Remove-Item "$base*.png" -ErrorAction SilentlyContinue
    $log = Join-Path $project "Builds\shot.log"
    $p = Start-Process $exe -ArgumentList "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile `"$log`" $depthArgs -autoshot `"$base`"" -PassThru
    if (-not $p.WaitForExit(180000)) { $p.Kill(); "timeout" }
    Get-ChildItem "$base*.png" | ForEach-Object { $_.FullName }
} else {
    $log = Join-Path $project "Builds\sim.log"
    $p = Start-Process $exe -ArgumentList "-batchmode -nographics -logFile `"$log`" $depthArgs -simulate $Count" -PassThru
    if (-not $p.WaitForExit(600000)) { $p.Kill(); "timeout" }
}
Select-String -Path $log -Pattern "\[SIM\]|\[AUTOSHOT\]|\[SHOT\]|\[PERF\]|Exception" | ForEach-Object { $_.Line }
