using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
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
        private ReviewCard requestedReviewResult;
        private NpcReviewPresence reviewPresence;
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
            ReviewView.ResultRequested += OpenReviewResult;
        }

        private void DisposeReview()
        {
            ReleaseReviewPresence();
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
                ReviewView.ResultRequested -= OpenReviewResult;
            }
        }

        private void OpenReview()
        {
            requestedReviewResult = null;
            if (reviewBook == null || dialogue.IsTalking)
                return;
            ReviewView.Show(reviewBook);
            UpdateMovementLock();
            _ = LoadReviewAsync(null);
        }

        private void CloseReview()
        {
            requestedReviewResult = null;
            ReviewView.Hide();
            UpdateMovementLock();
            ResumeReviewReturn();
        }

        private void FilterReview(string scene)
        {
            requestedReviewResult = null;
            _ = LoadReviewAsync(scene);
        }

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

        private void DeleteReview(ReviewCard card)
        {
            requestedReviewResult = null;
            _ = DeleteReviewAsync(card);
        }

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
            requestedReviewResult = null;
            // 장소 이동과 대화 시작은 같은 작업에서 연결한다.
            StartReviewVisit(card);
        }

        private void StartReviewVisit(ReviewCard card)
        {
            if (dialogue.IsTalking || SpaceTravel.IsTraveling || reviewBook.IsBusy)
                return;
            if (!RememberReviewOrigin())
                return;
            pendingReview = card;
            if (!TravelForVisit(card.SceneCode, card.Npc?.NpcCode))
                pendingReview = null;
        }

        // 복습·장소 연습 방문: 열린 창을 닫고 NPC가 있는 장면으로 간다. 이미 그 장면이면 바로 도착 처리를 한다.
        // 갈 수 없으면 기억한 출발점을 지우고 false를 돌려준다.
        private bool TravelForVisit(string sceneCode, string npcCode)
        {
            string scene = VisitScene(sceneCode, npcCode);
            if (feedbackView.IsVisible)
                CloseFeedback();
            if (historyView.IsVisible)
                CloseHistory();
            if (vocabularyView.IsVisible)
                CloseVocabulary();
            if (ReviewView != null && ReviewView.IsVisible)
                CloseReview();
            if (gameObject.scene.name == scene)
            {
                ResumeVisit();
                return true;
            }
            if (SpaceTravel.TryTravel(scene, null))
                return true;
            reviewReturn.Clear();
            hudView.ShowMessage("지금은 그 장소로 이동할 수 없어요.");
            return false;
        }

        private void SaveReview() => _ = SaveReviewAsync();

        private void OpenReviewResult(ReviewCard card) => _ = OpenReviewResultAsync(card);

        private async Task OpenReviewResultAsync(ReviewCard card)
        {
            if (requestedReviewResult != null || reviewBook.IsBusy)
                return;
            requestedReviewResult = card;
            hudView.ShowMessage("지난 연습을 불러오는 중이에요.");
            try
            {
                ReviewPracticeResult result = await reviewApi.GetResultAsync(card.Id);
                if (this == null || requestedReviewResult != card || !ReviewView.IsVisible)
                    return;
                card.PracticeCount = result.PracticeCount;
                ReviewView.ShowResult(
                    card,
                    new DialogueRecord(
                        card.Npc.NpcCode,
                        card.Npc.Name,
                        Array.Empty<DialogueLine>(),
                        result.Utterances.Select(item => item.Text).ToArray(),
                        false,
                        reviewCardId: card.Id,
                        reviewResult: result
                    )
                );
            }
            catch (ApiException exception)
            {
                if (this != null && requestedReviewResult == card)
                    hudView.ShowMessage(exception.Message);
            }
            finally
            {
                if (this != null && requestedReviewResult == card)
                    requestedReviewResult = null;
            }
        }

        private async Task SaveReviewAsync()
        {
            long? conversationId = shownServerFeedback?.ConversationId;
            if (conversationId == null || reviewBook.IsBusy)
                return;
            ReviewCard saved = await reviewBook.SaveAsync(conversationId.Value);
            if (this == null)
                return;
            // 담은 뒤에는 같은 자리의 버튼이 [복습장에 추가됨]으로 바뀐다. 다른 피드백으로 바뀌었으면 그대로 둔다.
            if (saved != null && shownServerFeedback?.ConversationId == conversationId)
                feedbackView.ShowReviewSaved();
            hudView.ShowMessage(
                saved != null
                    ? "복습장에 담았어요. 메뉴에서 다시 연습할 수 있어요."
                    : reviewBook.Message
            );
        }

        // 장면에 들어오거나 같은 장면에서 방문을 시작하면, 돌아가는 중이면 복귀를 잇고 아니면 기다리는 복습·장소 연습 방문을 이어 간다.
        private void ResumeVisit()
        {
            if (reviewReturn.IsReturning)
            {
                ResumeReviewReturn();
                return;
            }
            if (pendingReview != null)
                ResumeReviewVisit();
            else if (pendingPractice != null)
                ResumePracticeVisit();
        }

        private void ResumeReviewVisit()
        {
            ReviewCard card = pendingReview;
            pendingReview = null;
            if (reviewApi == null)
            {
                BeginReviewReturn(null, null, "복습 서버 연결을 확인해 주세요.");
                return;
            }
            StartCoroutine(
                ArriveAndTalk(card.Npc?.NpcCode, "복습", npc => StartReviewTalk(card, npc))
            );
        }

        private bool StartReviewTalk(ReviewCard card, NpcIdentity npc)
        {
            practicingReview = card;
            bool started = dialogue.TryStart(
                card.Npc.NpcCode,
                npc.LookTarget,
                ConversationInitiator.User,
                reviewCardId: card.Id,
                reviewStart: id => reviewApi.StartAsync(card.Id, id)
            );
            if (!started)
            {
                practicingReview = null;
                return false;
            }
            dialogue.SetReviewReference(
                card.Targets.Select(target => new ReviewReferenceItem(
                        target.Expression,
                        target.MeaningKo,
                        false
                    ))
                    .ToArray(),
                0,
                ReviewScenarioPresentation.Briefing(card)
            );
            return true;
        }

        // 복습·장소 연습 방문의 도착. 도착 처리와 NPC 배치가 끝나면 NPC를 근무지에 붙잡아 두고, 매장 안이면 문으로 들어가
        // NPC 앞 1.8 m에 세운 뒤 대화를 연다(talk). 대화를 열지 못하면 원래 자리로 돌려보낸다. activity는 안내 문구의 '복습'·'연습'이다.
        private IEnumerator ArriveAndTalk(
            string npcCode,
            string activity,
            Func<NpcIdentity, bool> talk
        )
        {
            // 도착 처리와 NPC 배치가 끝나기 전에는 API를 호출하지 않는다.
            yield return null;
            while (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                yield return null;
            NpcIdentity npc = FindReviewNpc(npcCode);
            if (npc == null)
            {
                BeginReviewReturn(
                    null,
                    null,
                    "지금은 이 NPC가 여기에 없어요. 원래 자리로 돌아왔어요."
                );
                yield break;
            }
            bool started = false;
            ReleaseReviewPresence();
            reviewPresence = new NpcReviewPresence(npc.Code);
            try
            {
                float activationDeadline = Time.realtimeSinceStartup + 2;
                do
                {
                    yield return null;
                } while (
                    npc != null
                    && !npc.gameObject.activeInHierarchy
                    && Time.realtimeSinceStartup < activationDeadline
                );
                if (dialogue.IsTalking)
                {
                    hudView.ShowMessage($"진행 중인 대화를 마친 뒤 {activity}을 시작해 주세요.");
                    yield break;
                }
                if (npc == null || !npc.gameObject.activeInHierarchy)
                {
                    hudView.ShowMessage(
                        $"{activity}할 NPC를 준비하지 못했어요. 다시 시도해 주세요."
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
                started = talk(npc);
                if (!started)
                    hudView.ShowMessage(
                        $"{activity} 대화를 시작하지 못했어요. 서버 연결을 확인해 주세요."
                    );
            }
            finally
            {
                if (!started)
                {
                    ReleaseReviewPresence();
                    BeginReviewReturn(
                        null,
                        null,
                        $"{activity}을 시작하지 못해 원래 자리로 돌아왔어요."
                    );
                }
            }
        }

        // 서버는 마을 매장 직원을 마을(TOWN)에 둔다. 매장은 따로 장면이므로 그 직원이 일하는 매장 장면으로 간다.
        private static string VisitScene(string sceneCode, string npcCode)
        {
            string scene = PlaceScenes.SceneOf(sceneCode) ?? "TownScene";
            string workplace = NpcWorkplaces.SceneOf(npcCode);
            return scene == "TownScene" && PlaceScenes.IsTownShop(workplace) ? workplace : scene;
        }

        private static NpcIdentity FindReviewNpc(string npcCode)
        {
            if (string.IsNullOrWhiteSpace(npcCode))
                return null;
            foreach (
                NpcPopulation population in FindObjectsByType<NpcPopulation>(
                    FindObjectsSortMode.None
                )
            )
            {
                NpcIdentity worker = population.FindWorker(npcCode);
                if (worker != null)
                    return worker;
            }
            return FindObjectsByType<NpcIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Where(item => item.Code == npcCode)
                .OrderBy(item => item.GetComponent<NpcTownWalker>() != null)
                .FirstOrDefault();
        }

        private void ReleaseReviewPresence()
        {
            reviewPresence?.Dispose();
            reviewPresence = null;
        }

        private void ShowReviewResult(DialogueRecord record)
        {
            ReleaseReviewPresence();
            ReviewCard completed = practicingReview;
            practicingReview = null;
            BeginReviewReturn(completed, record);
        }
    }
}
