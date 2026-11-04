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

        /// <summary>녹음 중이고 마이크가 열려 소리를 받고 있는지. 브라우저는 마이크를 여는 데 시간이 걸린다.</summary>
        bool IsCapturing { get; }

        /// <summary>권한을 요청한다. 브라우저는 버튼 클릭 같은 사용자 동작 뒤에 불러야 사용자에게 묻는다.</summary>
        void RequestAccess();

        /// <summary>
        /// 대화 동안 마이크를 열어 둔다. 열어 둔 동안은 녹음이 끝나도 닫지 않아 다음 녹음이 바로 소리를 받는다.
        /// 권한 창을 갑자기 띄우지 않도록 이미 허용된 경우에만 미리 연다.
        /// </summary>
        void KeepOpen();

        /// <summary>열어 둔 마이크를 닫는다. 녹음 중이면 녹음이 끝날 때 닫는다.</summary>
        void Release();

        void StartRecording();

        /// <summary>녹음을 멈추고 지금까지 받은 소리를 돌려준다. 열어 두지 않은 마이크는 닫는다.</summary>
        CapturedAudio StopRecording();
    }
}
