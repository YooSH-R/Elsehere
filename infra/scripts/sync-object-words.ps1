[CmdletBinding()]
param(
    [switch] $Check,
    [string] $UnityPath = $env:UNITY_EDITOR_PATH,
    [string] $LogPath = (Join-Path ([IO.Path]::GetTempPath()) 'a305-object-word-export.log')
)

$ErrorActionPreference = 'Stop'
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = Join-Path $repositoryPath 'frontend/UnityClient'
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $editorVersion = $versionLine.Substring('m_EditorVersion: '.Length)
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath)) {
    throw 'Unity Editor not found. Pass -UnityPath or set UNITY_EDITOR_PATH.'
}
$method = if ($Check) { 'CheckExports' } else { 'Export' }
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', "A305.EditorTools.ObjectHover.ObjectWordAuthoring.$method",
    '-quit', '-logFile', ('"' + $LogPath + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $LogPath) { Get-Content -LiteralPath $LogPath -Tail 25 }
    throw "Unity word export failed. Log: $LogPath. If the project is already open, use Tools/A305/ObjectHover in the Editor."
}
Write-Output "Unity word assets verified/exported. Log: $LogPath"
