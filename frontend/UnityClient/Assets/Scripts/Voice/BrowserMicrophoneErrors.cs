namespace A305.Voice
{
    /// <summary>
    /// 브라우저 마이크 연결(getUserMedia)이 실패했을 때 받은 오류 이름을 마이크 상태로 바꾼다. 화면 안내는 권한 거부와 장치 문제를 나눠 보여 준다.
    /// </summary>
    public static class BrowserMicrophoneErrors
    {
        /// <summary>브라우저에 마이크 API가 없거나 HTTPS가 아닌 주소일 때 jslib이 보내는 이름.</summary>
        public const string UnsupportedName = "Unsupported";

        public static MicrophoneAccess ToAccess(string errorName)
        {
            switch (errorName)
            {
                case UnsupportedName:
                    return MicrophoneAccess.Unsupported;
                // PermissionDeniedError는 예전 Chrome이 쓰던 이름이다.
                case "NotAllowedError":
                case "SecurityError":
                case "PermissionDeniedError":
                    return MicrophoneAccess.Denied;
                // 장치가 없거나 다른 프로그램이 쓰는 등 마이크를 열지 못한 경우다. 모르는 오류도 쓸 수 없는 것으로 본다.
                default:
                    return MicrophoneAccess.NoDevice;
            }
        }
    }
}
