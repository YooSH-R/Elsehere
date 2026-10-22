using System;
using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>
    /// 사용자가 모은 표현. 서버 연동 전에는 Play 동안만 남는다.
    /// </summary>
    public sealed class VocabularyBook
    {
        private readonly List<VocabularyEntry> entries = new List<VocabularyEntry>();

        public IReadOnlyList<VocabularyEntry> Entries => entries;

        /// <summary>
        /// 표현을 저장한다. 같은 표현(대소문자·앞뒤 공백 무시)을 다시 저장하면 새로 만들지 않고 저장 횟수만 늘린다.
        /// </summary>
        /// <returns>저장한 항목. 빈 표현이면 저장하지 않고 null.</returns>
        public VocabularyEntry Save(string english, string korean, string source)
        {
            string expression = english?.Trim();
            if (string.IsNullOrEmpty(expression))
            {
                return null;
            }

            VocabularyEntry entry = Find(expression);
            if (entry == null)
            {
                entry = new VocabularyEntry(expression, korean, source);
                entries.Add(entry);
            }

            entry.SaveCount++;
            return entry;
        }

        public void Delete(VocabularyEntry entry)
        {
            entries.Remove(entry);
        }

        public void ToggleLearned(VocabularyEntry entry)
        {
            entry.IsLearned = !entry.IsLearned;
        }

        private VocabularyEntry Find(string expression)
        {
            foreach (VocabularyEntry entry in entries)
            {
                if (string.Equals(entry.English, expression, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
