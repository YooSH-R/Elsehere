namespace A305.Voice
{
    /// <summary>
    /// 마이크 연결. Web 빌드는 브라우저 API(<see cref="BrowserMicrophone"/>), Editor·데스크톱은 Unity Microphone(<see cref="UnityMicrophone"/>)을 쓴다.
    /// </summary>
    public interface IMicrophone
    {
        /// <summary>
        /// 마이크 권한 요청 결과. 아직 요청하지 않았거나 브라우저가 답하기 전이면 null이다. 녹음을 시작하면 마이크를 여는 결과로 다시 정해진다.
        /// </summary>
        MicrophoneAccess? Access { get; }

        /// <summary>가장 최근 소리 조각의 음량(RMS, 0~1). 녹음 중이 아니면 0이다.</summary>
        float CurrentLevel { get; }

        /// <summary>권한을 요청한다. 브라우저는 버튼 클릭 같은 사용자 동작 뒤에 불러야 사용자에게 묻는다.</summary>
        void RequestAccess();

        void StartRecording();

        /// <summary>녹음을 멈추고 마이크를 닫은 뒤 지금까지 받은 소리를 돌려준다.</summary>
        CapturedAudio StopRecording();
    }
}
