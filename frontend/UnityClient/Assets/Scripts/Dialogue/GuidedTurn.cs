namespace A305.Dialogue
{
    /// <summary>정해진 흐름의 한 턴. 사용자가 읽어 볼 대사와, 사용자가 말한 뒤 NPC가 이어서 하는 대사.</summary>
    public readonly struct GuidedTurn
    {
        public GuidedTurn(string suggestedEnglish, string suggestedKorean, DialogueLine reply)
        {
            SuggestedEnglish = suggestedEnglish;
            SuggestedKorean = suggestedKorean;
            Reply = reply;
        }

        public string SuggestedEnglish { get; }
        public string SuggestedKorean { get; }
        public DialogueLine Reply { get; }
    }
}
