using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화창 대사 칸의 글자. 이번 대화의 대사를 말한 사람과 함께 처음부터 보이고,
    /// 뜻을 펼치면 가장 최근 NPC 대사 바로 아래에 뜻과 단어 풀이를 붙인다.
    /// </summary>
    public static class DialogueLinesText
    {
        private const string UserSpeaker = "나";
        private const string MeaningColor = "#B8C0CC";

        /// <param name="showsMeaning">[뜻 보기]를 펼쳤는지. 뜻이 아직 없으면 펼쳐도 붙이지 않는다.</param>
        /// <param name="glossary">가장 최근 NPC 대사의 단어 풀이. 없으면 null이다.</param>
        public static string Format(
            IReadOnlyList<DialogueLine> lines,
            string npcName,
            bool showsMeaning,
            IReadOnlyList<GlossaryData> glossary
        )
        {
            int latestNpcIndex = FindLatestNpcIndex(lines);
            var rows = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine line = lines[i];
                string speaker = line.IsNpc ? npcName : UserSpeaker;
                rows.Add($"<b>{speaker}</b>  {line.English}");
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

        private static int FindLatestNpcIndex(IReadOnlyList<DialogueLine> lines)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].IsNpc)
                    return i;
            }

            return -1;
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
