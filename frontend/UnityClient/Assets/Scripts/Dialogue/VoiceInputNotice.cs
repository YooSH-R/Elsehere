using A305.Voice;

namespace A305.Dialogue
{
    /// <summary>
    /// [말하기] 안내 문구. 녹음 중인지, 녹음이 왜 끝났는지, 마이크를 왜 쓸 수 없는지를 알려 사용자가 다음에 할 일을 알 수 있게 한다.
    /// </summary>
    public static class VoiceInputNotice
    {
        /// <summary>말하지 않고 [종료]를 눌러 녹음에 말소리가 없을 때. 전사하지 않는다.</summary>
        public const string TooShort =
            "말소리가 없었어요. [말하기]를 누르고 말한 뒤 [종료]를 눌러요.";

        /// <summary>
        /// 음성 처리에 동의하지 않아 음성 입력을 쓸 수 없을 때(API 명세 CONV-02). 회색 [말하기]를 누르면 동의 창이 떠
        /// 그 자리에서 켤 수 있으므로 [말하기]로 안내한다.
        /// </summary>
        public const string ConsentRequired =
            "음성으로 말하려면 [말하기]를 눌러 음성 처리에 동의해 주세요.";

        // 입력칸 위 한 줄에 녹음이 끝난 이유와 함께 들어가도록 짧게 쓴다. 다시 말하는 버튼은 바로 옆에 보인다.
        private const string CheckTranscript = "들은 문장이 맞으면 [보내기]를 눌러요.";

        public static string Listening(float maxSeconds)
        {
            // 목소리가 들리는지는 [종료] 버튼의 초록불로 보이므로, 상태 줄 한 줄에는 끝내는 방법만 쓴다.
            return $"듣고 있어요. 다 말했으면 [종료]를 눌러 주세요. (최대 {maxSeconds:0}초)";
        }

        /// <summary>전사한 문장을 입력창에 넣은 뒤의 안내. [종료]를 누르지 않았는데 녹음이 끝났으면 그 이유를 먼저 알린다.</summary>
        public static string Transcribed(RecordingEndReason endReason)
        {
            switch (endReason)
            {
                case RecordingEndReason.MaxDuration:
                    return "녹음 시간이 다 되어 멈췄어요. " + CheckTranscript;
                case RecordingEndReason.Silence:
                    return "말이 멈춰서 녹음을 마쳤어요. " + CheckTranscript;
                case RecordingEndReason.Interrupted:
                    return "창을 옮겨 녹음을 멈췄어요. " + CheckTranscript;
                default:
                    return CheckTranscript;
            }
        }

        /// <summary>마이크를 쓸 수 없을 때의 안내. 권한 거부·장치 없음·미지원은 해결 방법이 달라 나눠 안내한다(기능 명세 F-41).</summary>
        public static string MicrophoneUnavailable(MicrophoneAccess access)
        {
            switch (access)
            {
                case MicrophoneAccess.Denied:
                    return "마이크 권한이 꺼져 있어요. 주소창의 사이트 설정에서 마이크를 허용하거나, 텍스트로 입력해 주세요.";
                case MicrophoneAccess.NoDevice:
                    return "마이크를 찾지 못했어요. 마이크 연결을 확인하거나, 텍스트로 입력해 주세요.";
                case MicrophoneAccess.Unsupported:
                    return "이 브라우저나 주소에서는 마이크를 쓸 수 없어요. 텍스트로 입력해 주세요.";
                default:
                    return null;
            }
        }
    }
}
