[CmdletBinding()]
param([switch] $Check)

$ErrorActionPreference = 'Stop'
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$contentPath = Join-Path $repositoryPath 'backend/src/main/resources/content/object-words.json'
$previewPath = Join-Path $repositoryPath 'frontend/UnityClient/Assets/Data/ObjectHover/ObjectWords.json'
$seedPath = Join-Path $repositoryPath 'backend/src/main/resources/db/seed/world.sql'
$json = [IO.File]::ReadAllText($contentPath).Replace("`r`n", "`n")
$words = ($json | ConvertFrom-Json).words
if ($null -eq $words -or $words.Count -eq 0) { throw 'The shared word catalog must contain words.' }
$keys = @{}
$expressions = @{}
foreach ($word in $words) {
    foreach ($field in @('key', 'languageCode', 'text', 'meaningKo', 'example', 'exampleKo')) {
        if ([string]::IsNullOrWhiteSpace($word.$field)) { throw "Missing $field on word $($word.key)." }
    }
    if ($word.key -cnotmatch '^[a-z][a-z0-9_]*$') { throw "Invalid word key: $($word.key)" }
    if ($keys.ContainsKey($word.key)) { throw "Duplicate word key: $($word.key)" }
    $keys[$word.key] = $true
    $expression = "$($word.languageCode):$($word.text)"
    if ($expressions.ContainsKey($expression)) { throw "Duplicate vocabulary: $expression" }
    $expressions[$expression] = $true
}

function ConvertTo-SqlLiteral([string] $value) {
    return "'" + $value.Replace("'", "''") + "'"
}

$rows = foreach ($word in $words) {
    $values = @($word.languageCode, 'WORD', $word.text, $word.meaningKo, $word.example, $word.exampleKo, 'MASTER') |
        ForEach-Object { ConvertTo-SqlLiteral $_ }
    $audio = if ([string]::IsNullOrWhiteSpace($word.pronunciationAudioKey)) { 'NULL' } else { ConvertTo-SqlLiteral $word.pronunciationAudioKey }
    '  (' + (($values + $audio) -join ', ') + ')'
}
$generated = @"
-- BEGIN GENERATED OBJECT WORDS: infra/scripts/sync-object-words.ps1
INSERT INTO vocabularies (language_code, type, text, meaning_ko, example, example_ko, source, pronunciation_audio_key)
VALUES
$($rows -join ",`n")
ON CONFLICT (language_code, text) DO UPDATE
SET type = EXCLUDED.type,
    meaning_ko = EXCLUDED.meaning_ko,
    example = EXCLUDED.example,
    example_ko = EXCLUDED.example_ko,
    source = EXCLUDED.source,
    pronunciation_audio_key = EXCLUDED.pronunciation_audio_key,
    updated_at = now();
-- END GENERATED OBJECT WORDS
"@
$seed = [IO.File]::ReadAllText($seedPath).Replace("`r`n", "`n")
$pattern = '(?s)-- BEGIN GENERATED OBJECT WORDS:.*?-- END GENERATED OBJECT WORDS'
if ($seed -notmatch $pattern) { throw 'The generated word section is missing in world.sql.' }
$updatedSeed = [regex]::Replace($seed, $pattern, [System.Text.RegularExpressions.MatchEvaluator] { param($match) $generated })
if ($Check) {
    if ([IO.File]::ReadAllText($previewPath).Replace("`r`n", "`n") -cne $json) { throw 'Unity words are stale. Run sync-object-words.ps1.' }
    if ($seed -cne $updatedSeed) { throw 'SQL words are stale. Run sync-object-words.ps1.' }
    Write-Output "Shared catalog verified: $($words.Count) words; Unity and SQL match."
    exit 0
}
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($previewPath, $json, $encoding)
[IO.File]::WriteAllText($seedPath, $updatedSeed, $encoding)
Write-Output "Synchronized $($words.Count) words to Unity and SQL."
