using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화창 대사 칸의 글자. 이번 대화의 대사를 말한 사람과 함께 처음부터 보이고,
    /// 뜻을 펼치면 가장 최근 NPC 대사 바로 아래에 뜻과 단어 풀이를 붙인다.
    /// 뜻을 펼친 동안 풀이를 받은 NPC 대사의 풀이 단어는 눌러 볼 수 있게 밑줄 친 링크로 둔다.
    /// </summary>
    public static class DialogueLinesText
    {
        private const string UserSpeaker = "나";

        // 크림색 대화창 위에서도 읽히는 회색. 더 옅으면 뜻이 배경에 묻힌다.
        private const string MeaningColor = "#6E7888";

        /// <summary>
        /// 고른 단어를 칠하는 반투명 주황(말하기 버튼 색). 단어 카드가 어느 단어의 뜻인지 대사 안에서 바로 보이게 한다.
        /// </summary>
        public const string SelectedWordMark = "#F982564D";

        /// <param name="showsMeaning">[뜻 보기]를 펼쳤는지. 뜻이 아직 없으면 펼쳐도 붙이지 않는다.</param>
        /// <param name="glossary">가장 최근 NPC 대사의 단어 풀이. 없으면 null이다.</param>
        /// <param name="selected">단어 카드에 보이는 단어. 없으면 null이다.</param>
        public static string Format(
            IReadOnlyList<DialogueLine> lines,
            string npcName,
            bool showsMeaning,
            IReadOnlyList<GlossaryData> glossary,
            GlossaryWord selected = null
        )
        {
            int latestNpcIndex = FindLatestNpcIndex(lines);
            var rows = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];
                string speaker = line.IsNpc ? npcName : UserSpeaker;
                string english = showsMeaning
                    ? LinkGlossary(line.English, line.Glossary, i, selected)
                    : line.English;
                rows.Add($"<b>{speaker}</b>  {english}");
                if (i == latestNpcIndex && showsMeaning && line.Korean != null)
                {
                    rows.Add($"<color={MeaningColor}>{line.Korean}</color>");
                    if (glossary != null && glossary.Count > 0)
                    {
                        rows.Add(
                            $"<color={MeaningColor}>단어 풀이 · {FormatGlossary(glossary)}</color>"
                        );
                    }
                }
            }

            // 줄바꿈은 운영체제와 상관없이 \n으로 맞춘다.
            return string.Join("\n", rows);
        }

        /// <summary>대사 칸에서 누른 링크의 풀이 단어. 없는 링크면 null이다.</summary>
        public static GlossaryWord FindWord(IReadOnlyList<DialogueLine> lines, string linkId)
        {
            string[] parts = (linkId ?? "").Split('/');
            if (
                parts.Length != 2
                || !int.TryParse(parts[0], out int lineIndex)
                || !int.TryParse(parts[1], out int wordIndex)
                || lineIndex < 0
                || lineIndex >= lines.Count
            )
            {
                return null;
            }

            IReadOnlyList<GlossaryWord> glossary = lines[lineIndex].Glossary;
            return glossary != null && wordIndex >= 0 && wordIndex < glossary.Count
                ? glossary[wordIndex]
                : null;
        }

        private static int FindLatestNpcIndex(IReadOnlyList<DialogueLine> lines)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].IsNpc)
                    return i;
            }

            return -1;
        }

        // 풀이 단어를 대사에서 찾아 "<대사 번호>/<풀이 번호>" 링크로 감싼다. 대사에서 찾지 못한 풀이는 아래 단어 풀이 줄에만 보인다.
        // 단어 카드에 보이는 단어는 칠해 둔다.
        private static string LinkGlossary(
            string english,
            IReadOnlyList<GlossaryWord> glossary,
            int lineIndex,
            GlossaryWord selected
        )
        {
            if (glossary == null || glossary.Count == 0)
            {
                return english;
            }

            var found = new List<(int Start, int Length, int WordIndex)>();
            for (int wordIndex = 0; wordIndex < glossary.Count; wordIndex++)
            {
                string text = glossary[wordIndex].Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                Match match = WholeWords(text).Match(english);
                while (match.Success && Overlaps(found, match))
                {
                    match = match.NextMatch();
                }

                if (match.Success)
                {
                    found.Add((match.Index, match.Length, wordIndex));
                }
            }

            found.Sort((left, right) => left.Start.CompareTo(right.Start));
            var linked = new StringBuilder();
            int next = 0;
            foreach ((int start, int length, int wordIndex) in found)
            {
                linked.Append(english, next, start - next);
                string word = $"<u>{english.Substring(start, length)}</u>";
                if (glossary[wordIndex] == selected)
                {
                    word = $"<mark={SelectedWordMark}>{word}</mark>";
                }

                linked.Append($"<link=\"{lineIndex}/{wordIndex}\">{word}</link>");
                next = start + length;
            }

            linked.Append(english, next, english.Length - next);
            return linked.ToString();
        }

        // 서버 풀이는 문장 첫머리처럼 대소문자가 다를 수 있다. 다른 단어 속 글자(good의 go)는 고르지 않도록 앞뒤가 글자·숫자가 아닌 곳만 찾는다.
        private static Regex WholeWords(string text)
        {
            return new Regex(
                @"(?<![\p{L}\p{N}])" + Regex.Escape(text.Trim()) + @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase
            );
        }

        private static bool Overlaps(
            List<(int Start, int Length, int WordIndex)> found,
            Match match
        )
        {
            foreach (var (start, length, _) in found)
            {
                if (match.Index < start + length && start < match.Index + match.Length)
                    return true;
            }

            return false;
        }

        private static string FormatGlossary(IReadOnlyList<GlossaryData> glossary)
        {
            var items = new List<string>();
            foreach (GlossaryData item in glossary)
            {
                items.Add($"{item.Text} {item.MeaningKo}");
            }

            return string.Join(" · ", items);
        }
    }
}
