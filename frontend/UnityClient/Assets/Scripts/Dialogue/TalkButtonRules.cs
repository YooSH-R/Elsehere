namespace A305.Dialogue
{
    /// <summary>[말하기]를 눌렀을 때 할 일.</summary>
    public enum TalkButtonAction
    {
        None,
        StartRecording,
        StopRecording,
        AskVoiceConsent,
    }

    /// <summary>[말하기] 버튼을 보일지, 쓸 수 있는 모양으로 그릴지, 누르면 무엇을 할지.</summary>
    public readonly struct TalkButtonState
    {
        public TalkButtonState(bool isVisible, bool looksAvailable, TalkButtonAction action)
        {
            IsVisible = isVisible;
            LooksAvailable = looksAvailable;
            Action = action;
        }

        public bool IsVisible { get; }

        /// <summary>false면 회색으로 그린다. 음성 처리에 동의하지 않았어도 눌러서 동의 창을 열 수 있다.</summary>
        public bool LooksAvailable { get; }

        public TalkButtonAction Action { get; }
    }

    /// <summary>
    /// 음성 대화의 [말하기] 상태를 정한다. 음성 처리에 동의하지 않았으면 버튼을 숨기지 않고 회색으로 보이며,
    /// 누르면 동의 창을 열어 그 자리에서 동의하고 말할 수 있게 한다.
    /// </summary>
    public static class TalkButtonRules
    {
        /// <param name="usesVoice">음성으로 말할 수 있는 대화인지. 정해진 대본 대화는 아직 음성을 받지 않는다.</param>
        /// <param name="canTalk">음성 처리에 동의했는지</param>
        /// <param name="canSend">지금 문장을 보낼 수 있는지. NPC 대답을 기다리거나 대화가 끝났으면 false다.</param>
        /// <param name="isRecording">녹음 중인지</param>
        public static TalkButtonState Decide(
            bool usesVoice,
            bool canTalk,
            bool canSend,
            bool isRecording
        )
        {
            if (!usesVoice)
            {
                return new TalkButtonState(false, false, TalkButtonAction.None);
            }

            if (!canTalk)
            {
                return new TalkButtonState(true, false, TalkButtonAction.AskVoiceConsent);
            }

            if (isRecording)
            {
                return new TalkButtonState(true, true, TalkButtonAction.StopRecording);
            }

            return canSend
                ? new TalkButtonState(true, true, TalkButtonAction.StartRecording)
                : new TalkButtonState(true, false, TalkButtonAction.None);
        }
    }
}
