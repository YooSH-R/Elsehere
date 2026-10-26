using System;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>Unity 공용 단어와 서버 전달 데이터. 사용자별 저장·숨김 상태는 포함하지 않는다.</summary>
    [Serializable]
    public sealed class ObjectWordDetails
    {
        [SerializeField, HideInInspector]
        private string key;

        [SerializeField]
        private string languageCode = "en-US";

        [SerializeField]
        private string text;

        [SerializeField]
        private string meaningKo;

        [SerializeField]
        private string example;

        [SerializeField]
        private string exampleKo;

        [SerializeField]
        private string pronunciationAudioKey;

        public string Key => key;
        public string LanguageCode => languageCode;
        public string Text => text;
        public string MeaningKo => meaningKo;
        public string Example => example ?? string.Empty;
        public string ExampleKo => exampleKo ?? string.Empty;
        public string PronunciationAudioKey => pronunciationAudioKey ?? string.Empty;
    }
}
