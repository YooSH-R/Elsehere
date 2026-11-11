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
            bool isGuidedCompleted,
            ServerFeedback serverFeedback = null,
            long? reviewCardId = null,
            ReviewPracticeResult reviewResult = null,
            bool isFeedbackDeferred = false,
            string practiceScenarioId = null
        )
        {
            NpcCode = npcCode;
            NpcName = npcName;
            Lines = lines;
            UserUtterances = userUtterances;
            IsGuidedCompleted = isGuidedCompleted;
            ServerFeedback = serverFeedback;
            ReviewCardId = reviewCardId;
            ReviewResult = reviewResult;
            IsFeedbackDeferred = isFeedbackDeferred;
            PracticeScenarioId = practiceScenarioId;
        }

        /// <summary>ERD npcs.code</summary>
        public string NpcCode { get; }

        public string NpcName { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }

        /// <summary>사용자가 보낸 문장을 보낸 순서대로 담는다.</summary>
        public IReadOnlyList<string> UserUtterances { get; }

        /// <summary>정해진 흐름의 마지막 턴까지 마쳤는지. 자유 대화는 항상 false다.</summary>
        public bool IsGuidedCompleted { get; }

        /// <summary>서버 AI와 나눈 대화의 종료 피드백. 대본 대화이거나 서버 대화를 시작하지 못했으면 null이다.</summary>
        public ServerFeedback ServerFeedback { get; }
        public long? ReviewCardId { get; }
        public ReviewPracticeResult ReviewResult { get; }

        /// <summary>장소 연습으로 연 대화면 그 상황 ID. 장소 연습이 아니면 null이다.</summary>
        public string PracticeScenarioId { get; }

        /// <summary>사용자가 피드백 생성을 기다리지 않고 [나중에 확인하기]로 넘긴 대화인지.</summary>
        public bool IsFeedbackDeferred { get; }
    }
}
