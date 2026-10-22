using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>끝난 대화의 기록. 피드백·미션·튜토리얼이 대화 결과를 판단할 때 쓴다.</summary>
    public sealed class DialogueRecord
    {
        public DialogueRecord(
            string npcCode,
            string npcName,
            IReadOnlyList<DialogueLine> lines,
            IReadOnlyList<string> userUtterances,
            bool isGuidedCompleted
        )
        {
            NpcCode = npcCode;
            NpcName = npcName;
            Lines = lines;
            UserUtterances = userUtterances;
            IsGuidedCompleted = isGuidedCompleted;
        }

        /// <summary>ERD npcs.code</summary>
        public string NpcCode { get; }

        public string NpcName { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }

        /// <summary>사용자가 보낸 문장을 보낸 순서대로 담는다.</summary>
        public IReadOnlyList<string> UserUtterances { get; }

        /// <summary>정해진 흐름의 마지막 턴까지 마쳤는지. 자유 대화는 항상 false다.</summary>
        public bool IsGuidedCompleted { get; }
    }
}
