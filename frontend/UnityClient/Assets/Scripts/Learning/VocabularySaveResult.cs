namespace A305.Learning
{
    /// <summary>단어장에 단어를 저장한 결과</summary>
    public enum VocabularySaveResult
    {
        /// <summary>새로 저장했다.</summary>
        Saved,

        /// <summary>이미 단어장에 있던 단어다. 서버는 처음 저장한 항목을 그대로 둔다.</summary>
        AlreadySaved,

        /// <summary>저장하지 못했다. 이유는 <see cref="VocabularyBook.LastFailureMessage"/>에 있다.</summary>
        Failed,
    }
}
