using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화창 대사 칸의 글자. 이번 대화의 대사를 말한 사람과 함께 처음부터 보이고,
    /// 뜻을 펼치면 가장 최근 NPC 대사 바로 아래에 뜻과 단어 풀이를 붙인다.
    /// 뜻을 펼친 동안 풀이를 받은 NPC 대사의 풀이 단어는 눌러 볼 수 있게 밑줄 친 링크로 둔다.
    /// 자막을 끄면 NPC 대사 글자를 투명하게 두어 듣고 이해하게 한다.
    /// </summary>
    public static class DialogueLinesText
    {
        private const string UserSpeaker = "나";

        // 말한 사람 이름은 흰 대사와 구분되게 칠한다. NPC는 코랄 이름표와 같은 계열의 밝은 코랄, 나는 밝은 하늘색이다.
        private const string NpcSpeakerColor = "#FFB199";
        private const string UserSpeakerColor = "#9ED3FF";

        // 어두운 그라데이션 위에서 흰 대사보다 한 단계 옅게 읽히는 따뜻한 색. 회색은 어두운 바탕에 묻힌다.
        private const string MeaningColor = "#E6D3BC";

        // 뜻은 영어 대사를 돕는 글이라 대사보다 작게 쓴다.
        private const string MeaningSize = "80%";

        /// <summary>
        /// 고른 단어를 칠하는 반투명 주황(말하기 버튼 색). 단어 카드가 어느 단어의 뜻인지 대사 안에서 바로 보이게 한다.
        /// </summary>
        public const string SelectedWordMark = "#F982564D";

        /// <summary>
        /// 자막을 끈 NPC 대사를 감싸는 링크. 대사 글자는 투명하게 자리만 차지하고,
        /// 대사 칸(<see cref="NpcLineCovers"/>)이 이 링크의 단어 자리를 회색 막대로 덮는다.
        /// </summary>
        public const string HiddenLineLink = "hidden-npc-line";

        /// <param name="showsMeaning">[뜻 보기]를 펼쳤는지. 뜻이 아직 없으면 펼쳐도 붙이지 않는다.</param>
        /// <param name="glossary">가장 최근 NPC 대사의 단어 풀이. 없으면 null이다.</param>
        /// <param name="selected">단어 카드에 보이는 단어. 없으면 null이다.</param>
        /// <param name="sentenceIndent">
        /// 이름 뒤 문장이 시작하는 자리. 0보다 크면 이름 길이와 상관없이 모든 문장과 그 아래 뜻을 이 자리에서 시작하고, 긴 문장이 줄을 바꿔도 같은 자리에 맞춘다.
        /// </param>
        /// <param name="hidesNpcLines">자막을 껐는지. 뜻을 펼친 동안은 뜻과 맞춰 보도록 가리지 않는다.</param>
        public static string Format(
            IReadOnlyList<DialogueLine> lines,
            string npcName,
            bool showsMeaning,
            IReadOnlyList<GlossaryData> glossary,
            GlossaryWord selected = null,
            float sentenceIndent = 0,
            bool hidesNpcLines = false
        )
        {
            int latestNpcIndex = FindLatestNpcIndex(lines);
            var rows = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];
                string speaker = line.IsNpc
                    ? $"<color={NpcSpeakerColor}><b>{npcName}</b></color>"
                    : $"<color={UserSpeakerColor}><b>{UserSpeaker}</b></color>";
                string english = showsMeaning
                    ? LinkGlossary(line.English, line.Glossary, i, selected)
                    : line.English;
                if (line.IsNpc && hidesNpcLines && !showsMeaning)
                    english = Hidden(english);
                rows.Add(
                    sentenceIndent > 0
                        ? $"{speaker}{Indented(english, sentenceIndent)}"
                        : $"{speaker}  {english}"
                );
                if (i == latestNpcIndex && showsMeaning && line.Korean != null)
                {
                    rows.Add(
                        Indented(
                            $"<size={MeaningSize}><color={MeaningColor}>{line.Korean}</color></size>",
                            sentenceIndent
                        )
                    );
                    if (glossary != null && glossary.Count > 0)
                    {
                        rows.Add(
                            Indented(
                                $"<size={MeaningSize}><color={MeaningColor}>단어 풀이 · {FormatGlossary(glossary)}</color></size>",
                                sentenceIndent
                            )
                        );
                    }
                }
            }

            // 줄바꿈은 운영체제와 상관없이 \n으로 맞춘다.
            return string.Join("\n", rows);
        }

        // 글자를 지우지 않고 투명하게 그려야 줄바꿈과 띄어쓰기가 원래 대사와 같은 자리에 남는다.
        private static string Hidden(string english) =>
            $"<link=\"{HiddenLineLink}\"><alpha=#00>{english}<alpha=#FF></link>";

        private static string Indented(string text, float indent) =>
            indent > 0 ? $"<indent={indent:0.#}>{text}</indent>" : text;

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
        public static string LinkGlossary(
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
