#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace A305.Voice
{
    /// <summary>Web 빌드의 마이크. 소리를 모으고 음량을 재는 일은 VoiceMicrophone.jslib이 맡는다.</summary>
    public sealed class BrowserMicrophone : IMicrophone
    {
        private const int StateWaiting = 0;
        private const int StateGranted = 1;

        public MicrophoneAccess? Access
        {
            get
            {
                switch (A305Voice_GetAccessState())
                {
                    case StateWaiting:
                        return null;
                    case StateGranted:
                        return MicrophoneAccess.Granted;
                    default:
                        return BrowserMicrophoneErrors.ToAccess(A305Voice_GetErrorName());
                }
            }
        }

        public float CurrentLevel => A305Voice_GetLevel();

        public void RequestAccess()
        {
            A305Voice_RequestAccess();
        }

        public void StartRecording()
        {
            A305Voice_StartRecording();
        }

        public CapturedAudio StopRecording()
        {
            int count = A305Voice_StopRecording();
            var samples = new float[count];
            if (count > 0)
            {
                A305Voice_CopySamples(samples, count);
            }

            return new CapturedAudio(samples, A305Voice_GetSampleRate());
        }

        [DllImport("__Internal")]
        private static extern void A305Voice_RequestAccess();

        [DllImport("__Internal")]
        private static extern int A305Voice_GetAccessState();

        [DllImport("__Internal")]
        private static extern string A305Voice_GetErrorName();

        [DllImport("__Internal")]
        private static extern void A305Voice_StartRecording();

        [DllImport("__Internal")]
        private static extern float A305Voice_GetLevel();

        [DllImport("__Internal")]
        private static extern int A305Voice_StopRecording();

        [DllImport("__Internal")]
        private static extern int A305Voice_GetSampleRate();

        [DllImport("__Internal")]
        private static extern void A305Voice_CopySamples(float[] buffer, int length);
    }
}
#endif
