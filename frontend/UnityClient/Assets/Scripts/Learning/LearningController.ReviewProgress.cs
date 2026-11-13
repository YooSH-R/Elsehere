using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace A305.Learning
{
    public partial class LearningController
    {
        private ReviewLiveProgress liveReviewProgress;

        private void InitializeReviewProgress()
        {
            if (reviewApi == null)
                return;
            liveReviewProgress = new ReviewLiveProgress(reviewApi);
            dialogue.NpcResponded += RefreshReviewProgress;
        }

        private void DisposeReviewProgress()
        {
            if (dialogue != null)
                dialogue.NpcResponded -= RefreshReviewProgress;
            liveReviewProgress?.Close();
        }

        private void RefreshReviewProgress()
        {
            if (practicingReview != null && liveReviewProgress != null)
                StartCoroutine(RefreshReviewProgressAfterOpening());
        }

        // 첫 인사가 동기적으로 끝나도 Arrive의 초기 참고 문구 뒤에 사용 표시를 붙인다.
        private IEnumerator RefreshReviewProgressAfterOpening()
        {
            yield return null;
            if (practicingReview != null && dialogue.ActiveReviewConversationId is long id)
                _ = RefreshReviewProgressAsync(practicingReview, id);
        }

        private async Task RefreshReviewProgressAsync(ReviewCard card, long conversationId)
        {
            liveReviewProgress.Begin(card, conversationId);
            dialogue.SetReviewReference(
                liveReviewProgress.Items,
                liveReviewProgress.VerifiedCount
            );
            bool loaded = await liveReviewProgress.RefreshAsync();
            if (
                this == null
                || practicingReview != card
                || dialogue.ActiveReviewConversationId != conversationId
            )
                return;
            if (loaded)
                dialogue.SetReviewReference(
                    liveReviewProgress.Items,
                    liveReviewProgress.VerifiedCount
                );
            else if (liveReviewProgress.ErrorCode != null)
                Debug.LogWarning($"복습 표현 표시 갱신 실패: {liveReviewProgress.ErrorCode}", this);
        }
    }
}
