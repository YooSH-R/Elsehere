namespace A305.Voice
{
    /// <summary>녹음이 끝난 이유.</summary>
    public enum RecordingEndReason
    {
        /// <summary>[누르고 말하기] 버튼을 뗐다.</summary>
        UserStopped,

        /// <summary>최대 녹음 시간에 닿았다.</summary>
        MaxDuration,

        /// <summary>조용한 상태가 정한 시간만큼 이어졌다.</summary>
        Silence,

        /// <summary>녹음 중 다른 탭·창으로 가서 멈췄다.</summary>
        Interrupted,
    }
}
