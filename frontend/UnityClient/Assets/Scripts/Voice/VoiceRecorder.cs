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
        public float CurrentLevel => flow.CurrentLevel;
        public float ElapsedSeconds => flow.ElapsedSeconds;
        public float MaxRecordingSeconds => settings.MaxRecordingSeconds;

        /// <summary>이 음량(RMS)보다 작으면 조용한 것으로 본다. 녹음에 말소리가 있는지 볼 때도 같은 기준을 쓴다.</summary>
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
