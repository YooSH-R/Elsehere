using System.Collections.Generic;

namespace A305.ObjectHover
{
    /// <summary>WORLD-02 응답 중 사물 카드에 쓰는 단어 카드 목록.</summary>
    public sealed class SceneWords
    {
        public List<WordCardData> Words { get; set; } = new List<WordCardData>();
    }

    /// <summary>장소의 공용 단어 카드 하나와 사용자별 저장·숨김 상태.</summary>
    public sealed class WordCardData
    {
        public string WordKey { get; set; }

        public WordCardVocab Vocab { get; set; }

        public bool HasPronunciation { get; set; }

        public bool Saved { get; set; }

        public bool HoverHidden { get; set; }
    }

    public sealed class WordCardVocab
    {
        public long VocabId { get; set; }

        public string Text { get; set; }

        public string MeaningKo { get; set; }

        public string Example { get; set; }

        public string ExampleKo { get; set; }
    }
}
