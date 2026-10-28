[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectPath,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectDirectory = [System.IO.Path]::GetFullPath($ProjectPath)
$fixtureDirectory = [System.IO.Path]::GetFullPath($OutputPath)
$toolDirectory = Join-Path $projectDirectory 'Library/TripoTools'
$blenderName = 'blender-4.5.14-windows-x64'
$blenderExecutable = Join-Path $toolDirectory "$blenderName/blender.exe"
$archivePath = Join-Path $toolDirectory "$blenderName.zip"
$expectedArchiveHash = 'b9533d2397ac1984db4466fb23a7a4649391cca93f6e84209f9bcc60d071c8b9'

New-Item -ItemType Directory -Path $toolDirectory, $fixtureDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $blenderExecutable -PathType Leaf)) {
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        Write-Host 'Downloading the pinned Blender integration-test runtime.'
        $partialArchive = "$archivePath.partial"
        $archiveUrl = "https://download.blender.org/release/Blender4.5/$blenderName.zip"
        Invoke-WebRequest -UseBasicParsing -Uri $archiveUrl -OutFile $partialArchive
        if ((Get-FileHash -LiteralPath $partialArchive -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
            throw 'Blender archive does not match the pinned official SHA256.'
        }
        Move-Item -LiteralPath $partialArchive -Destination $archivePath
    }
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
        throw 'Cached Blender archive does not match the pinned official SHA256.'
    }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $toolDirectory -Force
}
if (-not (Test-Path -LiteralPath $blenderExecutable -PathType Leaf)) {
    throw 'The pinned Blender executable is missing after extraction.'
}

$fixtureGenerator = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '../tests/tripo/create_ci_fixtures.py')
)

function Invoke-FixtureTool {
    param([string] $ScriptPath, [string] $LogName, [string[]] $ScriptArguments = @())

    $arguments = @(
        '--background', '--factory-startup', '--disable-autoexec', '--python-exit-code', '1',
        '--python', ('"{0}"' -f $ScriptPath)
    )
    if ($ScriptArguments.Count -gt 0) {
        $arguments += '--'
        $arguments += $ScriptArguments | ForEach-Object { '"{0}"' -f $_ }
    }
    $fixtureProcess = Start-Process -FilePath $blenderExecutable -ArgumentList $arguments `
        -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $fixtureDirectory "$LogName.log") `
        -RedirectStandardError (Join-Path $fixtureDirectory "$LogName-errors.log")
    if ($fixtureProcess.ExitCode -ne 0) {
        throw "Blender test input step failed: $LogName. See the preparation logs."
    }
}

Write-Host 'Verifying the humanoid and material regression input generator.'
Invoke-FixtureTool -ScriptPath (Join-Path (Split-Path $fixtureGenerator) 'test_ci_fixtures.py') `
    -LogName 'FixtureChecks'
Write-Host 'Generating disposable humanoid and material regression inputs.'
Invoke-FixtureTool -ScriptPath $fixtureGenerator -LogName 'Blender' `
    -ScriptArguments @($fixtureDirectory)
foreach ($filename in @('Humanoid.fbx', 'MisnamedColor.fbx')) {
    $fixtureFile = Join-Path $fixtureDirectory $filename
    if (-not (Test-Path -LiteralPath $fixtureFile -PathType Leaf) -or
        (Get-Item -LiteralPath $fixtureFile).Length -eq 0) {
        throw "Blender produced no complete test input: $filename"
    }
}
$reportDirectory = Join-Path $fixtureDirectory 'roundtrip-results'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
[pscustomobject]@{
    Blender = $blenderExecutable
    Model = Join-Path $fixtureDirectory 'Humanoid.fbx'
    MaterialModel = Join-Path $fixtureDirectory 'MisnamedColor.fbx'
    Reports = $reportDirectory
}
