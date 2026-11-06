[CmdletBinding()]
param([string] $RepositoryPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))

$ErrorActionPreference = 'Stop'
$outputPath = Join-Path $RepositoryPath 'dist/webgl'
$version = Get-Content -LiteralPath (Join-Path $outputPath 'version.json') -Raw | ConvertFrom-Json
if ($env:CI_COMMIT_SHA -notmatch '^[0-9a-f]{40}$' -or
    $version.commit -ne $env:CI_COMMIT_SHA -or $version.build_type -ne 'unity-webgl' -or
    -not (Test-Path -LiteralPath (Join-Path $outputPath 'index.html'))) {
    throw 'A complete WebGL build for this candidate is required.'
}
foreach ($identifier in @($env:CI_PROJECT_ID, $env:CI_PIPELINE_ID, $env:CI_JOB_ID)) {
    if ($identifier -notmatch '^[1-9][0-9]*$') {
        throw 'Valid CI project, pipeline and job identifiers are required.'
    }
}
if (-not $env:CI_JOB_TOKEN -or -not $env:CI_API_V4_URL) {
    throw 'The CI job token and API URL are required.'
}

$archivePath = Join-Path ([System.IO.Path]::GetTempPath()) ('a305-webgl-' + [guid]::NewGuid().ToString('N') + '.tar')
$client = $null
$content = $null
$response = $null
try {
    # The build already contains gzip files; stream an uncompressed tar instead of recompressing them.
    & tar.exe -cf $archivePath -C $outputPath .
    if ($LASTEXITCODE -ne 0) { throw 'Could not archive the verified WebGL build.' }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $source = [System.IO.File]::OpenRead($archivePath)
    try {
        $checksum = [BitConverter]::ToString($sha256.ComputeHash($source)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $source.Dispose()
        $sha256.Dispose()
    }
    $metadata = @{
        schema_version = 1
        commit = $env:CI_COMMIT_SHA
        pipeline_id = $env:CI_PIPELINE_ID
        job_id = $env:CI_JOB_ID
        sha256 = $checksum
        size = (Get-Item -LiteralPath $archivePath).Length
    }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RepositoryPath 'dist/webgl-package.json') -Encoding UTF8
    $packageVersion = "$($env:CI_PIPELINE_ID)-$($env:CI_JOB_ID)"
    $url = "$($env:CI_API_V4_URL)/projects/$($env:CI_PROJECT_ID)/packages/generic/a305-webgl/$packageVersion/webgl.tar"

    Add-Type -AssemblyName System.Net.Http
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(10)
    $client.DefaultRequestHeaders.Add('JOB-TOKEN', $env:CI_JOB_TOKEN)
    $content = [System.Net.Http.StreamContent]::new([System.IO.File]::OpenRead($archivePath))
    $content.Headers.ContentLength = $metadata.size
    $response = $client.PutAsync($url, $content).GetAwaiter().GetResult()
    if ([int] $response.StatusCode -ne 201) {
        throw "WebGL package upload failed with HTTP $([int] $response.StatusCode)."
    }
    Write-Host "Published verified WebGL package ${packageVersion}: $($metadata.size) bytes."
}
finally {
    if ($response) { $response.Dispose() }
    if ($content) { $content.Dispose() }
    if ($client) { $client.Dispose() }
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath }
}
