# 유니티 에디터를 배치 모드로 실행해 컴파일 + 윈도우 빌드를 한다.
# 에디터에서 프로젝트를 열어둔 상태에서는 실행할 수 없다(프로젝트 잠금).
$ErrorActionPreference = "Stop"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
$project = Split-Path $PSScriptRoot -Parent
$log = Join-Path $project "Logs\build.log"

& $unity -batchmode -nographics -projectPath $project -executeMethod ProjectTools.BuildWindows -logFile $log | Out-Null
$code = $LASTEXITCODE

Select-String -Path $log -Pattern "error CS|Build (succeeded|failed)" | ForEach-Object { $_.Line }
exit $code
