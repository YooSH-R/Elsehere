using System;
using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>VOCAB-01 단어장 한 페이지. 최근 저장순이다.</summary>
    public sealed class VocabPage
    {
        public VocabCounts Counts { get; set; }

        public List<VocabItem> Items { get; set; } = new List<VocabItem>();

        /// <summary>다음 페이지를 요청할 때 그대로 보내는 값. 마지막 페이지면 null.</summary>
        public string NextCursor { get; set; }
    }

    public sealed class VocabCounts
    {
        /// <summary>요청 조건(외운 단어 포함 여부)을 적용한 개수</summary>
        public long All { get; set; }
    }

    /// <summary>단어장 항목 하나(VOCAB-01·02·03 응답).</summary>
    public sealed class VocabItem
    {
        public long UserVocabId { get; set; }

        public VocabWord Vocab { get; set; }

        /// <summary>저장한 문장과 문장 뜻. 저장한 문장이 없으면 null.</summary>
        public VocabContext Context { get; set; }

        public string Memo { get; set; }

        public bool Memorized { get; set; }

        public DateTimeOffset SavedAt { get; set; }

        public VocabSource Source { get; set; }

        /// <summary>단어 저장(VOCAB-02) 응답에만 있다. 새로 저장했으면 true, 이미 저장한 단어면 false.</summary>
        public bool Created { get; set; }
    }

    public sealed class VocabWord
    {
        public long VocabId { get; set; }

        public string Text { get; set; }

        public string MeaningKo { get; set; }
    }

    public sealed class VocabContext
    {
        public string Text { get; set; }

        public string MeaningKo { get; set; }
    }

    public sealed class VocabSource
    {
        /// <summary>OBJECT, NPC_LINE, FEEDBACK</summary>
        public string SourceType { get; set; }

        /// <summary>대화에서 저장한 단어의 NPC 이름. 사물에서 저장했으면 null.</summary>
        public string NpcName { get; set; }
    }
}
