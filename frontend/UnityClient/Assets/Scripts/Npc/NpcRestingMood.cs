using A305.Characters;

namespace A305.Npc
{
    /// <summary>NPC 코드로 정하는 평소 인상. 호텔 외형은 여러 직원이 공유하므로 프리팹이 아니라 코드로 가른다.</summary>
    public static class NpcRestingMood
    {
        /// <summary>
        /// 평소에는 Jake만 무심하다. 시연에서는 Jake가 기본 표정으로 맞이하고, 사이가 나쁜 Owen이 무심해 둘이 대비된다.
        /// 따로 정한 인상이 없으면 null이다.
        /// </summary>
        public static CharacterEmotion? Of(string npcCode, bool showcase) =>
            npcCode == (showcase ? "HOTEL_RECEPTIONIST" : "HOTEL_STAFF")
                ? CharacterEmotion.Indifferent
                : null;
    }
}
