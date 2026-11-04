using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Voice
{
    /// <summary>
    /// 마이크 녹음 확인용 화면(VoiceSandbox). 권한 상태, 녹음 중 음량과 목소리가 들리는지, 끝난 이유와 WAV 크기를 보여 주고 녹음을 다시 들려준다.
    /// 확인용이라 배포용 프리팹에는 붙이지 않는다.
    /// </summary>
    public class VoiceSandboxView : MonoBehaviour
    {
        [SerializeField]
        private VoiceRecorder recorder;

        [SerializeField]
        private Button accessButton;

        [SerializeField]
        private Button talkButton;

        [SerializeField]
        private TMP_Text talkCaption;

        [SerializeField]
        private Image levelFill;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private Button playButton;

        [SerializeField]
        private AudioSource audioSource;

        private RecordedVoice lastVoice;

        private void Awake()
        {
            accessButton.onClick.AddListener(recorder.RequestAccess);
            talkButton.onClick.AddListener(ToggleRecording);
            playButton.onClick.AddListener(PlayLastVoice);
            recorder.AccessChanged += access => statusText.text = AccessMessage(access);
            recorder.RecordingFinished += ShowResult;
            playButton.interactable = false;
            statusText.text = "[마이크 권한 요청]을 누른 뒤 [말하기]를 누르고 말해 보세요.";
        }

        private void Update()
        {
            levelFill.fillAmount = Mathf.Clamp01(recorder.CurrentLevel * 5f);
            talkCaption.text = recorder.IsRecording ? "종료" : "말하기";
            if (recorder.IsRecording)
            {
                string voice = recorder.IsHearingVoice ? "목소리 들림" : "조용함";
                statusText.text =
                    $"녹음 중 {recorder.ElapsedSeconds:0.0}초 / 최대 {recorder.MaxRecordingSeconds:0}초, 음량 {recorder.CurrentLevel:0.000} ({voice})";
            }
        }

        private void ToggleRecording()
        {
            if (recorder.IsRecording)
            {
                recorder.StopRecording();
            }
            else
            {
                recorder.StartRecording();
            }
        }

        private void ShowResult(RecordedVoice voice)
        {
            lastVoice = voice;
            playButton.interactable = voice.Samples.Length > 0;
            statusText.text =
                $"끝난 이유: {EndReasonMessage(voice.EndReason)}\n"
                + $"길이 {voice.DurationSeconds:0.0}초, 16kHz 샘플 {voice.Samples.Length}개, WAV {voice.WavBytes.Length / 1024f:0.0}KB";
        }

        private void PlayLastVoice()
        {
            AudioClip clip = AudioClip.Create(
                "RecordedVoice",
                lastVoice.Samples.Length,
                1,
                lastVoice.SampleRate,
                false
            );
            clip.SetData(lastVoice.Samples, 0);
            audioSource.clip = clip;
            audioSource.Play();
        }

        private static string AccessMessage(MicrophoneAccess access)
        {
            switch (access)
            {
                case MicrophoneAccess.Granted:
                    return "마이크를 쓸 수 있어요. [말하기]를 누르고 말한 뒤 다시 눌러 끝내 보세요.";
                case MicrophoneAccess.Denied:
                    return "마이크 권한이 거부됐어요. 대화에서는 텍스트로 입력합니다.";
                case MicrophoneAccess.NoDevice:
                    return "마이크를 찾지 못했거나 다른 프로그램이 쓰고 있어요.";
                default:
                    return "이 브라우저나 주소(HTTPS가 아님)에서는 마이크를 쓸 수 없어요.";
            }
        }

        private static string EndReasonMessage(RecordingEndReason reason)
        {
            switch (reason)
            {
                case RecordingEndReason.MaxDuration:
                    return "최대 녹음 시간";
                case RecordingEndReason.Silence:
                    return "조용한 시간이 이어짐";
                case RecordingEndReason.Interrupted:
                    return "다른 탭·창으로 이동";
                default:
                    return "[종료]를 누름";
            }
        }
    }
}
