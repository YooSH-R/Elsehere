using System.Collections.Generic;
using System.Linq;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC 대사의 단어 풀이(CONV-04 <c>glossary</c>) 하나. 단어이거나 <c>to go</c> 같은 숙어다. 대사 안에서 눌러 뜻을 보고,
    /// 그 턴 출처(VOCAB-02 <c>NPC_LINE</c>)로 단어장에 저장한다.
    /// </summary>
    public sealed class GlossaryWord
    {
        public GlossaryWord(long turnId, string text, string meaningKo)
        {
            TurnId = turnId;
            Text = text;
            MeaningKo = meaningKo;
        }

        /// <summary>풀이를 받은 NPC 턴. 단어장 저장에 보낸다.</summary>
        public long TurnId { get; }

        /// <summary>대사에 그대로 있는 말</summary>
        public string Text { get; }

        public string MeaningKo { get; }

        /// <summary>한 턴의 단어 풀이를 그 턴 ID와 함께 묶는다. 풀이가 없으면 빈 목록이다.</summary>
        public static IReadOnlyList<GlossaryWord> FromMeaning(
            long turnId,
            IReadOnlyList<GlossaryData> glossary
        )
        {
            if (glossary == null)
            {
                return new GlossaryWord[0];
            }

            return glossary
                .Select(item => new GlossaryWord(turnId, item.Text, item.MeaningKo))
                .ToList();
        }
    }
}
