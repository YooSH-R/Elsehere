[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'build')]
    [string] $Action,
    [string] $UnityEditorPath = $env:UNITY_EDITOR_PATH,
    # MR checks pass their merge base so web-template-only changes run just the deployment tests.
    [string] $ChangedSince
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
        # Start-Process -Wait also waits for descendants left behind by a crashed Editor.
        # Wait on the Editor itself so its failure reaches CI and preserves the log.
        $unityProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments `
            -WindowStyle Hidden -PassThru
        $unityProcess.WaitForExit()
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

function Test-DeploymentOnlyPath {
    param([string] $Path)

    # Git quotes non-ASCII names; only the ASCII folder prefix and extension matter here.
    $Path = $Path.Trim('"')
    $deploymentOnlyPatterns = @(
        'frontend/UnityClient/Assets/WebGLTemplates/*',
        'frontend/UnityClient/Assets/WebGLTemplates.meta',
        'frontend/design/*'
    )
    foreach ($pattern in $deploymentOnlyPatterns) {
        if ($Path -like $pattern) {
            return $true
        }
    }
    # Unity never loads documents outside its project; the API contract still affects the client.
    $isDocument = ($Path -like 'docs/*') -or ($Path -like '*.md')
    return $isDocument -and ($Path -notlike 'docs/api/*') -and ($Path -notlike 'frontend/UnityClient/*')
}

function Get-TestScope {
    param([string] $BaseCommit)

    if (-not $BaseCommit) {
        return 'All'
    }
    # A failed comparison must widen the run instead of stopping the required check.
    $ErrorActionPreference = 'Continue'
    # MR checkouts are shallow, so the merge base may need to be fetched first.
    & git -C $repositoryPath rev-parse --verify --quiet "$BaseCommit^{commit}" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        & git -C $repositoryPath fetch --quiet --no-tags --depth=1 origin $BaseCommit
    }
    # Without rename pairing, moving a script into the template folder still lists the removed script.
    $changedPaths = @(& git -C $repositoryPath diff --name-only --no-renames $BaseCommit HEAD)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Unity test scope: All (could not compare with $BaseCommit)"
        return 'All'
    }
    $playAffectingPath = $changedPaths |
        Where-Object { -not (Test-DeploymentOnlyPath $_) } |
        Select-Object -First 1
    if ($playAffectingPath) {
        Write-Host "Unity test scope: All ($playAffectingPath changed)"
        return 'All'
    }
    Write-Host 'Unity test scope: Deployment (only web templates, design images or documents changed)'
    return 'Deployment'
}

if ($Action -eq 'test' -and (Get-TestScope -BaseCommit $ChangedSince) -eq 'Deployment') {
    # Play code and Tripo inputs are unchanged, so only the template checks can change.
    Invoke-UnityTests -TestPlatform 'EditMode' -AssemblyNames 'A305.Deployment.Tests'
}
elseif ($Action -eq 'test') {
    $inputDirectory = Join-Path $runPath 'Tripo'
    try {
        # The committed cafe character is required by the master-rig regression test.
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
        foreach ($testPlatform in @('EditMode', 'PlayMode')) {
            Invoke-UnityTests -TestPlatform $testPlatform
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
