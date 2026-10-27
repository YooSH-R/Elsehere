using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>프리팹이 공유하는 단어 키로 공용 뜻·예문·발음 정보를 찾는다.</summary>
    public sealed class ObjectWordCatalog
    {
        private readonly Dictionary<string, ObjectWordDetails> words = new Dictionary<
            string,
            ObjectWordDetails
        >(StringComparer.Ordinal);

        public IReadOnlyCollection<ObjectWordDetails> Words => words.Values;

        public static ObjectWordCatalog Parse(string json)
        {
            Document document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.words == null)
                throw new ArgumentException(
                    "공용 단어 목록의 words 배열이 필요합니다.",
                    nameof(json)
                );
            var catalog = new ObjectWordCatalog();
            foreach (ObjectWordDetails word in document.words)
            {
                if (
                    word == null
                    || string.IsNullOrWhiteSpace(word.Key)
                    || string.IsNullOrWhiteSpace(word.LanguageCode)
                    || string.IsNullOrWhiteSpace(word.Text)
                    || string.IsNullOrWhiteSpace(word.MeaningKo)
                )
                    throw new ArgumentException(
                        "공용 단어의 key, languageCode, text, meaningKo가 필요합니다.",
                        nameof(json)
                    );
                catalog.words.Add(word.Key, word);
            }
            return catalog;
        }

        public bool TryGetDetails(string wordKey, out ObjectWordDetails details)
        {
            if (string.IsNullOrEmpty(wordKey))
            {
                details = null;
                return false;
            }
            return words.TryGetValue(wordKey, out details);
        }

        [Serializable]
        private sealed class Document
        {
            public ObjectWordDetails[] words;
        }
    }
}
