[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'build')]
    [string] $Action,
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH,
    # MR checks pass their merge base to select affected test assemblies.
    [string] $ChangedSince,
    [ValidateRange(1, 3600)]
    [int] $ProgressIntervalSeconds = 30
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
    $elapsed = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Start-Process -Wait also waits for descendants left behind by a crashed Editor.
        # Wait on the Editor itself so its failure reaches CI and preserves the log.
        $unityProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments `
            -WindowStyle Hidden -PassThru
        while (-not $unityProcess.WaitForExit($ProgressIntervalSeconds * 1000)) {
            if (Test-Path -LiteralPath $logPath) {
                Copy-Item -LiteralPath $logPath -Destination $artifactPath
            }
            Write-Host "Unity $RunName still running: elapsed=$([int] $elapsed.Elapsed.TotalSeconds)s; log=$artifactPath/$RunName.log"
        }
        Write-Host "Unity $RunName exited: elapsed=$([int] $elapsed.Elapsed.TotalSeconds)s; exit_code=$($unityProcess.ExitCode)"
        if ($unityProcess.ExitCode -ne 0) {
            throw "Unity $RunName failed with exit code $($unityProcess.ExitCode). See the artifact log."
        }
    }
    finally {
        $elapsed.Stop()
        if (Test-Path -LiteralPath $logPath) {
            Copy-Item -LiteralPath $logPath -Destination $artifactPath
        }
    }
}

function Read-UnityTestResults {
    param([string] $ResultPath, [string] $TestPlatform)

    if (-not (Test-Path -LiteralPath $ResultPath)) {
        throw "Unity produced no $TestPlatform test result."
    }
    [xml] $results = Get-Content -LiteralPath $ResultPath -Raw
    $testRun = $results.'test-run'
    if (-not $testRun -or $testRun.result -ne 'Passed' -or
        [int] $testRun.failed -ne 0 -or [int] $testRun.passed -lt 1 -or
        [int] $testRun.skipped -ne 0) {
        throw "Unity $TestPlatform tests did not pass. See the artifact XML."
    }
    return $testRun
}

function Invoke-UnityTests {
    param([string] $TestPlatform, [string] $AssemblyNames)

    $resultPath = Join-Path $runPath "$TestPlatform.xml"
    $testArguments = @(
        '-runTests', '-testPlatform', $TestPlatform,
        '-testResults', ('"{0}"' -f $resultPath)
    )
    if ($AssemblyNames) {
        $testArguments += @('-assemblyNames', $AssemblyNames)
    }
    try {
        Invoke-Unity -RunName $TestPlatform -AdditionalArguments $testArguments
        $testRun = Read-UnityTestResults -ResultPath $resultPath -TestPlatform $TestPlatform
        Write-Host "$TestPlatform results: passed=$($testRun.passed), skipped=$($testRun.skipped), failed=$($testRun.failed)"
    }
    finally {
        if (Test-Path -LiteralPath $resultPath) {
            Copy-Item -LiteralPath $resultPath -Destination $artifactPath
        }
    }
}

if ($Action -eq 'test') {
    . "$PSScriptRoot/unity-test-scope.ps1"
    $plan = Get-UnityTestPlan -RepositoryPath $repositoryPath -BaseCommit $ChangedSince
    $plan | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifactPath 'test-plan.json') -Encoding UTF8
    Write-Host "Unity test scope: $($plan.Mode) ($($plan.Reason))"
    Write-Host "EditMode assemblies: $($plan.EditMode -join ';')"
    Write-Host "PlayMode assemblies: $($plan.PlayMode -join ';')"
    $prepareInputs = $plan.Mode -eq 'All' -or @($plan.EditMode + $plan.PlayMode | Where-Object { $_ -like 'A305.Tripo.*' }).Count -gt 0
    $inputDirectory = Join-Path $runPath 'Tripo'
    try {
        if ($prepareInputs) {
            if (-not $env:TRIPO_MASTER_RECORD) {
                $env:TRIPO_MASTER_RECORD = 'Assets/Data/Tripo/100e191da12a84145a71d737bcfd57c2.asset'
            }
            if (-not $env:TRIPO_MASTER_SCENE) {
                $env:TRIPO_MASTER_SCENE = 'Assets/Scenes/Sandbox/Tripo/LittleMung_Cafe_Female_112b39c5_HandBinding.unity'
            }
            $testInputs = & "$PSScriptRoot/prepare-unity-test-inputs.ps1" `
                -ProjectPath $projectPath -OutputPath $inputDirectory
            $env:TRIPO_REVIEW_BLENDER = $testInputs.Blender
            $env:TRIPO_REVIEW_MODEL = $testInputs.Model
            $env:TRIPO_REVIEW_OUTPUT = $testInputs.Reports
            $env:TRIPO_MATERIAL_TEST_FBX = $testInputs.MaterialModel
        }
        foreach ($testPlatform in @('EditMode', 'PlayMode')) {
            if ($plan.Mode -eq 'All') {
                Invoke-UnityTests -TestPlatform $testPlatform
            }
            elseif (@($plan.$testPlatform).Count -gt 0) {
                Invoke-UnityTests -TestPlatform $testPlatform -AssemblyNames ($plan.$testPlatform -join ';')
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $inputDirectory) {
            $inputArtifacts = Join-Path $artifactPath 'Tripo'
            New-Item -ItemType Directory -Path $inputArtifacts -Force | Out-Null
            Get-ChildItem -LiteralPath $inputDirectory |
                Copy-Item -Destination $inputArtifacts -Recurse -Force
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
