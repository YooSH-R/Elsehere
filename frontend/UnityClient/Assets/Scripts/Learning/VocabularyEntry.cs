using System;

namespace A305.Learning
{
    /// <summary>단어장의 한 단어. 뜻·예시 문장·메모와 '외웠어요' 표시를 가진다.</summary>
    public sealed class VocabularyEntry
    {
        private const string NpcLineSource = "NPC_LINE";
        private const string FeedbackSource = "FEEDBACK";

        /// <summary>서버 단어장 항목(VOCAB-01)으로 만든다.</summary>
        public VocabularyEntry(VocabItem item)
        {
            UserVocabId = item.UserVocabId;
            English = item.Vocab?.Text;
            Korean = item.Vocab?.MeaningKo;
            ContextText = item.Context?.Text;
            ContextMeaning = item.Context?.MeaningKo;
            string sourceType = item.Source?.SourceType;
            IsFromConversation = sourceType == NpcLineSource || sourceType == FeedbackSource;
            PlaceName = item.Source?.PlaceName;
            PlaceGroup = Enum.TryParse(item.Source?.PlaceGroup, true, out PlaceGroup group)
                ? group
                : (PlaceGroup?)null;
            // NPC 대사에서 저장한 단어는 예시 문장이 그 NPC의 대사다. 이름 끝소리에 맞춰 이·가를 붙인다(Ben이, Mia가).
            // 일하는 곳을 알면 앞에 붙인다(카페 · Mia가 한 말).
            SourceLabel =
                sourceType == NpcLineSource && !string.IsNullOrEmpty(item.Source.NpcName)
                    ? (PlaceName == null ? "" : $"{PlaceName} · ")
                        + $"{FeedbackText.NameAsSubject(item.Source.NpcName)} 한 말"
                    : null;
            Memo = item.Memo;
            IsLearned = item.Memorized;
        }

        /// <summary>서버 단어장 항목 ID. 메모·외웠어요·삭제 요청에 쓴다.</summary>
        public long UserVocabId { get; }

        /// <summary>단어가 쓰인 예시 문장(사물 카드 예문, NPC 대사, 피드백 예문). 없으면 null.</summary>
        public string ContextText { get; }

        /// <summary>예시 문장의 뜻. 없으면 null.</summary>
        public string ContextMeaning { get; }

        /// <summary>예시 문장을 누가 했는지(예: "Mia가 한 말"). 사물 카드처럼 대화 출처가 아니면 null.</summary>
        public string SourceLabel { get; }

        /// <summary>NPC 대사·대화 피드백에서 저장한 대화 표현인지. 아니면 사물 카드에서 저장한 단어다.</summary>
        public bool IsFromConversation { get; }

        /// <summary>출처 대화 NPC가 일하는 곳. 사물에서 저장했거나 근무 시간 밖 대화면 null.</summary>
        public string PlaceName { get; }

        /// <summary>대화 표현의 장소 묶음. 사물에서 저장한 단어는 null.</summary>
        public PlaceGroup? PlaceGroup { get; }

        /// <summary>사용자가 남긴 메모. 없으면 null.</summary>
        public string Memo { get; internal set; }

        public string English { get; }
        public string Korean { get; }

        /// <summary>사용자가 '외웠어요'로 직접 표시한 상태. 자동 채점 결과가 아니다.</summary>
        public bool IsLearned { get; internal set; }
    }
}
