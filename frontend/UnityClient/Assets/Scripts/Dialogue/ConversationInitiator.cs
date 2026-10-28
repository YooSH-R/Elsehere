namespace A305.Dialogue
{
    /// <summary>
    /// 누가 먼저 대화를 걸었는지(ERD conversations.initiated_by). NPC가 다가와 제안한 대화는 Npc,
    /// 플레이어가 [말 걸기]로 시작한 대화는 User다.
    /// </summary>
    public enum ConversationInitiator
    {
        Npc,
        User,
    }
}
