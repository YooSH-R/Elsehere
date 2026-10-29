param(
    [string]$Text = 'May I help you. I find very happy.',
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$BaseName = 'CafeFemale_TwoSentences',
    [string]$Voice = 'Microsoft Zira Desktop'
)
$ErrorActionPreference = 'Stop'
if ($BaseName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple file name without a path.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$wavePath = Join-Path $directory ($BaseName + '.wav')
$jsonPath = Join-Path $directory ($BaseName + '.json')
if ((Test-Path -LiteralPath $wavePath) -or (Test-Path -LiteralPath $jsonPath)) {
    throw 'Use a new output directory/name to preserve existing speech.'
}
Add-Type -AssemblyName System.Speech
Add-Type -ReferencedAssemblies System.Speech -TypeDefinition @"
using System;
using System.IO;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.Threading;
public sealed class LocalViseme {
    public double start;
    public int viseme;
}
public sealed class LocalWord {
    public double start;
    public string text;
}
public sealed class LocalSpeechResult {
    public List<LocalViseme> cues = new List<LocalViseme>();
    public List<LocalWord> words = new List<LocalWord>();
    public double duration;
}
public static class LocalSpeechExport {
    public static LocalSpeechResult Create(string text, string voice, string path) {
        var result = new LocalSpeechResult();
        using (var speech = new SpeechSynthesizer())
        using (var done = new ManualResetEvent(false)) {
            speech.SelectVoice(voice);
            if (speech.Voice.Culture.Name != "en-US")
                throw new InvalidOperationException("This viseme mapping requires an en-US voice.");
            speech.Rate = 0;
            // Zira's viseme offsets use its 16 kHz clock. A 22.05 kHz output
            // produced drift on this engine; keep PCM and event clocks identical.
            speech.SetOutputToWaveFile(path, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            speech.VisemeReached += (sender, e) => result.cues.Add(new LocalViseme { start=e.AudioPosition.TotalSeconds, viseme=e.Viseme });
            speech.SpeakProgress += (sender, e) => result.words.Add(new LocalWord { start=e.AudioPosition.TotalSeconds, text=e.Text });
            Exception failure = null;
            speech.SpeakCompleted += (sender, e) => { failure=e.Error; done.Set(); };
            speech.SpeakAsync(text);
            if (!done.WaitOne(TimeSpan.FromSeconds(60))) {
                speech.SpeakAsyncCancelAll();
                throw new TimeoutException("Offline speech generation timed out.");
            }
            if (failure != null) throw failure;
            speech.SetOutputToNull();
        }
        using (var reader = new BinaryReader(File.OpenRead(path))) {
            reader.BaseStream.Position = 12;
            uint bytesPerSecond = 0;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
                string chunk = new string(reader.ReadChars(4));
                uint length = reader.ReadUInt32();
                if (chunk == "fmt ") {
                    long next = reader.BaseStream.Position + length + (length % 2);
                    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32();
                    bytesPerSecond = reader.ReadUInt32();
                    reader.BaseStream.Position = next;
                    continue;
                }
                if (chunk == "data") { result.duration=length/(double)bytesPerSecond; break; }
                reader.BaseStream.Position += length + (length % 2);
            }
        }
        if (result.duration <= 0 || result.cues.Count == 0)
            throw new InvalidOperationException("The voice must produce both PCM audio and viseme timing.");
        return result;
    }
}
"@
$result = [LocalSpeechExport]::Create($Text, $Voice, $wavePath)
# Keep the last event at each timestamp, and derive non-overlapping intervals
# from the TTS audio clock rather than guessed word or character durations.
$events = @($result.cues | Group-Object start | ForEach-Object { $_.Group[-1] } | Sort-Object start)
if (@($events | Where-Object { $_.start -lt 0 -or $_.start -gt $result.duration -or $_.viseme -lt 0 -or $_.viseme -gt 21 }).Count -gt 0) {
    throw 'TTS viseme timing does not match the WAV. Do not import this output.'
}
$cues = @()
for ($i = 0; $i -lt $events.Count; $i++) {
    $end = $result.duration
    if ($i + 1 -lt $events.Count) { $end = $events[$i + 1].start }
    if ($end -gt $events[$i].start) {
        $cues += [ordered]@{ start=$events[$i].start; end=$end; viseme=$events[$i].viseme }
    }
}
$manifest = [ordered]@{
    version=1; provider='windows-sapi-en-US'; text=$Text; voice=$Voice
    audioFile=($BaseName + '.wav'); audioSha256=(Get-FileHash -LiteralPath $wavePath -Algorithm SHA256).Hash
    duration=$result.duration; cues=$cues; words=@($result.words)
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
Write-Output "$wavePath`n$jsonPath"
