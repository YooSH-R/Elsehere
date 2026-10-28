namespace A305.Npc
{
    /// <summary>대화 제안 화면에 보일 문구.</summary>
    public static class PasserbyOfferText
    {
        /// <summary>
        /// 누가 말을 걸었는지 알리는 머리말. 행인과 직원이 같은 제안 화면을 쓰므로 "행인이"로 고정하지 않는다.
        /// 영어 이름만으로는 누구인지 알기 어려워 역할(편의점 직원 등)이 있으면 역할로, 없으면 이름으로 알린다.
        /// </summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        /// <param name="roleLabel">대화 대본의 NPC 역할. 모르면 null.</param>
        public static string Hint(string npcName, string roleLabel = null)
        {
            string speaker = string.IsNullOrEmpty(roleLabel) ? npcName : roleLabel;
            return string.IsNullOrEmpty(speaker)
                ? "누군가 말을 걸었어요"
                : $"{speaker}{SubjectParticle(speaker)} 말을 걸었어요";
        }

        /// <summary>한글로 끝나면 받침에 맞춰 이·가를 고르고, 영어 이름처럼 알 수 없으면 "이(가)"를 쓴다.</summary>
        private static string SubjectParticle(string word)
        {
            char last = word[word.Length - 1];
            if (last < '가' || last > '힣')
            {
                return "이(가)";
            }

            bool hasFinalConsonant = (last - '가') % 28 != 0;
            return hasFinalConsonant ? "이" : "가";
        }

        /// <summary>플레이어가 먼저 말을 거는 버튼 문구. 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '에게'를 쓴다.</summary>
        /// <param name="npcName">대화 대본의 NPC 이름. 모르면 null.</param>
        public static string TalkLabel(string npcName)
        {
            return string.IsNullOrEmpty(npcName) ? "말 걸기" : $"{npcName}에게 말 걸기";
        }
    }
}
