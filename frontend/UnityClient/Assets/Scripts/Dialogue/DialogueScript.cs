using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC 한 명과의 대화 대본. 정해진 턴이 있으면 튜토리얼처럼 읽을 대사를 따라가고,
    /// 없으면 키워드 응답 규칙과 기본 응답으로 자유 대화를 흉내 낸다.
    /// </summary>
    public sealed class DialogueScript
    {
        public DialogueScript(
            string npcName,
            DialogueLine openingLine,
            IReadOnlyList<GuidedTurn> guidedTurns,
            IReadOnlyList<KeywordReply> keywordReplies,
            DialogueLine defaultReply,
            string exampleSentence,
            string roleLabel = null
        )
        {
            NpcName = npcName;
            RoleLabel = roleLabel;
            OpeningLine = openingLine;
            GuidedTurns = guidedTurns;
            KeywordReplies = keywordReplies;
            DefaultReply = defaultReply;
            ExampleSentence = exampleSentence;
        }

        public string NpcName { get; }

        /// <summary>대화 제안처럼 누가 말을 거는지 알릴 때 쓰는 역할(예: 드럭스토어 직원). 없으면 null이다.</summary>
        public string RoleLabel { get; }
        public DialogueLine OpeningLine { get; }
        public IReadOnlyList<GuidedTurn> GuidedTurns { get; }

        /// <summary>위에서부터 확인해 처음 맞는 규칙으로 답한다.</summary>
        public IReadOnlyList<KeywordReply> KeywordReplies { get; }

        public DialogueLine DefaultReply { get; }

        /// <summary>자유 대화에서 [예시 문장]으로 입력창에 넣어 주는 문장</summary>
        public string ExampleSentence { get; }
    }
}
