using System.Collections.Generic;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>피드백 창의 탭. 순서는 머리줄 탭 버튼 순서와 같다.</summary>
    public enum FeedbackSection
    {
        Summary,
        Corrections,
        Conversation,
    }

    /// <summary>요약 탭의 "이 상황에서 자주 써요" 표현 줄과 탭이 비었을 때의 안내 문구.</summary>
    public static class FeedbackPages
    {
        public const string SituationTitle = "이 상황에서 자주 써요";

        /// <summary>문장 교정 탭에 보일 발화가 없을 때의 안내</summary>
        public const string NoCorrections = "이 대화에는 문장별 교정 내용이 없어요.";

        /// <summary>영어 표현이 있는 "이 상황에서 자주 써요" 표현만 시안 순서대로 고른다.</summary>
        public static IReadOnlyList<SituationExpressionData> Situations(
            ConversationFeedback feedback
        )
        {
            var shown = new List<SituationExpressionData>();
            foreach (SituationExpressionData situation in feedback.SituationExpressions)
            {
                if (!string.IsNullOrWhiteSpace(situation.Expression))
                    shown.Add(situation);
            }
            return shown;
        }

        /// <summary>표현 줄의 한국어 한 줄: 뜻 · 쓰는 때. 쓰는 때가 없으면 뜻만 보인다.</summary>
        public static string MeaningLine(SituationExpressionData situation)
        {
            string usage = situation.UsageKo?.Trim();
            if (string.IsNullOrEmpty(usage))
                return situation.MeaningKo ?? "";
            if (string.IsNullOrEmpty(situation.MeaningKo))
                return usage;
            return $"{situation.MeaningKo} · {usage}";
        }
    }
}
