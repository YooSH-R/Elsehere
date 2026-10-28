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
    }
}
