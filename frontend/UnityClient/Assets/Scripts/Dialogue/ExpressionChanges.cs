using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace A305.Dialogue
{
    /// <summary>
    /// 영어 표현 카드의 고친 문장에서 학습자가 한 말과 달라진 곳을 찾아 표시한다.
    /// 낱말 단위로 비교해(대소문자 구분) 학습자가 그대로 말한 낱말은 두고, 새로 생기거나 바뀐 낱말에 밑줄을 긋는다.
    /// 문장 부호는 표시하지 않고, 공백만 사이에 둔 바뀐 낱말은 한 덩어리로 묶는다.
    /// </summary>
    public static class ExpressionChanges
    {
        // 바뀐 곳은 테마 강조색(accentInk)에 밑줄로 보인다. 다른 표현 번호 칸과 같은 색이다.
        private const string MarkStart = "<color=#D24F2F><u>";
        private const string MarkEnd = "</u></color>";

        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*");
        private static readonly Regex Latin = new Regex("[A-Za-z]");

        /// <summary>고친 문장에서 표시할 부분들. 앞에서부터 차례로다.</summary>
        public static IReadOnlyList<string> ChangedParts(string said, string fixedSentence) =>
            ChangedRanges(said, fixedSentence)
                .Select(range => fixedSentence.Substring(range.Start, range.Length))
                .ToArray();

        /// <summary>
        /// 바뀐 곳에 강조색 밑줄을 그은 TextMeshPro 글. 문장 안의 '&lt;'를 꾸밈 태그로 읽지 않게 모든 글자를 noparse로 감싼다.
        /// </summary>
        public static string Mark(string said, string fixedSentence)
        {
            var text = new StringBuilder();
            int done = 0;
            foreach ((int start, int length) in ChangedRanges(said, fixedSentence))
            {
                text.Append(Plain(fixedSentence.Substring(done, start - done)));
                text.Append(MarkStart)
                    .Append(Plain(fixedSentence.Substring(start, length)))
                    .Append(MarkEnd);
                done = start + length;
            }
            return text.Append(Plain(fixedSentence.Substring(done))).ToString();
        }

        private static string Plain(string part) =>
            part.Length == 0 ? string.Empty : $"<noparse>{part}</noparse>";

        // 고친 문장 안에서 바뀐 낱말 덩어리의 (시작, 길이).
        private static List<(int Start, int Length)> ChangedRanges(
            string said,
            string fixedSentence
        )
        {
            var ranges = new List<(int Start, int Length)>();
            // 영어를 한 글자도 말하지 않았으면 문장 전체가 새로 생긴 것이라 표시하지 않는다.
            if (string.IsNullOrEmpty(fixedSentence) || said == null || !Latin.IsMatch(said))
                return ranges;
            Match[] words = Word.Matches(fixedSentence).Cast<Match>().ToArray();
            bool[] kept = KeptWords(
                Word.Matches(said).Cast<Match>().Select(match => match.Value).ToArray(),
                words.Select(match => match.Value).ToArray()
            );
            for (int index = 0; index < words.Length; index++)
            {
                if (kept[index])
                    continue;
                Match word = words[index];
                bool joinsPrevious =
                    ranges.Count > 0
                    && !kept[index - 1]
                    && string.IsNullOrWhiteSpace(
                        fixedSentence.Substring(
                            ranges[^1].Start + ranges[^1].Length,
                            word.Index - (ranges[^1].Start + ranges[^1].Length)
                        )
                    );
                if (joinsPrevious)
                    ranges[^1] = (ranges[^1].Start, word.Index + word.Length - ranges[^1].Start);
                else
                    ranges.Add((word.Index, word.Length));
            }
            return ranges;
        }

        // 고친 문장의 낱말마다 학습자가 그대로 말한 낱말인지. 가장 긴 공통 낱말 순서(LCS)로 짝짓는다.
        private static bool[] KeptWords(string[] said, string[] fixedWords)
        {
            var common = new int[said.Length + 1, fixedWords.Length + 1];
            for (int s = said.Length - 1; s >= 0; s--)
            for (int f = fixedWords.Length - 1; f >= 0; f--)
                common[s, f] = string.Equals(said[s], fixedWords[f], StringComparison.Ordinal)
                    ? common[s + 1, f + 1] + 1
                    : Math.Max(common[s + 1, f], common[s, f + 1]);
            var kept = new bool[fixedWords.Length];
            int i = 0,
                j = 0;
            while (i < said.Length && j < fixedWords.Length)
            {
                if (string.Equals(said[i], fixedWords[j], StringComparison.Ordinal))
                {
                    kept[j] = true;
                    i++;
                    j++;
                }
                else if (common[i + 1, j] >= common[i, j + 1])
                    i++;
                else
                    j++;
            }
            return kept;
        }
    }
}
