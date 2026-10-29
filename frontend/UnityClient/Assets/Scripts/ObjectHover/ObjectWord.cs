namespace A305.ObjectHover
{
    /// <summary>사물에 연결된 영어 표현. ERD vocabularies의 text·meaning_ko·example·example_ko에 해당한다.</summary>
    public readonly struct ObjectWord
    {
        public ObjectWord(string text, string meaningKo, string example, string exampleKo = "")
        {
            Text = text;
            MeaningKo = meaningKo;
            Example = example;
            ExampleKo = exampleKo ?? string.Empty;
        }

        /// <summary>
        /// 호버 툴팁에 보여 줄 표현을 만든다. 예문과 예문 뜻은 사용자가 단어장에 저장한 단어만 보인다(ERD vocabularies.example).
        /// </summary>
        public static ObjectWord ForHover(
            string text,
            string meaningKo,
            string example,
            bool isSavedToVocabulary,
            string exampleKo = null
        )
        {
            string visibleExample = isSavedToVocabulary && example != null ? example : string.Empty;
            string visibleExampleKo =
                isSavedToVocabulary && exampleKo != null ? exampleKo : string.Empty;
            return new ObjectWord(text, meaningKo, visibleExample, visibleExampleKo);
        }

        public string Text { get; }

        public string MeaningKo { get; }

        /// <summary>없으면 빈 문자열.</summary>
        public string Example { get; }

        /// <summary>예문 뜻. 없으면 빈 문자열.</summary>
        public string ExampleKo { get; }
    }
}
