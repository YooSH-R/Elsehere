using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Npc;
using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Learning
{
    public partial class LearningController
    {
        private ReviewBook reviewBook;
        private ReviewApi reviewApi;
        private static ReviewCard pendingReview;
        private ReviewCard practicingReview;
        private ReviewWorkbookView ReviewView => hudView.ReviewView;

        private void InitializeReview()
        {
            if (apiSettings == null || ReviewView == null)
                return;
            reviewApi = new ReviewApi(apiSettings.CreateClient());
            reviewBook = new ReviewBook(reviewApi);
            hudView.ReviewRequested += OpenReview;
            feedbackView.ReviewSaveRequested += SaveReview;
            ReviewView.CloseRequested += CloseReview;
            ReviewView.FilterRequested += FilterReview;
            ReviewView.MoreRequested += MoreReview;
            ReviewView.DeleteRequested += DeleteReview;
            ReviewView.PracticeRequested += PracticeReview;
        }

        private void DisposeReview()
        {
            if (hudView == null)
                return;
            hudView.ReviewRequested -= OpenReview;
            if (feedbackView != null)
                feedbackView.ReviewSaveRequested -= SaveReview;
            if (ReviewView != null)
            {
                ReviewView.CloseRequested -= CloseReview;
                ReviewView.FilterRequested -= FilterReview;
                ReviewView.MoreRequested -= MoreReview;
                ReviewView.DeleteRequested -= DeleteReview;
                ReviewView.PracticeRequested -= PracticeReview;
            }
        }

        private void OpenReview()
        {
            if (reviewBook == null || dialogue.IsTalking)
                return;
            ReviewView.Show(reviewBook);
            UpdateMovementLock();
            _ = LoadReviewAsync(null);
        }

        private void CloseReview()
        {
            ReviewView.Hide();
            UpdateMovementLock();
        }

        private void FilterReview(string scene) => _ = LoadReviewAsync(scene);

        private async Task LoadReviewAsync(string scene)
        {
            Task<bool> request = reviewBook.LoadAsync(scene);
            ReviewView.Refresh();
            await request;
            if (this != null && ReviewView.IsVisible)
                ReviewView.Refresh();
        }

        private void MoreReview() => _ = MoreReviewAsync();

        private async Task MoreReviewAsync()
        {
            Task<bool> request = reviewBook.LoadMoreAsync();
            ReviewView.Refresh();
            await request;
            if (this != null && ReviewView.IsVisible)
                ReviewView.Refresh();
        }

        private void DeleteReview(ReviewCard card) => _ = DeleteReviewAsync(card);

        private async Task DeleteReviewAsync(ReviewCard card)
        {
            Task<bool> request = reviewBook.DeleteAsync(card.Id);
            ReviewView.Refresh();
            await request;
            if (this != null && ReviewView.IsVisible)
                ReviewView.Refresh();
        }

        private void PracticeReview(ReviewCard card)
        {
            // 장소 이동과 대화 시작은 같은 작업에서 연결한다.
            StartReviewVisit(card);
        }

        private void StartReviewVisit(ReviewCard card)
        {
            if (dialogue.IsTalking || SpaceTravel.IsTraveling || reviewBook.IsBusy)
                return;
            string scene = PlaceScenes.SceneOf(card.SceneCode) ?? "TownScene";
            pendingReview = card;
            if (feedbackView.IsVisible)
                CloseFeedback();
            if (historyView.IsVisible)
                CloseHistory();
            if (vocabularyView.IsVisible)
                CloseVocabulary();
            CloseReview();
            if (gameObject.scene.name == scene)
                ResumeReviewVisit();
            else if (!SpaceTravel.TryTravel(scene, null))
            {
                pendingReview = null;
                hudView.ShowMessage("지금은 그 장소로 이동할 수 없어요.");
            }
        }

        private void SaveReview() => _ = SaveReviewAsync();

        private async Task SaveReviewAsync()
        {
            long? conversationId = shownServerFeedback?.ConversationId;
            if (conversationId == null || reviewBook.IsBusy)
                return;
            ReviewCard saved = await reviewBook.SaveAsync(conversationId.Value);
            if (this == null)
                return;
            hudView.ShowMessage(
                saved != null
                    ? "복습장에 담았어요. 메뉴에서 다시 연습할 수 있어요."
                    : reviewBook.Message
            );
        }

        private void ResumeReviewVisit()
        {
            if (pendingReview == null)
                return;
            ReviewCard card = pendingReview;
            pendingReview = null;
            if (reviewApi == null)
            {
                hudView.ShowMessage("복습 서버 연결을 확인해 주세요.");
                return;
            }
            StartCoroutine(ArriveForReview(card));
        }

        private IEnumerator ArriveForReview(ReviewCard card)
        {
            // 도착 처리와 NPC 배치가 끝나기 전에는 API를 호출하지 않는다.
            yield return null;
            while (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                yield return null;
            NpcIdentity npc = FindObjectsByType<NpcIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .FirstOrDefault(item => item.Code == card.Npc?.NpcCode);
            if (npc == null)
            {
                hudView.ShowMessage("지금은 이 NPC가 여기에 없어요. 잠시 뒤 다시 시도해 주세요.");
                yield break;
            }
            ShopInterior room = FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.ContainsStaff(npc.transform));
            if (room != null && !room.IsLoaded)
            {
                ShopDoorway door = room.GetComponentsInChildren<ShopDoorway>()
                    .FirstOrDefault(item => item.Room == room);
                if (door == null || !door.TryEnter(player))
                {
                    hudView.ShowMessage(
                        "지금은 매장에 들어갈 수 없어요. 영업시간을 확인해 주세요."
                    );
                    yield break;
                }
                while (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                    yield return null;
            }
            if (!npc.gameObject.activeInHierarchy || dialogue.IsTalking)
            {
                hudView.ShowMessage(
                    "지금은 이 NPC와 연습할 수 없어요. 잠시 뒤 다시 시도해 주세요."
                );
                yield break;
            }
            Vector3 standing = npc.transform.position + npc.transform.forward * 1.8f;
            if (NavMesh.SamplePosition(standing, out NavMeshHit hit, 2, NavMesh.AllAreas))
                standing = hit.position;
            Vector3 toward = npc.transform.position - standing;
            toward.y = 0;
            player.Teleport(
                standing,
                toward.sqrMagnitude > .01f
                    ? Quaternion.LookRotation(toward)
                    : player.transform.rotation
            );
            practicingReview = card;
            if (
                !dialogue.TryStart(
                    card.Npc.NpcCode,
                    npc.LookTarget,
                    ConversationInitiator.User,
                    reviewCardId: card.Id,
                    reviewStart: id => reviewApi.StartAsync(card.Id, id)
                )
            )
            {
                practicingReview = null;
                hudView.ShowMessage("복습 대화를 시작하지 못했어요. 서버 연결을 확인해 주세요.");
            }
            else
                dialogue.SetReviewReference(
                    string.Join(
                        "\n\n",
                        card.Targets.Select(target =>
                            "<b>"
                            + ReviewWorkbookRow.Escape(target.Expression)
                            + "</b>\n<size=85%>"
                            + ReviewWorkbookRow.Escape(target.MeaningKo)
                            + "</size>"
                        )
                    )
                );
        }

        private void ShowReviewResult(DialogueRecord record)
        {
            ReviewCard completed = practicingReview;
            practicingReview = null;
            if (ReviewView != null)
            {
                if (completed != null && record.ReviewResult != null)
                    completed.PracticeCount = record.ReviewResult.PracticeCount;
                ReviewView.ShowResult(completed, record);
                UpdateMovementLock();
            }
        }
    }
}
