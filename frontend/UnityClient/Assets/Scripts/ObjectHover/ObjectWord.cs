namespace A305.ObjectHover
{
    /// <summary>사물에 연결된 영어 표현. ERD vocabularies의 text·meaning_ko·example에 해당한다.</summary>
    public readonly struct ObjectWord
    {
        public ObjectWord(string text, string meaningKo, string example)
        {
            Text = text;
            MeaningKo = meaningKo;
            Example = example;
        }

        public string Text { get; }

        public string MeaningKo { get; }

        /// <summary>없으면 빈 문자열.</summary>
        public string Example { get; }
    }
}
