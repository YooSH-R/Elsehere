namespace A305.Learning
{
    /// <summary>단어장의 한 단어. 뜻·저장한 문장·메모와 '외웠어요' 표시를 가진다.</summary>
    public sealed class VocabularyEntry
    {
        private const string NpcLineSource = "NPC_LINE";

        /// <summary>서버 단어장 항목(VOCAB-01)으로 만든다.</summary>
        public VocabularyEntry(VocabItem item)
        {
            UserVocabId = item.UserVocabId;
            English = item.Vocab?.Text;
            Korean = item.Vocab?.MeaningKo;
            ContextText = item.Context?.Text;
            // NPC 대사에서 저장한 단어는 저장한 문장이 그 NPC의 대사다. 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '의'를 쓴다.
            SourceLabel =
                item.Source?.SourceType == NpcLineSource
                && !string.IsNullOrEmpty(item.Source.NpcName)
                    ? $"{item.Source.NpcName}의 말"
                    : null;
            Memo = item.Memo;
            IsLearned = item.Memorized;
        }

        /// <summary>서버 단어장 항목 ID. 메모·외웠어요·삭제 요청에 쓴다.</summary>
        public long UserVocabId { get; }

        /// <summary>단어를 저장한 문장. 없으면 null.</summary>
        public string ContextText { get; }

        /// <summary>저장한 문장이 어디서 나왔는지(예: "Mia의 말"). 사물 카드처럼 대화 출처가 아니면 null.</summary>
        public string SourceLabel { get; }

        /// <summary>사용자가 남긴 메모. 없으면 null.</summary>
        public string Memo { get; internal set; }

        public string English { get; }
        public string Korean { get; }

        /// <summary>사용자가 '외웠어요'로 직접 표시한 상태. 자동 채점 결과가 아니다.</summary>
        public bool IsLearned { get; internal set; }
    }
}
