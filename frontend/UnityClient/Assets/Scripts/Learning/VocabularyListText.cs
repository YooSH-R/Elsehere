using System;
using System.Collections.Generic;
using System.Text;

namespace A305.Learning
{
    /// <summary>단어장 화면의 목록 글자. 고른 단어 주변의 몇 개만 보여 준다.</summary>
    public static class VocabularyListText
    {
        public const string EmptyMessage =
            "아직 모은 단어가 없어요. 사물 카드에서 [단어장에 저장]을 눌러 보세요.";

        /// <param name="selectedIndex">고른 단어. 목록 범위 안의 값이어야 한다.</param>
        /// <param name="visibleCount">한 번에 보여 줄 단어 수</param>
        public static string Format(
            IReadOnlyList<VocabularyEntry> entries,
            int selectedIndex,
            int visibleCount
        )
        {
            if (entries.Count == 0)
            {
                return EmptyMessage;
            }

            int firstIndex = Math.Clamp(
                selectedIndex - visibleCount / 2,
                0,
                Math.Max(0, entries.Count - visibleCount)
            );
            int lastIndex = Math.Min(entries.Count, firstIndex + visibleCount);
            var text = new StringBuilder();
            for (int i = firstIndex; i < lastIndex; i++)
            {
                VocabularyEntry entry = entries[i];
                string marker = i == selectedIndex ? "→ " : "    ";
                string learned = entry.IsLearned ? " · 외웠어요" : "";
                text.AppendLine($"{marker}<b>{entry.English}</b>  {entry.Korean}{learned}");
                if (!string.IsNullOrEmpty(entry.Memo))
                {
                    text.AppendLine($"      메모: {entry.Memo}");
                }
            }

            text.Append($"\n{selectedIndex + 1} / {entries.Count}");
            return text.ToString();
        }
    }
}
