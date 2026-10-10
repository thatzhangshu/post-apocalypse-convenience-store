param(
    [ValidateSet('Setup', 'Validate', 'BuildWindows', 'ExportWeChat')]
    [string]$Action = 'Setup',
    [string]$EditorPath = 'C:/Program Files/Tuanjie/Hub/Editor/2022.3.55t3/Editor/Tuanjie.exe',
    [string]$AppId = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Editor not found: $EditorPath" }
if ($AppId -and $AppId -notmatch '^wx[0-9a-fA-F]{16}$') { throw 'Invalid WeChat AppID.' }
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'Logs') | Out-Null
$logPath = Join-Path $projectRoot ("Logs/prototype-" + $Action + '.log')
$previousAppId = $env:WX_APPID
try {
    if ($AppId) { $env:WX_APPID = $AppId }
    $method = 'ConvenienceStore.Prototype.Editor.PrototypeTools.' + $Action
    $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod', $method, '-logFile', ('"' + $logPath + '"'))
    if ($Action -eq 'ExportWeChat') { $arguments += @('-buildTarget', 'WeixinMiniGame') }
    if ($Action -eq 'BuildWindows') { $arguments += @('-buildTarget', 'Win64') }
    Write-Output "Running prototype $Action. Log: $logPath"
    $editorProcess = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $editorProcess.WaitForExit(); $editorProcess.Refresh()
    if ($editorProcess.ExitCode -ne 0) { throw "Editor failed with exit code $($editorProcess.ExitCode). See $logPath" }
    Select-String -LiteralPath $logPath -Pattern '\[Prototype\]' | ForEach-Object { $_.Line }
}
finally { $env:WX_APPID = $previousAppId }
