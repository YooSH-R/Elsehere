using System;
using System.Collections.Generic;
using System.Text;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>전체 대화 탭에 표시할 본문과 선택 가능한 사용자 발화 수.</summary>
    public sealed class FeedbackTranscriptDocument
    {
        public FeedbackTranscriptDocument(
            string body,
            int reviewCount,
            IReadOnlyList<FeedbackTranscriptEntry> entries
        )
        {
            Body = body;
            ReviewCount = reviewCount;
            Entries = entries;
        }

        public string Body { get; }
        public int ReviewCount { get; }
        public IReadOnlyList<FeedbackTranscriptEntry> Entries { get; }
    }

    public sealed class FeedbackTranscriptEntry
    {
        public FeedbackTranscriptEntry(
            bool isNpc,
            string text,
            int? reviewIndex,
            string korean = null,
            IReadOnlyList<GlossaryWord> glossary = null,
            long turnId = 0
        )
        {
            IsNpc = isNpc;
            Text = text ?? "";
            ReviewIndex = reviewIndex;
            Korean = korean;
            Glossary = glossary;
            TurnId = turnId;
        }

        public bool IsNpc { get; }
        public string Text { get; }
        public int? ReviewIndex { get; }
        public string Korean { get; }
        public IReadOnlyList<GlossaryWord> Glossary { get; }
        public long TurnId { get; }
    }

    /// <summary>
    /// 대화 기록을 NPC·학습자 순서대로 보여 주고, 서버가 평가한 학습자 발화에는 교정 보기 링크를 붙인다.
    /// 같은 문장을 여러 번 말해도 등장 순서대로 상세 평가와 연결한다.
    /// </summary>
    public static class FeedbackTranscript
    {
        private const string NpcColor = "#7D6E5B";
        private const string LearnerColor = "#D85D3B";
        private const string HintColor = "#8A7761";

        public static FeedbackTranscriptDocument Build(
            string npcName,
            IReadOnlyList<DialogueLine> lines,
            IReadOnlyList<UtteranceReview> reviews,
            int? selectedReviewIndex
        )
        {
            if (lines == null || lines.Count == 0)
            {
                return new FeedbackTranscriptDocument(
                    "주고받은 대화가 없어요.",
                    reviews?.Count ?? 0,
                    Array.Empty<FeedbackTranscriptEntry>()
                );
            }

            IReadOnlyList<UtteranceReview> availableReviews =
                reviews ?? Array.Empty<UtteranceReview>();
            var reviewUsed = new bool[availableReviews.Count];
            var body = new StringBuilder();
            var entries = new List<FeedbackTranscriptEntry>();

            foreach (DialogueLine line in lines)
            {
                if (line == null)
                    continue;

                if (line.IsNpc)
                {
                    entries.Add(
                        new FeedbackTranscriptEntry(
                            true,
                            line.English,
                            null,
                            line.Korean,
                            line.Glossary,
                            line.TurnId
                        )
                    );
                    AppendLine(body, Escape(npcName), Escape(line.English), NpcColor, null);
                    if (!string.IsNullOrWhiteSpace(line.Korean))
                        body.Append(Escape(line.Korean)).Append("\n\n");
                    continue;
                }

                int reviewIndex = FindNextReview(line.English, availableReviews, reviewUsed);
                if (reviewIndex >= 0)
                    reviewUsed[reviewIndex] = true;
                entries.Add(
                    new FeedbackTranscriptEntry(
                        false,
                        line.English,
                        reviewIndex >= 0 ? reviewIndex : (int?)null
                    )
                );
                AppendLine(body, "나", Escape(line.English), LearnerColor, reviewIndex);
            }

            if (
                selectedReviewIndex.HasValue
                && selectedReviewIndex.Value >= 0
                && selectedReviewIndex.Value < availableReviews.Count
            )
            {
                AppendReview(body, availableReviews[selectedReviewIndex.Value]);
            }

            return new FeedbackTranscriptDocument(
                body.ToString().TrimEnd(),
                availableReviews.Count,
                entries
            );
        }

        private static void AppendLine(
            StringBuilder body,
            string speaker,
            string utterance,
            string color,
            int? reviewIndex
        )
        {
            body.Append("<color=")
                .Append(color)
                .Append("><b>")
                .Append(speaker)
                .Append("</b></color>\n");
            if (reviewIndex.HasValue)
            {
                body.Append("<link=\"review:")
                    .Append(reviewIndex.Value)
                    .Append("\"><u>")
                    .Append(utterance)
                    .Append("</u></link>\n")
                    .Append("<size=75%><color=")
                    .Append(HintColor)
                    .Append(">눌러서 피드백 보기</color></size>");
            }
            else
            {
                body.Append(utterance);
            }
            body.Append("\n\n");
        }

        private static void AppendReview(StringBuilder body, UtteranceReview review)
        {
            body.Append("<color=")
                .Append(LearnerColor)
                .Append("><b>선택한 문장 피드백</b></color>\n")
                .Append("<b>내가 한 말</b>\n")
                .Append(Escape(review.Utterance))
                .Append("\n\n");

            if (review.Reference != null)
            {
                body.Append("<b>이렇게 말해요</b>\n").Append(Escape(review.Reference.English));
                if (!string.IsNullOrWhiteSpace(review.Reference.Reason))
                    body.Append("\n").Append(Escape(review.Reference.Reason));
            }
            else
            {
                body.Append(
                    string.IsNullOrWhiteSpace(review.Comment)
                        ? "별도의 교정 없이도 자연스럽게 전달됐어요."
                        : Escape(review.Comment)
                );
            }

            if (review.Idiom != null)
            {
                body.Append("\n\n<b>실제 회화에서는</b>\n").Append(Escape(review.Idiom.English));
                if (!string.IsNullOrWhiteSpace(review.Idiom.Korean))
                    body.Append("\n").Append(Escape(review.Idiom.Korean));
            }
        }

        private static int FindNextReview(
            string utterance,
            IReadOnlyList<UtteranceReview> reviews,
            IReadOnlyList<bool> used
        )
        {
            for (int i = 0; i < reviews.Count; i++)
            {
                if (
                    !used[i]
                    && string.Equals(reviews[i].Utterance, utterance, StringComparison.Ordinal)
                )
                    return i;
            }
            return -1;
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
