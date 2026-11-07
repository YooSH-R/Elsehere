namespace A305.ObjectHover
{
    /// <summary>
    /// 고정한 카드의 버튼 문구와 누를 수 있는지. 서버 단어 카드가 없으면 단어 버튼 없이 [닫기]만 둔다.
    /// </summary>
    public readonly struct WordCardButtons
    {
        private WordCardButtons(WordCard card, bool browserPronunciationAvailable)
        {
            HasWordActions = true;
            SaveLabel = card.Saved ? "저장했어요" : "단어장에 저장";
            CanSave = !card.Saved && !card.IsBusy;
            HideLabel = card.HoverHidden ? "다시 띄우기" : "숨기기";
            CanToggleHide = !card.IsBusy;
            HasPronunciation = card.HasPronunciation || browserPronunciationAvailable;
            Message = card.Message;
        }

        public static WordCardButtons None => default;

        public static WordCardButtons For(WordCard card, bool browserPronunciationAvailable = false)
        {
            return card == null ? None : new WordCardButtons(card, browserPronunciationAvailable);
        }

        /// <summary>[단어장에 저장]·[발음 듣기]·[숨기기]를 보이는지</summary>
        public bool HasWordActions { get; }

        public string SaveLabel { get; }

        public bool CanSave { get; }

        public string HideLabel { get; }

        public bool CanToggleHide { get; }

        public bool HasPronunciation { get; }

        /// <summary>버튼 줄 위에 보일 안내. 없으면 null.</summary>
        public string Message { get; }
    }
}
