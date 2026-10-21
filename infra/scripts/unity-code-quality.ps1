[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('format', 'check')]
    [string] $Action
)

$ErrorActionPreference = 'Stop'
$repositoryPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$unityProjectPath = Join-Path $repositoryPath 'frontend/UnityClient'
$sourcePaths = @('Assets/Scripts', 'Assets/Tests') |
    ForEach-Object { Join-Path $unityProjectPath $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Container }

try {
    Push-Location -LiteralPath $unityProjectPath

    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restore the pinned CSharpier version. Check the .NET SDK and NuGet access.'
    }

    foreach ($sourcePath in $sourcePaths) {
        & dotnet csharpier $Action $sourcePath
        if ($LASTEXITCODE -ne 0) {
            throw 'C# formatting failed. Run the format command, then run check again.'
        }
    }

    if ($Action -eq 'format') {
        exit 0
    }

    $solutionPath = @('UnityClient.slnx', 'UnityClient.sln') |
        ForEach-Object { Join-Path $unityProjectPath $_ } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1

    if (-not $solutionPath) {
        throw 'Open the project in Unity and use Assets > Open C# Project to generate the solution before checking.'
    }

    $solutionProjects = & dotnet sln $solutionPath list
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read the Unity solution. Regenerate project files in Unity.'
    }

    $projectQueue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($projectName in $solutionProjects) {
        if ($projectName.Trim().EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            $projectQueue.Enqueue([System.IO.Path]::GetFullPath((Join-Path $unityProjectPath $projectName.Trim())))
        }
    }

    if ($projectQueue.Count -eq 0) {
        throw 'The Unity solution contains no C# projects. Regenerate project files in Unity.'
    }

    # Count only projects that the selected solution actually builds.
    $visitedProjects = @{}
    $includedSources = @{}
    while ($projectQueue.Count -gt 0) {
        $projectPath = $projectQueue.Dequeue()
        if ($visitedProjects.ContainsKey($projectPath)) {
            continue
        }

        $visitedProjects[$projectPath] = $true
        $projectDirectory = Split-Path -Parent $projectPath
        [xml] $projectXml = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
        foreach ($compileItem in $projectXml.SelectNodes('//Compile[@Include]')) {
            $includedPath = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $compileItem.Include))
            $includedSources[$includedPath] = $true
        }

        foreach ($reference in $projectXml.SelectNodes('//ProjectReference[@Include]')) {
            $referencePath = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $reference.Include))
            $projectQueue.Enqueue($referencePath)
        }
    }

    $missingSources = @(Get-ChildItem -LiteralPath $sourcePaths -Filter '*.cs' -Recurse |
        Where-Object { -not $includedSources.ContainsKey($_.FullName) })
    if ($missingSources.Count -gt 0) {
        $missingPaths = $missingSources.FullName -join [Environment]::NewLine
        throw "Regenerate project files in Unity (Assets > Open C# Project) before checking these scripts:`n$missingPaths"
    }

    & dotnet build $solutionPath --no-incremental --nologo --verbosity minimal '-p:UnityCodeQuality=true'
    if ($LASTEXITCODE -ne 0) {
        throw 'Unity C# compilation or lint checks failed. See the diagnostics above.'
    }

    Write-Host 'Unity C# formatting and lint checks passed.'
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
finally {
    Pop-Location
}
