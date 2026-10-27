namespace A305.Voice
{
    /// <summary>마이크를 쓸 수 있는지. 쓸 수 없으면 대화에서 텍스트로 입력한다(기능 명세 F-08).</summary>
    public enum MicrophoneAccess
    {
        Granted,

        /// <summary>사용자나 브라우저 설정이 권한을 거부했다. 대화 화면에서 다시 요청할 수 있다.</summary>
        Denied,

        /// <summary>마이크가 없거나 다른 프로그램이 쓰고 있어 열 수 없다.</summary>
        NoDevice,

        /// <summary>브라우저가 마이크 API를 지원하지 않거나 HTTPS가 아닌 주소다.</summary>
        Unsupported,
    }
}
