function New-UnityTestPlan {
    param([string] $Mode, [string] $Reason, [string[]] $EditMode = @(), [string[]] $PlayMode = @())
    return [pscustomobject]@{
        Mode = $Mode
        Reason = $Reason
        EditMode = @($EditMode | Sort-Object -Unique)
        PlayMode = @($PlayMode | Sort-Object -Unique)
    }
}

function Test-DeploymentOnlyPath {
    param([string] $Path)
    $Path = $Path.Trim('"')
    if ($Path -like 'frontend/UnityClient/Assets/WebGLTemplates/*' -or
        $Path -eq 'frontend/UnityClient/Assets/WebGLTemplates.meta' -or
        $Path -like 'frontend/design/*') {
        return $true
    }
    $isDocument = ($Path -like 'docs/*') -or ($Path -like '*.md')
    return $isDocument -and ($Path -notlike 'docs/api/*') -and ($Path -notlike 'frontend/UnityClient/*')
}

function Get-UnityTestPlan {
    param([string] $RepositoryPath, [string] $BaseCommit)
    if (-not $BaseCommit) { return New-UnityTestPlan 'All' 'No comparison base' }
    try {
        $ErrorActionPreference = 'Continue'
        & git -C $RepositoryPath rev-parse --verify --quiet "$BaseCommit^{commit}" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            & git -C $RepositoryPath fetch --quiet --no-tags --depth=1 origin $BaseCommit 2>$null
        }
        $paths = @(& git -C $RepositoryPath -c core.quotePath=false diff --name-only --no-renames $BaseCommit HEAD 2>$null)
        if ($LASTEXITCODE -ne 0) { return New-UnityTestPlan 'All' 'Comparison unavailable' }
        $ErrorActionPreference = 'Stop'
        $assetsPath = Join-Path $RepositoryPath 'frontend/UnityClient/Assets'
        $assemblies = @{}
        $guidNames = @{}
        foreach ($file in (Get-ChildItem -LiteralPath $assetsPath -Recurse -Filter '*.asmdef' -File)) {
            $definition = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            if (-not $definition.name -or $assemblies.ContainsKey($definition.name)) {
                return New-UnityTestPlan 'All' 'Invalid or duplicate assembly definition'
            }
            $relative = $file.FullName.Substring($assetsPath.Length).Replace('\', '/')
            $platform = ''
            if ($relative -match '^/Tests/(EditMode|PlayMode)/') { $platform = $Matches[1] }
            $assemblies[$definition.name] = [pscustomobject]@{
                Name = $definition.name
                Directory = $file.DirectoryName
                References = @($definition.references)
                Platform = $platform
            }
            $meta = $file.FullName + '.meta'
            if (Test-Path -LiteralPath $meta) {
                $guid = Select-String -LiteralPath $meta -Pattern '^guid: ([0-9a-f]{32})$'
                if ($guid) { $guidNames[$guid.Matches[0].Groups[1].Value] = $definition.name }
            }
        }
        $affected = [System.Collections.Generic.HashSet[string]]::new()
        $deploymentChanged = $false
        foreach ($path in $paths) {
            if (Test-DeploymentOnlyPath $path) {
                if ($path -like 'frontend/*') { $deploymentChanged = $true }
                continue
            }
            if ($path -like 'backend/*' -or $path -like 'ai/*') { continue }
            if ($path -notmatch '^frontend/UnityClient/Assets/.*\.cs(\.meta)?$') {
                return New-UnityTestPlan 'All' "Shared or unmapped input: $path"
            }
            $directory = [System.IO.Path]::GetDirectoryName((Join-Path $RepositoryPath $path))
            $owner = $null
            while ($directory.StartsWith($assetsPath, [StringComparison]::OrdinalIgnoreCase)) {
                $owners = @($assemblies.Values | Where-Object { $_.Directory -eq $directory })
                if ($owners.Count -gt 1) { return New-UnityTestPlan 'All' 'Ambiguous assembly definitions' }
                if ($owners.Count -eq 1) {
                    $owner = $owners[0]
                    break
                }
                $directory = [System.IO.Path]::GetDirectoryName($directory)
            }
            if (-not $owner) { return New-UnityTestPlan 'All' "Assembly not found: $path" }
            $affected.Add($owner.Name) | Out-Null
        }
        if ($affected.Count -eq 0) {
            return New-UnityTestPlan 'Deployment' 'Web templates, design or documents only' @('A305.Deployment.Tests')
        }
        # Include every assembly that references a changed assembly, including indirect callers.
        do {
            $expanded = $false
            foreach ($assembly in $assemblies.Values) {
                if ($affected.Contains($assembly.Name)) { continue }
                foreach ($reference in $assembly.References) {
                    if ($reference -like 'GUID:*') { $reference = $guidNames[$reference.Substring(5)] }
                    if ($reference -and $affected.Contains($reference)) {
                        $affected.Add($assembly.Name) | Out-Null
                        $expanded = $true
                        break
                    }
                }
            }
        } while ($expanded)
        $selected = @($assemblies.Values | Where-Object { $affected.Contains($_.Name) -and $_.Platform })
        if ($selected.Count -eq 0) { return New-UnityTestPlan 'All' 'No mapped tests for changed code' }
        $editMode = @($selected | Where-Object { $_.Platform -eq 'EditMode' } | ForEach-Object { $_.Name })
        $playMode = @($selected | Where-Object { $_.Platform -eq 'PlayMode' } | ForEach-Object { $_.Name })
        if ($deploymentChanged) { $editMode += 'A305.Deployment.Tests' }
        return New-UnityTestPlan 'Selected' 'Changed assemblies and transitive dependents' $editMode $playMode
    }
    catch {
        return New-UnityTestPlan 'All' 'Could not determine affected assemblies'
    }
}
