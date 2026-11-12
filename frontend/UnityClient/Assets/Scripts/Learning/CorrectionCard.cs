using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace A305.Learning
{
    /// <summary>문장 교정 카드가 보여 줄 발화의 종류.</summary>
    public enum CorrectionKind
    {
        /// <summary>영어 문장을 고쳤다. 내가 한 말에 줄을 긋고 고친 문장을 보인다.</summary>
        Corrected,

        /// <summary>한국어(한영 혼합 포함)로 말했다. 내가 한 말은 그대로 두고 영어 문장을 보인다.</summary>
        Korean,

        /// <summary>고칠 것이 없는 영어 문장이다.</summary>
        WellSaid,

        /// <summary>문법 오류가 아닌 표현 확장이나, 의도 확인이 필요한 말이다.</summary>
        ExpressionPractice,
    }

    /// <summary>
    /// 문장 교정 탭의 카드 한 장(시안 F2). 발화마다 고친 문장, 한국어로 한 말의 영어 문장, 잘한 문장 중 하나로 보이고,
    /// 담을 영어 문장이 있을 때만 [담기]를 둔다. 전체 대화의 문장 피드백도 같은 이름표를 쓴다.
    /// </summary>
    public sealed class CorrectionCard
    {
        private static readonly Regex Hangul = new Regex("[가-힣ㄱ-ㅎㅏ-ㅣ]");

        private CorrectionCard(UtteranceReview review)
        {
            Said = review.Utterance;
            Reason = review.Explanation;
            Kind = KindOf(review);
            Better = review.HasAlternative ? review.Reference.English : null;
        }

        private static CorrectionKind KindOf(UtteranceReview review)
        {
            if (IsKorean(review.Utterance))
                return CorrectionKind.Korean;
            if (
                review.Verdict == "needs_clarification"
                || review.Verdict == "not_assessable"
                || review.SuggestionKind == "natural_alternative"
                || review.SuggestionKind == "idiomatic_expression"
                || review.SuggestionKind == "context_fit"
            )
                return CorrectionKind.ExpressionPractice;
            return review.HasAlternative ? CorrectionKind.Corrected : CorrectionKind.WellSaid;
        }

        public static IReadOnlyList<CorrectionCard> From(ConversationFeedback feedback)
        {
            var cards = new List<CorrectionCard>();
            foreach (UtteranceReview review in feedback.Reviews)
                cards.Add(From(review));
            return cards;
        }

        public static CorrectionCard From(UtteranceReview review) => new CorrectionCard(review);

        /// <summary>한글이 한 글자라도 있으면 한국어로 한 말로 본다. AI 피드백이 영어 문장을 주는 기준과 같다.</summary>
        public static bool IsKorean(string text) => text != null && Hangul.IsMatch(text);

        public CorrectionKind Kind { get; }

        /// <summary>첫 줄 칩 이름표</summary>
        public string SaidLabel =>
            Kind switch
            {
                CorrectionKind.Corrected => "내가 한 말",
                CorrectionKind.Korean => "내가 한 말 · 한국어",
                CorrectionKind.ExpressionPractice => "내가 한 말",
                _ => "잘했어요",
            };

        /// <summary>내가 한 말. 고칠 것이 없으면 이 문장을 그대로 잘한 문장으로 보인다.</summary>
        public string Said { get; }

        /// <summary>고친 영어 문장에만 줄을 긋는다. 한국어로 한 말은 틀린 문장이 아니다.</summary>
        public bool IsSaidStruck => Kind == CorrectionKind.Corrected;

        /// <summary>둘째 줄 칩 이름표. 영어 문장이 없으면 null이다.</summary>
        public string BetterLabel
        {
            get
            {
                if (Better == null)
                    return null;
                return Kind == CorrectionKind.Korean ? "영어로는" : "이렇게 말해요";
            }
        }

        /// <summary>고친 문장이나 한국어로 한 말의 영어 문장. 없으면 null이다.</summary>
        public string Better { get; }

        /// <summary>AI가 쓴 이유. 없으면 비어 있다.</summary>
        public string Reason { get; }

        /// <summary>[담기]로 단어장에 담을 영어 문장이 있는지</summary>
        public bool CanSave => Better != null;
    }
}
