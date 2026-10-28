using System.Collections.Generic;
using System.Text;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>
    /// 피드백 화면 본문에 보일 문구. 짧은 피드백은 잘한 점·아쉬운 점과 다음 대화 표현, 상세 피드백은 발화별 참고 표현이다.
    /// </summary>
    public static class FeedbackText
    {
        private const string ServerNotice =
            "AI가 대화를 보고 만든 피드백이에요. 점수가 아니라 다음 대화를 위한 참고예요.";

        /// <summary>
        /// [대화 기록 보기]의 본문. 피드백을 받지 못했어도 방금 나눈 대화는 볼 수 있게 대화 화면과 같은 형식으로 보여 준다.
        /// </summary>
        public static string Transcript(string npcName, IReadOnlyList<DialogueLine> lines)
        {
            if (lines.Count == 0)
            {
                return "주고받은 대화가 없어요.";
            }

            var text = new StringBuilder();
            foreach (DialogueLine line in lines)
            {
                string speaker = line.IsNpc ? npcName : "나";
                text.AppendLine($"<b>{speaker}</b>  {line.English}");
            }

            return text.ToString().TrimEnd();
        }

        public static string Short(ConversationFeedback feedback)
        {
            var text = new StringBuilder();
            foreach (string note in feedback.Notes)
            {
                text.AppendLine(note);
            }

            if (feedback.Notes.Count > 0)
            {
                text.AppendLine();
            }

            ReferenceExpression expression = feedback.NextExpression;
            if (expression == null)
            {
                text.AppendLine(
                    "이번 대화에서는 따로 권할 표현이 없어요. 지금처럼 말해도 자연스러워요."
                );
            }
            else
            {
                text.AppendLine("다음 대화에서 활용할 표현");
                text.AppendLine($"<b>{expression.English}</b>");
                // 서버 피드백의 개선 표현은 뜻이 없어 대신 권하는 이유(내가 한 말)를 보여 준다.
                text.AppendLine(
                    string.IsNullOrEmpty(expression.Korean) ? expression.Reason : expression.Korean
                );
            }

            text.AppendLine();
            text.Append(
                feedback.IsFromServer
                    ? ServerNotice
                    : "피드백 예시 · 실제 AI 분석이나 실력 평가가 아니에요."
            );
            return text.ToString();
        }

        public static string Detail(ConversationFeedback feedback)
        {
            var text = new StringBuilder();
            for (int i = 0; i < feedback.Reviews.Count; i++)
            {
                UtteranceReview review = feedback.Reviews[i];
                text.AppendLine($"{i + 1:00} 내가 말한 문장 · <b>{review.Utterance}</b>");
                if (review.Reference == null)
                {
                    text.AppendLine(
                        string.IsNullOrEmpty(review.Comment)
                            ? "참고 표현 없음 · 지금 문장 그대로도 자연스럽게 전달돼요."
                            : review.Comment
                    );
                }
                else
                {
                    text.AppendLine(
                        $"참고 표현 · <b>{review.Reference.English}</b>{Meaning(review.Reference.Korean)}"
                    );
                    if (!string.IsNullOrEmpty(review.Reference.Reason))
                    {
                        text.AppendLine(review.Reference.Reason);
                    }
                }

                if (review.Idiom != null)
                {
                    text.AppendLine(
                        $"회화 표현 · <b>{review.Idiom.English}</b>{Meaning(review.Idiom.Korean)}"
                    );
                }

                text.AppendLine();
            }

            // AI 피드백은 문법 교정도 하므로, 오류를 판정하지 않는다는 안내는 키워드 규칙 예시에만 붙인다.
            text.Append(
                feedback.IsFromServer
                    ? ServerNotice
                    : "문법 오류나 점수를 매기지 않아요. 상황에 맞는 참고 표현만 보여 드려요."
            );
            return text.ToString();
        }

        private static string Meaning(string korean)
        {
            return string.IsNullOrEmpty(korean) ? "" : $" ({korean})";
        }
    }
}
