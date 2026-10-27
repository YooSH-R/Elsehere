[CmdletBinding()]
param([string] $ArchivePath)

$ErrorActionPreference = 'Stop'
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = Join-Path $repositoryPath 'frontend/UnityClient'
$packagePath = Join-Path $projectPath 'Packages/com.tripo3d.unitybridge'
$expectedHash = 'B2B5C84FC780A621B9B985F670DAB5391304B62EBBA82CA5260C9AAD19844532'
$downloadUrl = 'https://tripo-public.tripo3d.ai/plugins/unity-bridge/Tripo3d_Unity_Bridge-latest.zip'
$cachePath = Join-Path $projectPath 'Library/TripoTools/UnityBridge-1.0.14'

if (Test-Path -LiteralPath $packagePath) {
    $installed = Get-Content -LiteralPath (Join-Path $packagePath 'package.json') -Raw | ConvertFrom-Json
    $cleanup = Get-Content -LiteralPath (Join-Path $packagePath 'Editor/StartupCleanup.cs') -Raw
    if ($installed.version -ne '1.0.14' -or $cleanup.Contains('[InitializeOnLoad]')) {
        throw 'An existing Bridge installation differs from the reviewed version. It was not overwritten.'
    }
    Write-Host 'Tripo Bridge 1.0.14 is already installed; automatic cleanup is disabled.'
    exit 0
}

New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
if (-not $ArchivePath) {
    $ArchivePath = Join-Path $cachePath 'Tripo3d_Unity_Bridge.zip'
    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $ArchivePath
    }
}
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'The vendor archive changed. Review its version and contents before updating the pinned SHA-256.'
}

$sourcePath = Join-Path $cachePath 'Tripo3d_Unity_Bridge'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $cachePath
}
$package = Get-Content -LiteralPath (Join-Path $sourcePath 'package.json') -Raw | ConvertFrom-Json
if ($package.name -ne 'com.tripo3d.unitybridge' -or $package.version -ne '1.0.14') {
    throw 'Unexpected package identity.'
}

Copy-Item -LiteralPath $sourcePath -Destination $packagePath -Recurse
# Preserve the cached vendor original. Disable only startup cleanup in the local installed copy:
# the vendor deletes Assets/ImportedModels folders older than seven days.
$cleanupPath = Join-Path $packagePath 'Editor/StartupCleanup.cs'
$cleanup = [IO.File]::ReadAllText($cleanupPath)
if (-not $cleanup.Contains('[InitializeOnLoad]')) {
    throw 'The reviewed startup hook was not found.'
}
$cleanup = $cleanup.Replace('[InitializeOnLoad]', '// A305: automatic deletion is disabled; imported sources must be preserved.')
[IO.File]::WriteAllText($cleanupPath, $cleanup, [Text.UTF8Encoding]::new($false))
Write-Host 'Installed official Tripo Bridge 1.0.14 as a local embedded package.'
Write-Host 'Automatic startup cleanup is disabled. Open Tools > Tripo Bridge in Unity.'
