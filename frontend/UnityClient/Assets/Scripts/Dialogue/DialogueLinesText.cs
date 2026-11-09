using System;
using System.Collections.Generic;
using System.Linq;
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
                    List<GlossaryData> notLinked = NotLinked(glossary, line);
                    if (notLinked.Count > 0)
                    {
                        rows.Add(
                            Indented(
                                $"<size={MeaningSize}><color={MeaningColor}>단어 풀이 · {FormatGlossary(notLinked)}</color></size>",
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

        /// <summary>
        /// 대사 칸에서 누른 링크가 가리키는 풀이. 링크는 그 자리를 덮는 풀이(누른 단어와 그 단어가 든 숙어)를 짧은 말부터 가리킨다.
        /// 없는 링크면 빈 목록이다.
        /// </summary>
        public static IReadOnlyList<GlossaryWord> FindWords(
            IReadOnlyList<DialogueLine> lines,
            string linkId
        )
        {
            string[] parts = (linkId ?? "").Split('/');
            if (
                parts.Length != 2
                || !int.TryParse(parts[0], out int lineIndex)
                || lineIndex < 0
                || lineIndex >= lines.Count
            )
            {
                return Array.Empty<GlossaryWord>();
            }

            IReadOnlyList<GlossaryWord> glossary = lines[lineIndex].Glossary;
            var words = new List<GlossaryWord>();
            foreach (string part in parts[1].Split(','))
            {
                if (
                    glossary == null
                    || !int.TryParse(part, out int wordIndex)
                    || wordIndex < 0
                    || wordIndex >= glossary.Count
                )
                {
                    return Array.Empty<GlossaryWord>();
                }

                words.Add(glossary[wordIndex]);
            }

            return words;
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

        // 풀이 단어를 대사에서 찾아 밑줄 링크로 감싼다. 숙어와 그 안의 단어처럼 풀이가 겹치면 겹침이 바뀌는 자리마다 조각을 나누고,
        // 조각 링크 "<대사 번호>/<풀이 번호>,<풀이 번호>…"는 그 조각을 덮는 풀이를 짧은 말부터 모두 가리킨다.
        // 대사에서 찾지 못한 풀이는 아래 단어 풀이 줄에만 보인다. 단어 카드가 먼저 보이는 풀이를 덮는 조각은 칠해 둔다.
        public static string LinkGlossary(
            string english,
            IReadOnlyList<GlossaryWord> glossary,
            int lineIndex,
            GlossaryWord selected
        )
        {
            List<Piece> pieces = Pieces(english, glossary);
            if (pieces.Count == 0)
            {
                return english;
            }

            var linked = new StringBuilder();
            int next = 0;
            foreach (Piece piece in pieces)
            {
                linked.Append(english, next, piece.Start - next);
                string text = $"<u>{english.Substring(piece.Start, piece.Length)}</u>";
                if (piece.Words.Exists(index => glossary[index] == selected))
                {
                    text = $"<mark={SelectedWordMark}>{text}</mark>";
                }

                linked.Append(
                    $"<link=\"{lineIndex}/{string.Join(",", piece.Words)}\">{text}</link>"
                );
                next = piece.Start + piece.Length;
            }

            linked.Append(english, next, english.Length - next);
            return linked.ToString();
        }

        // 대사에서 밑줄을 칠 한 조각. Words는 이 조각을 덮는 풀이 번호이고 짧은 말이 먼저다.
        private sealed class Piece
        {
            public int Start;
            public int Length;
            public List<int> Words;
        }

        // 찾은 풀이들의 시작·끝 자리로 대사를 자르고, 같은 풀이가 덮는 이웃 조각은 하나로 합친다.
        private static List<Piece> Pieces(string english, IReadOnlyList<GlossaryWord> glossary)
        {
            List<(int Start, int End, int WordIndex)> matches = FindMatches(english, glossary);
            var cuts = new SortedSet<int>();
            foreach ((int start, int end, _) in matches)
            {
                cuts.Add(start);
                cuts.Add(end);
            }

            var pieces = new List<Piece>();
            int previous = -1;
            foreach (int cut in cuts)
            {
                if (previous >= 0)
                {
                    List<int> words = Covering(matches, previous, cut);
                    Piece last = pieces.Count > 0 ? pieces[pieces.Count - 1] : null;
                    if (
                        last != null
                        && last.Start + last.Length == previous
                        && last.Words.SequenceEqual(words)
                    )
                    {
                        last.Length += cut - previous;
                    }
                    else if (words.Count > 0)
                    {
                        pieces.Add(
                            new Piece
                            {
                                Start = previous,
                                Length = cut - previous,
                                Words = words,
                            }
                        );
                    }
                }

                previous = cut;
            }

            return pieces;
        }

        // start~end 자리를 덮는 풀이 번호. 누른 말에 가까운 짧은 풀이부터 둔다.
        private static List<int> Covering(
            List<(int Start, int End, int WordIndex)> matches,
            int start,
            int end
        )
        {
            return matches
                .Where(match => match.Start <= start && end <= match.End)
                .OrderBy(match => match.End - match.Start)
                .ThenBy(match => match.WordIndex)
                .Select(match => match.WordIndex)
                .ToList();
        }

        // 풀이마다 대사에서 처음 나오는 자리를 찾는다. 숙어와 그 안의 단어처럼 다른 풀이와 겹쳐도 찾는다.
        private static List<(int Start, int End, int WordIndex)> FindMatches(
            string english,
            IReadOnlyList<GlossaryWord> glossary
        )
        {
            var matches = new List<(int Start, int End, int WordIndex)>();
            if (glossary == null)
            {
                return matches;
            }

            for (int wordIndex = 0; wordIndex < glossary.Count; wordIndex++)
            {
                string text = glossary[wordIndex].Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                Match match = WholeWords(text).Match(english);
                if (match.Success)
                {
                    matches.Add((match.Index, match.Index + match.Length, wordIndex));
                }
            }

            return matches;
        }

        // 대사 안에서 밑줄 링크가 된 풀이는 눌러서 볼 수 있으므로, 아래 단어 풀이 줄에는 링크하지 못한 풀이만 남긴다.
        private static List<GlossaryData> NotLinked(
            IReadOnlyList<GlossaryData> glossary,
            DialogueLine line
        )
        {
            var notLinked = new List<GlossaryData>();
            if (glossary == null)
            {
                return notLinked;
            }

            var linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((_, _, int wordIndex) in FindMatches(line.English, line.Glossary))
            {
                linked.Add(line.Glossary[wordIndex].Text.Trim());
            }

            foreach (GlossaryData item in glossary)
            {
                if (!linked.Contains(item.Text?.Trim() ?? ""))
                {
                    notLinked.Add(item);
                }
            }

            return notLinked;
        }

        // 서버 풀이는 문장 첫머리처럼 대소문자가 다를 수 있다. 다른 단어 속 글자(good의 go)는 고르지 않도록 앞뒤가 글자·숫자가 아닌 곳만 찾는다.
        private static Regex WholeWords(string text)
        {
            return new Regex(
                @"(?<![\p{L}\p{N}])" + Regex.Escape(text.Trim()) + @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase
            );
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
