using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using A305.ObjectHover;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    public static class ObjectWordExport
    {
        public static string SerializeWords(IEnumerable<ObjectWordDefinition> definitions)
        {
            var document = new Document { words = Validate(definitions) };
            return JsonUtility.ToJson(document, true) + "\n";
        }

        public static string BuildSqlSection(IEnumerable<ObjectWordDefinition> definitions)
        {
            var rows = Validate(definitions)
                .Select(word =>
                    "  ("
                    + string.Join(
                        ", ",
                        new[]
                        {
                            Literal(word.LanguageCode),
                            "'WORD'",
                            Literal(word.Text),
                            Literal(word.MeaningKo),
                            Literal(word.Example),
                            Literal(word.ExampleKo),
                            "'MASTER'",
                            string.IsNullOrEmpty(word.PronunciationAudioKey)
                                ? "NULL"
                                : Literal(word.PronunciationAudioKey),
                        }
                    )
                    + ")"
                );
            return "-- BEGIN GENERATED OBJECT WORDS: Unity ObjectWordAuthoring.Export\n"
                + "INSERT INTO vocabularies (language_code, type, text, meaning_ko, example, example_ko, source, pronunciation_audio_key)\nVALUES\n"
                + string.Join(",\n", rows)
                + "\n"
                + "ON CONFLICT (language_code, text) DO UPDATE\n"
                + "SET type = EXCLUDED.type,\n    meaning_ko = EXCLUDED.meaning_ko,\n"
                + "    example = EXCLUDED.example,\n    example_ko = EXCLUDED.example_ko,\n"
                + "    source = EXCLUDED.source,\n    pronunciation_audio_key = EXCLUDED.pronunciation_audio_key,\n"
                + "    updated_at = now();\n-- END GENERATED OBJECT WORDS";
        }

        private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

        private static ObjectWordDetails[] Validate(IEnumerable<ObjectWordDefinition> definitions)
        {
            var words = new List<ObjectWordDetails>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var expressions = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectWordDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("공용 단어 참조가 비어 있습니다.");
                ObjectWordDetails word = definition.Details;
                if (
                    new[]
                    {
                        word.Key,
                        word.LanguageCode,
                        word.Text,
                        word.MeaningKo,
                        word.Example,
                        word.ExampleKo,
                    }.Any(string.IsNullOrWhiteSpace)
                )
                    throw new ArgumentException(
                        definition.name + ": 단어·뜻·영문 예문·예문 해석을 모두 입력하세요."
                    );
                if (
                    !Regex.IsMatch(word.Key, @"^[a-z][a-z0-9_]{0,49}$")
                    || word.LanguageCode.Length > 10
                    || word.PronunciationAudioKey.Length > 100
                )
                    throw new ArgumentException(
                        definition.name + ": 키 또는 언어·발음 파일 키 형식이 잘못됐습니다."
                    );
                if (!keys.Add(word.Key))
                    throw new ArgumentException("중복 공용 단어 키: " + word.Key);
                if (!expressions.Add(word.LanguageCode + ":" + word.Text))
                    throw new ArgumentException("동일 언어의 중복 단어: " + word.Text);
                words.Add(word);
            }
            if (words.Count == 0)
                throw new ArgumentException("내보낼 공용 단어가 없습니다.");
            return words.OrderBy(word => word.Key, StringComparer.Ordinal).ToArray();
        }

        [Serializable]
        private sealed class Document
        {
            public ObjectWordDetails[] words;
        }
    }
}
