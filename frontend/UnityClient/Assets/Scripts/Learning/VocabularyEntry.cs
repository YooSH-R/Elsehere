namespace A305.Learning
{
    /// <summary>단어장의 한 표현. 저장한 횟수와 '외웠어요' 표시를 함께 가진다.</summary>
    public sealed class VocabularyEntry
    {
        public VocabularyEntry(string english, string korean, string source)
        {
            English = english;
            Korean = korean;
            Source = source;
        }

        public string English { get; }
        public string Korean { get; }

        /// <summary>처음 저장한 곳. 예: 대화, 피드백</summary>
        public string Source { get; }

        public int SaveCount { get; internal set; }

        /// <summary>사용자가 '외웠어요'로 직접 표시한 상태. 자동 채점 결과가 아니다.</summary>
        public bool IsLearned { get; internal set; }
    }
}
