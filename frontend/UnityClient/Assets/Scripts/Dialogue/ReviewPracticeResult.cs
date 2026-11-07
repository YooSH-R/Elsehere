using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>추가 모델 평가 없이 서버가 원문에서 확인한 복습 근거.</summary>
    public sealed class ReviewPracticeResult
    {
        public long ConversationId { get; set; }
        public long ReviewCardId { get; set; }
        public bool PracticeCompleted { get; set; }
        public int VerifiedExpressionCount { get; set; }
        public int PracticeCount { get; set; }
        public string CompletedAt { get; set; }
        public List<ReviewExpressionEvidence> Expressions { get; set; } =
            new List<ReviewExpressionEvidence>();
        public List<ReviewUtterance> Utterances { get; set; } = new List<ReviewUtterance>();
    }

    public sealed class ReviewExpressionEvidence
    {
        public string Expression { get; set; }
        public List<ReviewUtterance> Evidence { get; set; } = new List<ReviewUtterance>();
    }

    public sealed class ReviewUtterance
    {
        public long TurnId { get; set; }
        public string Text { get; set; }
    }
}
