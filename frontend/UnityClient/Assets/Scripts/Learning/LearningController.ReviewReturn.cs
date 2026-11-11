using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.World;
using UnityEngine;

namespace A305.Learning
{
    public partial class LearningController
    {
        private static ReviewReturnJourney reviewReturn = new ReviewReturnJourney();
        private static ReviewCard returningReview;
        private static DialogueRecord returningRecord;
        private static string returnNotice;
        private bool restoringReviewOrigin;

        // 복습·장소 연습 방문 전 자리를 기억한다. 앞 방문에서 아직 돌아오지 않았으면 새로 시작하지 않는다.
        private bool RememberReviewOrigin()
        {
            ShopInterior occupied = ShopInterior.Occupied;
            var point = new ReviewReturnLocation(
                gameObject.scene.name,
                new Pose(player.transform.position, player.transform.rotation),
                player.CameraRig != null
                    ? player.CameraRig.ViewYawDegrees
                    : player.transform.eulerAngles.y,
                occupied != null ? occupied.SceneName : null
            );
            if (reviewReturn.TryBegin(point))
                return true;
            hudView.ShowMessage("연습을 마친 원래 자리로 돌아온 뒤 다시 연습해 주세요.");
            return false;
        }

        private void BeginReviewReturn(ReviewCard card, DialogueRecord record, string notice = null)
        {
            if (!reviewReturn.RequestReturn())
            {
                if (record != null)
                    ShowReturnedReviewResult(card, record);
                if (!string.IsNullOrEmpty(notice))
                    hudView.ShowMessage(notice);
                return;
            }
            returningReview = card;
            returningRecord = record;
            returnNotice = notice;
            ResumeReviewReturn();
        }

        private void ResumeReviewReturn()
        {
            if (!restoringReviewOrigin && reviewReturn.IsReturning)
                StartCoroutine(RestoreReviewOrigin());
        }

        private IEnumerator RestoreReviewOrigin()
        {
            restoringReviewOrigin = true;
            try
            {
                yield return null;
                ReviewReturnLocation origin = reviewReturn.Origin;
                if (origin == null)
                    yield break;
                float deadline = Time.realtimeSinceStartup + 30;
                while (
                    (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                    && Time.realtimeSinceStartup < deadline
                )
                    yield return null;
                if (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                {
                    ShowReviewReturnFailure();
                    yield break;
                }
                if (gameObject.scene.name != origin.SceneName)
                {
                    if (!SpaceTravel.TryTravel(origin.SceneName, null))
                        ShowReviewReturnFailure();
                    // 새 장면의 LearningController.Start가 정적 복귀 정보를 이어받는다.
                    yield break;
                }
                ShopInterior savedRoom = string.IsNullOrEmpty(origin.InteriorSceneName)
                    ? null
                    : FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                        .FirstOrDefault(room => room.SceneName == origin.InteriorSceneName);
                if (!string.IsNullOrEmpty(origin.InteriorSceneName) && savedRoom == null)
                {
                    ShowReviewReturnFailure();
                    yield break;
                }
                if (
                    !SpaceTravel.TryRestoreReviewPosition(
                        player,
                        origin.Pose,
                        savedRoom,
                        origin.ViewYaw
                    )
                )
                {
                    ShowReviewReturnFailure();
                    yield break;
                }
                deadline = Time.realtimeSinceStartup + 30;
                while (
                    (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                    && Time.realtimeSinceStartup < deadline
                )
                    yield return null;
                if (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                {
                    ShowReviewReturnFailure();
                    yield break;
                }
                if (!reviewReturn.CompleteReturn(gameObject.scene.name))
                    yield break;
                ReviewCard card = returningReview;
                DialogueRecord record = returningRecord;
                string notice = returnNotice;
                returningReview = null;
                returningRecord = null;
                returnNotice = null;
                if (record != null)
                    ShowReturnedReviewResult(card, record);
                else
                    UpdateMovementLock();
                if (!string.IsNullOrEmpty(notice))
                    hudView.ShowMessage(notice);
            }
            finally
            {
                restoringReviewOrigin = false;
            }
        }

        private void ShowReviewReturnFailure()
        {
            if (returningRecord != null)
                ShowReturnedReviewResult(returningReview, returningRecord);
            hudView.ShowMessage("원래 자리로 돌아가지 못했어요. 이 창을 닫으면 다시 시도해요.");
        }

        private void ShowReturnedReviewResult(ReviewCard card, DialogueRecord record)
        {
            if (ReviewView == null)
                return;
            if (card != null && record.ReviewResult != null)
                card.PracticeCount = record.ReviewResult.PracticeCount;
            ReviewView.ShowResult(card, record);
            UpdateMovementLock();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReviewReturn()
        {
            reviewReturn = new ReviewReturnJourney();
            returningReview = null;
            returningRecord = null;
            returnNotice = null;
            pendingReview = null;
            pendingPractice = null;
        }
    }
}
