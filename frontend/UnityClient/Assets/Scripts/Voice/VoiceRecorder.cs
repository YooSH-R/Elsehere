using System;
using UnityEngine;

namespace A305.Voice
{
    /// <summary>
    /// [말하기] 녹음 컴포넌트. 플랫폼에 맞는 마이크를 골라 <see cref="VoiceRecordingFlow"/>에 넘기고, 매 프레임 시각을 전달한다.
    /// </summary>
    public class VoiceRecorder : MonoBehaviour
    {
        [SerializeField]
        private VoiceRecorderSettings settings;

        private VoiceRecordingFlow flow;

        public event Action<MicrophoneAccess> AccessChanged;
        public event Action<RecordedVoice> RecordingFinished;

        public MicrophoneAccess? Access => flow.Access;
        public bool IsRecording => flow.IsRecording;
        public bool IsHearingVoice => flow.IsHearingVoice;

        /// <summary>녹음을 시작했지만 마이크가 아직 열리는 중인지. 이때 한 말은 녹음되지 않는다.</summary>
        public bool IsPreparing => flow.IsPreparing;
        public float CurrentLevel => flow.CurrentLevel;
        public float ElapsedSeconds => flow.ElapsedSeconds;
        public float MaxRecordingSeconds => settings.MaxRecordingSeconds;

        /// <summary>녹음 자동 종료에 쓰는 무음 기준 음량(RMS). 녹음 후 작은 목소리를 판정할 때도 기준으로 쓴다.</summary>
        public float SilenceLevel => settings.SilenceLevel;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IMicrophone microphone = new BrowserMicrophone();
#else
            IMicrophone microphone = new UnityMicrophone(settings.MaxRecordingSeconds);
#endif
            flow = new VoiceRecordingFlow(
                microphone,
                new RecordingSession(
                    settings.MaxRecordingSeconds,
                    settings.SilenceSeconds,
                    settings.SilenceLevel
                )
            );
            // 다른 컴포넌트가 이 컴포넌트보다 먼저 Awake에서 구독해도 받도록 이벤트는 이 컴포넌트에 둔다.
            flow.AccessChanged += access => AccessChanged?.Invoke(access);
            flow.RecordingFinished += voice => RecordingFinished?.Invoke(voice);
        }

        public void RequestAccess()
        {
            flow.RequestAccess();
        }

        /// <summary>대화를 시작할 때 부른다. 마이크를 미리 열어 [말하기]를 누르자마자 녹음되게 한다.</summary>
        public void KeepMicrophoneOpen()
        {
            flow.KeepMicrophoneOpen();
        }

        /// <summary>대화가 끝나면 부른다. 미리 연 마이크를 닫는다.</summary>
        public void ReleaseMicrophone()
        {
            flow.ReleaseMicrophone();
        }

        public void StartRecording()
        {
            flow.StartRecording(Time.unscaledTime);
        }

        /// <summary>사용자가 [종료]를 눌렀을 때 부른다.</summary>
        public void StopRecording()
        {
            flow.StopRecording(Time.unscaledTime);
        }

        private void Update()
        {
            flow.Update(Time.unscaledTime);
        }

        // 대화 중에 씬을 떠나면 대화 끝을 거치지 않으므로, 미리 연 마이크가 켜진 채 남지 않게 닫는다.
        private void OnDestroy()
        {
            flow?.ReleaseMicrophone();
        }

        // 녹음 중 다른 탭·창으로 가면 멈춘다. 브라우저가 백그라운드 탭의 소리 처리를 멈추거나 늦출 수 있기 때문이다.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                flow.Interrupt(Time.unscaledTime);
            }
        }
    }
}
