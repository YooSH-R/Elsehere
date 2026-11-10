namespace A305.Learning
{
    /// <summary>단어장 맨 위 탭. 어디서 저장했는지로 나눈다.</summary>
    public enum VocabularyTab
    {
        /// <summary>NPC 대사·대화 피드백에서 저장한 표현. 장소 묶음으로 더 나눈다.</summary>
        Conversation,

        /// <summary>사물 카드에서 저장한 단어. 여러 장소에 같은 사물이 있어 장소로 나누지 않는다.</summary>
        Objects,
    }
}
