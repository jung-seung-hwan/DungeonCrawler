# 빌드된 게임을 자동 플레이 모드로 실행한다.
#   .\test.ps1 shot [-StartDepth 5]             봇 플레이 스크린샷을 Builds\shot_*.png 로 저장
#   .\test.ps1 sim [-Count 30] [-StartDepth 1]  화면 없이 여러 판을 돌려 밸런스 통계 출력
#   .\test.ps1 sim -StartDepth 50 -BossOnly     보스 층의 다른 몬스터를 치우고 보스전 승률만 확인 (shot에도 사용 가능)
param(
    [ValidateSet("shot", "sim")] [string]$Mode = "shot",
    [int]$Count = 30,
    [int]$StartDepth = 1,
    [ValidateSet("", "ko", "en")] [string]$Lang = "",  # 언어 고정(저장된 선택은 바꾸지 않음). shot은 Builds\shot_<언어>_*.png 로 저장
    [switch]$BossOnly                                  # -StartDepth <보스 층>과 함께: 그 층의 다른 몬스터를 치우고 보스전만 본다
)
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project "Builds\Windows\DungeonCrawler.exe"
$depthArgs = if ($StartDepth -gt 1) { "-startdepth $StartDepth" } else { "" }
if ($BossOnly) { $depthArgs += " -bossonly" }
$langArgs = if ($Lang) { "-lang $Lang" } else { "" }

if ($Mode -eq "shot") {
    $base = Join-Path $project $(if ($Lang) { "Builds\shot_$Lang" } else { "Builds\shot" })
    Remove-Item "$base*.png" -ErrorAction SilentlyContinue
    $log = Join-Path $project "Builds\shot.log"
    $p = Start-Process $exe -ArgumentList "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile `"$log`" $depthArgs $langArgs -autoshot `"$base`"" -PassThru
    if (-not $p.WaitForExit(180000)) { $p.Kill(); "timeout" }
    Get-ChildItem "$base*.png" | ForEach-Object { $_.FullName }
} else {
    $log = Join-Path $project "Builds\sim.log"
    $p = Start-Process $exe -ArgumentList "-batchmode -nographics -logFile `"$log`" $depthArgs $langArgs -simulate $Count" -PassThru
    if (-not $p.WaitForExit(600000)) { $p.Kill(); "timeout" }
}
Select-String -Path $log -Pattern "\[SIM\]|\[AUTOSHOT\]|\[SHOT\]|\[PERF\]|\[RECORDS\]|Exception" | ForEach-Object { $_.Line }
