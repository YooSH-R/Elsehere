namespace A305.Npc
{
    /// <summary>대화 제안 화면에 보일 문구.</summary>
    public static class PasserbyOfferText
    {
        /// <summary>
        /// 누가 말을 걸었는지 알리는 머리말. 행인과 카페 직원이 같은 제안 화면을 쓰므로 NPC 종류 대신 이름을 쓴다.
        /// </summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        public static string Hint(string npcName)
        {
            return string.IsNullOrEmpty(npcName)
                ? "누군가 말을 걸었어요"
                : $"{npcName}이(가) 말을 걸었어요";
        }

        /// <summary>플레이어가 먼저 말을 거는 버튼 문구. 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '에게'를 쓴다.</summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        public static string TalkLabel(string npcName)
        {
            return string.IsNullOrEmpty(npcName) ? "말 걸기" : $"{npcName}에게 말 걸기";
        }
    }
}
