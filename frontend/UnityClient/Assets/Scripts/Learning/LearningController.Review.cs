using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
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
        private NpcReviewPresence reviewPresence;
        private bool arrivingForVisit;
        private ReviewWorkbookView ReviewView => hudView.ReviewView;

        // 복습·장소 연습 방문이 이동·도착·대화 가운데 어디든 진행 중인지.
        private bool IsVisiting =>
            pendingReview != null
            || pendingPractice != null
            || arrivingForVisit
            || practicingReview != null
            || practicing != null;

        private void InitializeReview()
        {
            if (apiSettings == null || ReviewView == null)
                return;
            reviewApi = new ReviewApi(apiSettings.CreateClient());
            reviewBook = new ReviewBook(reviewApi);
            historyView.PracticeRequested += PracticeConversation;
            feedbackView.PracticeRequested += PracticeShownFeedback;
            ReviewView.CloseRequested += CloseReview;
            ReviewView.PracticeRequested += PracticeReview;
        }

        private void DisposeReview()
        {
            ReleaseReviewPresence();
            if (hudView == null)
                return;
            if (historyView != null)
                historyView.PracticeRequested -= PracticeConversation;
            if (feedbackView != null)
                feedbackView.PracticeRequested -= PracticeShownFeedback;
            if (ReviewView != null)
            {
                ReviewView.CloseRequested -= CloseReview;
                ReviewView.PracticeRequested -= PracticeReview;
            }
        }

        private void CloseReview()
        {
            ReviewView.Hide();
            UpdateMovementLock();
            ResumeReviewReturn();
        }

        // [연습 돌아보기]의 [다시 연습]. 장소 이동과 대화 시작은 같은 작업에서 연결한다.
        private void PracticeReview(ReviewCard card) => StartReviewVisit(card);

        private void PracticeConversation(ConversationSummary conversation) =>
            _ = PracticeConversationAsync(conversation);

        // 대화 노트의 [다시 연습]. 그 대화의 복습 카드를 찾거나 만든 뒤 그 카드로 NPC를 찾아가 연습을 시작한다.
        // 연습할 표현이 모자라는 등 서버가 거절하면 목록 아래에 이유를 보인다.
        private async Task PracticeConversationAsync(ConversationSummary conversation)
        {
            if (reviewBook.IsBusy)
                return;
            historyView.ShowStatus("연습을 준비하는 중이에요…");
            ReviewCard card = await reviewBook.SaveAsync(conversation.ConversationId);
            if (this == null || !historyView.IsVisible)
                return;
            if (card == null)
            {
                // 목록 아래 한 줄은 지나치기 쉬워 알림 카드로도 이유를 보인다.
                historyView.ShowStatus(reviewBook.Message);
                hudView.ShowMessage(reviewBook.Message);
                return;
            }
            historyView.ShowStatus("");
            StartReviewVisit(card);
        }

        // 피드백 창의 [다시 연습]. 지금 보고 있는 피드백의 대화를 바로 다시 연습한다.
        private void PracticeShownFeedback()
        {
            if (shownServerFeedback?.ConversationId is long conversationId)
                _ = PracticeShownFeedbackAsync(conversationId);
        }

        private async Task PracticeShownFeedbackAsync(long conversationId)
        {
            if (reviewBook.IsBusy)
                return;
            hudView.ShowMessage("연습을 준비하는 중이에요…");
            ReviewCard card = await reviewBook.SaveAsync(conversationId);
            // 기다리는 사이 피드백을 닫았으면 뒤늦게 데려가지 않는다.
            if (this == null || !feedbackView.IsVisible)
                return;
            if (card == null)
            {
                hudView.ShowMessage(reviewBook.Message);
                return;
            }
            StartReviewVisit(card);
        }

        private void StartReviewVisit(ReviewCard card)
        {
            if (dialogue.IsTalking || SpaceTravel.IsTraveling || reviewBook.IsBusy)
            {
                // 눌렀는데 아무 일도 없는 것처럼 보이지 않게 이유를 알린다.
                hudView.ShowMessage(
                    dialogue.IsTalking
                        ? "진행 중인 대화를 마친 뒤 다시 연습해 주세요."
                        : "이동을 마친 뒤 다시 눌러 주세요."
                );
                return;
            }
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
                        false,
                        target.OriginalText
                    ))
                    .ToArray(),
                0
            );
            // 상황 설명은 대화 위 카드 대신 왼쪽 위 퀘스트 카드에 둔다.
            PracticeVisit.Shared.Begin(ReviewScenarioPresentation.QuestCard(card));
            return true;
        }

        // 복습·장소 연습 방문의 도착. NPC의 생활 동작을 끝내고 실내 응대 자리에 정렬한 뒤 일반 대화 시점을 연다(talk).
        // 대화를 열지 못하면 원래 자리로 돌려보낸다. activity는 안내 문구의 '복습'·'연습'이다.
        private IEnumerator ArriveAndTalk(
            string npcCode,
            string activity,
            Func<NpcIdentity, bool> talk
        )
        {
            // 도착 처리와 NPC 배치가 끝나기 전에는 API를 호출하지 않는다.
            arrivingForVisit = true;
            yield return null;
            while (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                yield return null;
            NpcIdentity npc = FindReviewNpc(npcCode);
            if (npc == null)
            {
                arrivingForVisit = false;
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
                // 앉기·작업으로 바뀐 위치와 방향을 기준으로 카메라를 열지 않는다.
                var body = npc.GetComponent<Passerby>();
                Vector3 standing =
                    body != null
                        ? body.PrepareForPracticeConversation()
                        : npc.transform.position + npc.transform.forward * 1.8f;
                standing = FindClearReviewPosition(npc, standing);
                Vector3 toward = npc.transform.position - standing;
                toward.y = 0;
                if (toward.sqrMagnitude > .01f)
                    npc.transform.rotation = Quaternion.LookRotation(-toward);
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
                arrivingForVisit = false;
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

        private Vector3 FindClearReviewPosition(NpcIdentity npc, Vector3 preferred)
        {
            var body = npc.GetComponent<Passerby>();
            if (player.CameraRig == null || body == null)
                return preferred;
            // 꽃다발·포장지처럼 충돌체 없는 장식도 카메라 앞을 가릴 수 있다.
            Bounds[] obstacles = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(item =>
                    item.gameObject.scene == npc.gameObject.scene
                    && item.enabled
                    && item.sharedMaterial != null
                    && item.sharedMaterial.renderQueue < 2500
                    && item.GetComponentInParent<NpcIdentity>() == null
                    && item.GetComponentInParent<PlayerMover>() == null
                )
                .Select(item => item.bounds)
                .ToArray();
            Vector3 sideways = Vector3
                .Cross(Vector3.up, preferred - npc.transform.position)
                .normalized;
            Vector3 best = preferred;
            Vector3 origin = npc.transform.position;
            Vector3 bestNpcPosition = origin;
            int fewestObstacles = int.MaxValue;
            // 카운터 옆 응대 자리에서는 집기와 계단 사이의 열린 방향도 찾는다.
            var customerSides = new[] { preferred }.Concat(
                Enumerable
                    .Range(0, 8)
                    .Select(index =>
                        origin
                        + Quaternion.Euler(0, index * 45, 0)
                            * (preferred - origin).normalized
                            * 1.8f
                    )
            );
            foreach (Vector3 customerSide in customerSides)
            {
                foreach (float offset in new[] { 0f, .8f, -.8f, 1.6f, -1.6f })
                {
                    Vector3 npcPosition = origin + sideways * offset;
                    if (offset != 0)
                    {
                        if (
                            NavMesh.SamplePosition(
                                npcPosition,
                                out NavMeshHit npcHit,
                                .6f,
                                NavMesh.AllAreas
                            )
                        )
                        {
                            if (Mathf.Abs(npcHit.position.y - origin.y) >= .5f)
                                continue;
                            npcPosition = new Vector3(
                                npcHit.position.x,
                                origin.y,
                                npcHit.position.z
                            );
                        }
                        else if (!HasReviewFloor(npcPosition))
                            continue;
                    }
                    Vector3 shift = npcPosition - origin;
                    if (
                        !body.AllowsPracticePosition(npcPosition)
                        || !HasReviewFloor(npcPosition)
                        || Physics
                            .RaycastAll(
                                origin + Vector3.up * .7f,
                                shift.normalized,
                                shift.magnitude,
                                ~0,
                                QueryTriggerInteraction.Ignore
                            )
                            .Any(hit =>
                                hit.collider.GetComponentInParent<CharacterController>() == null
                            )
                    )
                        continue;
                    Vector3 candidate = customerSide + shift;
                    if (
                        NavMesh.SamplePosition(candidate, out NavMeshHit hit, .6f, NavMesh.AllAreas)
                    )
                    {
                        if (Mathf.Abs(hit.position.y - preferred.y) >= .5f)
                            continue;
                        candidate = hit.position;
                    }
                    else if (offset != 0 && !HasReviewFloor(candidate))
                        continue;
                    if (
                        !body.AllowsPracticePosition(
                            new Vector3(candidate.x, origin.y, candidate.z)
                        ) || !HasReviewFloor(candidate)
                    )
                        continue;
                    Vector3 face = npc.LookTarget.position + shift;
                    Vector3 camera =
                        player.CameraRig.DialogueCameraPositionFrom(
                            npc.LookTarget,
                            candidate - shift
                        ) + shift;
                    Vector3 faceSide = Vector3.Cross(Vector3.up, camera - face).normalized * .18f;
                    int blocked = 0;
                    foreach (
                        Vector3 point in new[]
                        {
                            face,
                            face - Vector3.up * .28f,
                            face + Vector3.up * .2f,
                            face + faceSide,
                            face - faceSide,
                        }
                    )
                    {
                        Vector3 toCamera = camera - point;
                        var ray = new Ray(point, toCamera.normalized);
                        blocked += obstacles.Count(bounds =>
                            bounds.IntersectRay(ray, out float distance)
                            && distance < toCamera.magnitude
                        );
                    }
                    blocked *= 100;
                    // 얼굴이 보여도 카메라가 자루·진열대에 붙으면 화면 가장자리가 큰 소품으로 막힌다.
                    blocked +=
                        obstacles.Count(bounds => bounds.SqrDistance(camera) < .45f * .45f) * 10;
                    if (blocked >= fewestObstacles)
                        continue;
                    fewestObstacles = blocked;
                    best = candidate;
                    bestNpcPosition = npcPosition;
                    if (blocked == 0)
                        break;
                }
                if (fewestObstacles == 0)
                    break;
            }
            var controller = npc.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;
            npc.transform.position = bestNpcPosition;
            if (controller != null)
                controller.enabled = wasEnabled;
            return best;
        }

        private static bool HasReviewFloor(Vector3 point) =>
            Physics
                .RaycastAll(
                    point + Vector3.up * .5f,
                    Vector3.down,
                    1f,
                    ~0,
                    QueryTriggerInteraction.Ignore
                )
                .Any(hit =>
                    hit.collider.GetComponentInParent<CharacterController>() == null
                    && hit.normal.y > .7f
                    && Mathf.Abs(hit.point.y - point.y) < .5f
                );

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
