namespace A305.Dialogue
{
    /// <summary>대화창이 크게 보여 줄 기다림. 뜻·힌트·음성 인식처럼 짧은 기다림은 상태 줄로만 알린다.</summary>
    public enum DialogueWait
    {
        None,

        // 대화를 시작하며 NPC 첫 대사를 기다린다.
        Opening,

        // 보낸 문장에 대한 NPC 대답을 기다린다.
        NpcReply,

        // 대화를 끝내고 피드백을 기다린다.
        Feedback,
    }
}
