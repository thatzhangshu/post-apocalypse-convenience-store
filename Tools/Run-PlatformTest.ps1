param(
    [ValidateSet('Setup', 'Validate', 'ExportWeChat')]
    [string]$Action = 'Setup',
    [string]$EditorPath = 'C:/Program Files/Tuanjie/Hub/Editor/2022.3.55t3/Editor/Tuanjie.exe',
    [string]$AppId = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Editor not found: $EditorPath" }
if ($AppId -and $AppId -notmatch '^wx[0-9a-fA-F]{16}$') { throw 'Expected a WeChat AppID beginning with wx and 16 hexadecimal characters.' }
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory ("platform-" + $Action + '.log')
$previousAppId = $env:WX_APPID
try {
    if ($AppId) { $env:WX_APPID = $AppId }
    $arguments = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $projectRoot + '"'),
        '-buildTarget', 'WeixinMiniGame',
        '-executeMethod', ('ConvenienceStore.PlatformTest.Editor.SmokeTestTools.' + $Action),
        '-logFile', ('"' + $logPath + '"')
    )
    Write-Output "Running $Action. Close this project in the editor before batch execution."
    Write-Output "Log: $logPath"
    $editorProcess = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $editorProcess.WaitForExit()
    $editorProcess.Refresh()
    if ($editorProcess.ExitCode -ne 0) { throw "Editor failed with exit code $($editorProcess.ExitCode). See $logPath" }
    Select-String -LiteralPath $logPath -Pattern '\[SmokeTest\]' | ForEach-Object { $_.Line }
    Write-Output 'Batch execution succeeded. This does not certify WeChat device behavior.'
}
finally {
    $env:WX_APPID = $previousAppId
}
