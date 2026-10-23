[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'build')]
    [string] $Action,
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH
)

$ErrorActionPreference = 'Stop'
$repositoryPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = Join-Path $repositoryPath 'frontend/UnityClient'
$artifactPath = Join-Path $repositoryPath 'dist/unity-ci'
$editorVersion = (Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') |
    Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value

if (-not $UnityEditorPath) {
    $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Install Unity $editorVersion or set UNITY_EDITOR_PATH to its executable."
}

New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
# Unity writes results outside the checkout; only the CI artifact copies live in dist.
$runPath = Join-Path ([System.IO.Path]::GetTempPath()) ('a305-unity-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath | Out-Null

function Invoke-Unity {
    param([string] $RunName, [string[]] $AdditionalArguments)

    $logPath = Join-Path $runPath "$RunName.log"
    $arguments = @(
        '-batchmode', '-nographics', '-buildTarget', 'WebGL', '-job-worker-count', '2',
        '-projectPath', ('"{0}"' -f $projectPath),
        '-logFile', ('"{0}"' -f $logPath)
    ) + $AdditionalArguments

    Write-Host "Running Unity ${editorVersion}: $RunName"
    try {
        # Unity.exe is a GUI application; explicitly wait for the actual exit code.
        $unityProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments `
            -WindowStyle Hidden -Wait -PassThru
        if ($unityProcess.ExitCode -ne 0) {
            throw "Unity $RunName failed with exit code $($unityProcess.ExitCode). See the artifact log."
        }
    }
    finally {
        if (Test-Path -LiteralPath $logPath) {
            Copy-Item -LiteralPath $logPath -Destination $artifactPath
        }
    }
}

if ($Action -eq 'test') {
    foreach ($testPlatform in @('EditMode', 'PlayMode')) {
        $resultPath = Join-Path $runPath "$testPlatform.xml"
        try {
            Invoke-Unity -RunName $testPlatform -AdditionalArguments @(
                '-runTests', '-testPlatform', $testPlatform,
                '-testResults', ('"{0}"' -f $resultPath)
            )
            if (-not (Test-Path -LiteralPath $resultPath)) {
                throw "Unity produced no $testPlatform test result."
            }

            [xml] $results = Get-Content -LiteralPath $resultPath -Raw
            $testRun = $results.'test-run'
            if (-not $testRun -or $testRun.result -ne 'Passed' -or
                [int] $testRun.failed -ne 0 -or [int] $testRun.passed -lt 1) {
                throw "Unity $testPlatform tests did not pass. See the artifact XML."
            }
            Write-Host "$testPlatform passed: $($testRun.passed) tests."
        }
        finally {
            if (Test-Path -LiteralPath $resultPath) {
                Copy-Item -LiteralPath $resultPath -Destination $artifactPath
            }
        }
    }
}
else {
    $env:WEBGL_BUILD_DIRECTORY = Join-Path $repositoryPath 'dist/webgl'
    Invoke-Unity -RunName 'WebGL' -AdditionalArguments @(
        '-quit', '-executeMethod', 'A305.EditorTools.Deployment.WebGlBuild.Build'
    )
    $indexPath = Join-Path $env:WEBGL_BUILD_DIRECTORY 'index.html'
    $versionPath = Join-Path $env:WEBGL_BUILD_DIRECTORY 'version.json'
    if (-not (Test-Path -LiteralPath $indexPath) -or -not (Test-Path -LiteralPath $versionPath)) {
        throw 'Unity produced no complete WebGL output.'
    }
    $version = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
    $expectedCommit = if ($env:CI_COMMIT_SHA) { $env:CI_COMMIT_SHA } else { 'local' }
    if ($version.commit -ne $expectedCommit -or $version.build_type -ne 'unity-webgl') {
        throw 'WebGL version does not match the candidate commit.'
    }
    Write-Host "WebGL build passed: $expectedCommit"
}
