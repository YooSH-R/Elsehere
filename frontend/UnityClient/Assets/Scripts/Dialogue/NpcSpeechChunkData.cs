namespace A305.Dialogue
{
    /// <summary>스트리밍으로 받은 NPC 대답의 문장 조각 음성 하나.</summary>
    public sealed class NpcSpeechChunkData
    {
        public NpcSpeechChunkData(int reply, int seq, byte[] wav)
        {
            Reply = reply;
            Seq = seq;
            Wav = wav;
        }

        /// <summary>어느 대답의 조각인지. 새 대답의 조각이 오면 지난 대답의 남은 조각은 재생하지 않는다.</summary>
        public int Reply { get; }

        /// <summary>대답 안에서의 순서. 0부터.</summary>
        public int Seq { get; }

        /// <summary>립싱크 음소 정보가 든 WAV</summary>
        public byte[] Wav { get; }
    }
}
