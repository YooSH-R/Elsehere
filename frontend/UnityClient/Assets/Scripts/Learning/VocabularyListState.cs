namespace A305.Learning
{
    /// <summary>단어장 화면이 보여 줄 상태.</summary>
    public enum VocabularyListState
    {
        Loading,
        Failed,
        Empty,

        /// <summary>저장한 단어를 모두 외워 목록에서 정리했다. [외운 표현도 보기]로 다시 볼 수 있다.</summary>
        AllLearned,

        /// <summary>고른 탭에는 저장한 단어가 없고 다른 탭에만 있다.</summary>
        TabEmpty,
        Entries,
    }
}
