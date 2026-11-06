using System;
using System.Threading.Tasks;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Diagnostics;
using System.Text;
#endif

namespace A305.Voice
{
    /// <summary>학습자 영어 표현을 기기의 영어 음성으로 읽는다. 서버 대화나 추가 AI 호출을 만들지 않는다.</summary>
    public static class TextPronunciation
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int A305Word_Pronounce(string text);

        [DllImport("__Internal")]
        private static extern void A305Word_StopPronunciation();
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static Process speaking;

        // 문장은 표준 입력으로만 전달한다. 명령문에 사용자 발화를 삽입하지 않는다.
        private const string SpeechScript =
            @"
[Console]::InputEncoding = [Text.Encoding]::UTF8
Add-Type -AssemblyName System.Speech
$voice = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $english = $voice.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.TwoLetterISOLanguageName -eq 'en' } | Select-Object -First 1
    if (-not $english) { exit 2 }
    $voice.SelectVoice($english.VoiceInfo.Name)
    $voice.Rate = -1
    $voice.Speak([Console]::In.ReadToEnd())
} finally { $voice.Dispose() }
";
#endif

        public static async Task<bool> SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            Stop();
#if UNITY_WEBGL && !UNITY_EDITOR
            return A305Word_Pronounce(text) == 1;
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Process process = null;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName =
                            Environment.GetFolderPath(Environment.SpecialFolder.System)
                            + @"\WindowsPowerShell\v1.0\powershell.exe",
                        Arguments =
                            "-NoProfile -NonInteractive -EncodedCommand "
                            + Convert.ToBase64String(Encoding.Unicode.GetBytes(SpeechScript)),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardInput = true,
                        StandardInputEncoding = new UTF8Encoding(false),
                    },
                };
                process.Start();
                speaking = process;
                process.StandardInput.Write(text);
                process.StandardInput.Close();
                await Task.Run(() => process.WaitForExit());
                return speaking != process || process.ExitCode == 0;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"기기 영어 음성을 재생하지 못했습니다: {exception.GetType().Name}"
                );
                return false;
            }
            finally
            {
                if (speaking == process)
                    speaking = null;
                process?.Dispose();
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public static void Stop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            A305Word_StopPronunciation();
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Process process = speaking;
            speaking = null;
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
                // 재생 종료와 중지가 겹쳐 이미 끝난 프로세스다.
            }
#endif
        }
    }
}
