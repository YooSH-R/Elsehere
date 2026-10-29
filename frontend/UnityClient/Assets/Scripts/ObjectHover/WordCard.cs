namespace A305.ObjectHover
{
    /// <summary>장소 단어 카드 하나의 서버 상태. 카드 버튼으로 바꾼 상태는 서버가 받아들인 뒤에만 반영한다.</summary>
    public sealed class WordCard
    {
        private readonly WordCardVocab vocab;

        internal WordCard(WordCardData data)
        {
            WordKey = data.WordKey;
            vocab = data.Vocab ?? new WordCardVocab();
            HasPronunciation = data.HasPronunciation;
            Saved = data.Saved;
            HoverHidden = data.HoverHidden;
        }

        public string WordKey { get; }

        /// <summary>발음 음성(WORLD-04)을 받을 때 쓰는 어휘 ID</summary>
        public long VocabId => vocab.VocabId;

        public bool HasPronunciation { get; }

        public bool Saved { get; internal set; }

        /// <summary>호버로 카드를 띄우지 않는 단어. 사물을 클릭하면 카드를 보인다.</summary>
        public bool HoverHidden { get; internal set; }

        /// <summary>저장·숨김 요청을 기다리는 중. 이때는 카드 버튼을 막는다.</summary>
        public bool IsBusy { get; internal set; }

        /// <summary>마지막 요청 뒤 카드에 보일 안내. 실패한 이유나 숨긴 카드를 다시 보는 방법이며, 없으면 null.</summary>
        public string Message { get; internal set; }

        /// <summary>카드에 보일 표현. 저장한 단어만 예문과 예문 뜻을 보인다(기능 명세 F-12).</summary>
        public ObjectWord Word =>
            ObjectWord.ForHover(vocab.Text, vocab.MeaningKo, vocab.Example, Saved, vocab.ExampleKo);

        // 저장에 실패하면 다시 시도할 때 같은 요청 ID를 보낸다. 저장하면 비운다.
        internal string SaveRequestId { get; set; }
    }
}
