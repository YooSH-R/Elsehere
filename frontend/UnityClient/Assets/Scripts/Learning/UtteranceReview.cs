using System;
using System.Text.RegularExpressions;

namespace A305.Learning
{
    /// <summary>상세 피드백의 한 줄. 사용자가 말한 문장과, 있으면 참고 표현.</summary>
    public readonly struct UtteranceReview
    {
        public UtteranceReview(
            string utterance,
            ReferenceExpression reference,
            string comment = null,
            ReferenceExpression idiom = null
        )
        {
            Utterance = utterance;
            Reference = reference;
            Comment = comment;
            Idiom = idiom;
        }

        public string Utterance { get; }

        /// <summary>참고 표현이 없으면 null. 대본 예시에서는 지금 문장 그대로도 자연스럽다는 뜻이다.</summary>
        public ReferenceExpression Reference { get; }

        /// <summary>대소문자·공백·끝 문장부호만 다른 제안은 같은 발화로 본다.</summary>
        public bool HasAlternative =>
            !string.IsNullOrWhiteSpace(Reference?.English)
            && !string.Equals(
                SpokenText(Utterance),
                SpokenText(Reference.English),
                StringComparison.OrdinalIgnoreCase
            );

        /// <summary>동일한 제안을 숨겨도 모델이 설명한 이유는 유지한다.</summary>
        public string Explanation =>
            !string.IsNullOrWhiteSpace(Reference?.Reason) ? Reference.Reason : Comment;

        private static string SpokenText(string text) =>
            Regex
                .Replace(
                    (text ?? "")
                        .Replace('’', '\'')
                        .Replace('‘', '\'')
                        .Trim()
                        .TrimEnd('.', '!', '?'),
                    @"\s+",
                    " "
                )
                .Trim();

        /// <summary>
        /// 참고 표현 없이 보여 줄 평가. 서버 피드백에서 고칠 표현이 없는 발화에 AI가 쓴 이유이며, 없으면 null이다.
        /// </summary>
        public string Comment { get; }

        /// <summary>서버 피드백의 회화 표현과 뜻. 고칠 표현이 없는 발화에도 붙을 수 있으며, 없으면 null이다.</summary>
        public ReferenceExpression Idiom { get; }
    }
}
