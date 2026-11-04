using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화의 한 줄. NPC 대사는 한국어 뜻과(서버 대화에서 뜻을 받았으면) 단어 풀이를 함께 가지고, 사용자 발화는 입력한 문장만 가진다.
    /// </summary>
    public sealed class DialogueLine
    {
        private DialogueLine(
            bool isNpc,
            string english,
            string korean,
            IReadOnlyList<GlossaryWord> glossary,
            byte[] speech
        )
        {
            IsNpc = isNpc;
            English = english;
            Korean = korean;
            Glossary = glossary;
            Speech = speech;
        }

        public bool IsNpc { get; }

        /// <summary>화면에 보이는 영어 문장. 사용자 발화는 입력한 그대로다.</summary>
        public string English { get; }

        /// <summary>NPC 대사의 한국어 뜻. 사용자 발화는 null이다.</summary>
        public string Korean { get; }

        /// <summary>대사 안에서 눌러 볼 단어 풀이. 서버 대화에서 뜻을 받기 전이거나 대본 대사면 null이다.</summary>
        public IReadOnlyList<GlossaryWord> Glossary { get; }

        /// <summary>
        /// 대본 대사를 NPC 목소리로 미리 합성한 음성(입 모양 타이밍이 든 WAV). 서버 대화 대사는 음성을 따로 받으므로 null이고,
        /// 음성을 준비하지 않은 대본 대사도 null이다.
        /// </summary>
        public byte[] Speech { get; }

        public static DialogueLine FromNpc(
            string english,
            string korean,
            IReadOnlyList<GlossaryWord> glossary = null,
            byte[] speech = null
        )
        {
            return new DialogueLine(true, english, korean, glossary, speech);
        }

        public static DialogueLine FromUser(string text)
        {
            return new DialogueLine(false, text, null, null, null);
        }
    }
}
