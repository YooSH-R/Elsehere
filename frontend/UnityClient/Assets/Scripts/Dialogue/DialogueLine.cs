namespace A305.Dialogue
{
    /// <summary>대화의 한 줄. NPC 대사는 한국어 뜻을 함께 가지고, 사용자 발화는 입력한 문장만 가진다.</summary>
    public sealed class DialogueLine
    {
        private DialogueLine(bool isNpc, string english, string korean)
        {
            IsNpc = isNpc;
            English = english;
            Korean = korean;
        }

        public bool IsNpc { get; }

        /// <summary>화면에 보이는 영어 문장. 사용자 발화는 입력한 그대로다.</summary>
        public string English { get; }

        /// <summary>NPC 대사의 한국어 뜻. 사용자 발화는 null이다.</summary>
        public string Korean { get; }

        public static DialogueLine FromNpc(string english, string korean)
        {
            return new DialogueLine(true, english, korean);
        }

        public static DialogueLine FromUser(string text)
        {
            return new DialogueLine(false, text, null);
        }
    }
}
