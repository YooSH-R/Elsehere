namespace A305.Dialogue
{
    /// <summary>
    /// 발화 전송(CONV-03)을 스트리밍으로 받을 때 NPC 대사 글자와 문장 조각 음성을 받는 쪽. 저장된 턴은 전송 결과로 따로 받는다.
    /// </summary>
    public interface ITurnStream
    {
        /// <summary>대사 글자 조각. 이어 붙이면 대사가 된다.</summary>
        void OnDelta(string text);

        /// <summary>문장 조각 음성. seq 순서대로 온다.</summary>
        /// <param name="wav">립싱크 음소 정보가 든 WAV. 합성이 늦거나 실패한 조각은 null이며 자막만 보인다.</param>
        void OnSpeechChunk(int seq, string text, byte[] wav);
    }
}
