// Unity Microphone 클래스는 Web 빌드 대상에는 없어 Web 빌드에서는 이 파일을 컴파일하지 않는다.
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;

namespace A305.Voice
{
    /// <summary>Editor·데스크톱의 마이크. Unity Microphone 클래스로 16kHz에서 바로 녹음한다.</summary>
    public sealed class UnityMicrophone : IMicrophone
    {
        private const int SampleRate = 16000;

        // 음량을 잴 최근 구간(약 0.06초).
        private const int LevelWindowSize = 1024;

        private readonly int clipSeconds;
        private readonly float[] levelWindow = new float[LevelWindowSize];
        private AudioClip clip;

        /// <param name="maxRecordingSeconds">최대 녹음 시간. 녹음 버퍼는 이보다 1초 길게 잡는다.</param>
        public UnityMicrophone(float maxRecordingSeconds)
        {
            clipSeconds = Mathf.CeilToInt(maxRecordingSeconds) + 1;
        }

        public MicrophoneAccess? Access { get; private set; }

        public float CurrentLevel
        {
            get
            {
                if (clip == null)
                {
                    return 0f;
                }

                int position = Microphone.GetPosition(null);
                if (position < LevelWindowSize)
                {
                    return 0f;
                }

                clip.GetData(levelWindow, position - LevelWindowSize);
                float sum = 0f;
                foreach (float sample in levelWindow)
                {
                    sum += sample * sample;
                }

                return Mathf.Sqrt(sum / LevelWindowSize);
            }
        }

        public void RequestAccess()
        {
            Access =
                Microphone.devices.Length > 0
                    ? MicrophoneAccess.Granted
                    : MicrophoneAccess.NoDevice;
        }

        public void StartRecording()
        {
            // 브라우저처럼 녹음을 시작할 때 마이크가 있는지 다시 확인한다. 없으면 녹음 진행이 녹음을 취소한다.
            RequestAccess();
            if (Access == MicrophoneAccess.Granted)
            {
                clip = Microphone.Start(null, false, clipSeconds, SampleRate);
            }
        }

        public CapturedAudio StopRecording()
        {
            // 녹음 위치는 End를 부르기 전에 읽어야 한다.
            int position = Microphone.GetPosition(null);
            Microphone.End(null);
            var samples = new float[position];
            if (clip != null && position > 0)
            {
                clip.GetData(samples, 0);
            }

            clip = null;
            return new CapturedAudio(samples, SampleRate);
        }
    }
}
#endif
