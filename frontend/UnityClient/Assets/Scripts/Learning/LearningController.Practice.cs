using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Npc;
using A305.World;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 장소 연습 방문. 퀘스트 창이 고른 상황(<see cref="PracticeVisit"/>)을 받아 복습 방문과 같은 길로 그 가게 직원 앞에 세우고
    /// 장소 연습 대화(PRC-02)를 연다. NPC가 대답할 때마다 목표 진행(PRC-03)을 대화 위 참고 카드에 표시하고,
    /// 대화가 끝나면 마지막 진행을 한 번 더 읽어 원래 자리로 돌아간 뒤 "연습 완료!" 또는 남은 목표를 짧게 알린다.
    /// [연습하기]부터 대화가 끝날 때까지는 방문과 해낸 목표를 <see cref="PracticeVisit"/>에 알려 왼쪽 위 퀘스트 카드가 그 연습을 보이게 한다.
    /// 끝낸 기록은 퀘스트 창의 장소 연습 탭을 열 때마다 새로 읽는다.
    /// </summary>
    public partial class LearningController
    {
        private PracticeApi practiceApi;
        private PracticeLiveProgress livePracticeProgress;

        // 장면을 옮겨 도착한 새 장면의 LearningController가 이어받는다.
        private static PracticeVisitPlan pendingPractice;
        private PracticeVisitPlan practicing;

        private void InitializePractice()
        {
            if (apiSettings == null)
                return;
            practiceApi = new PracticeApi(apiSettings.CreateClient());
            livePracticeProgress = new PracticeLiveProgress(practiceApi);
            PracticeVisit.Shared.Requested += StartPracticeVisit;
            dialogue.NpcResponded += RefreshPracticeProgress;
        }

        private void DisposePractice()
        {
            PracticeVisit.Shared.Requested -= StartPracticeVisit;
            if (dialogue != null)
                dialogue.NpcResponded -= RefreshPracticeProgress;
            livePracticeProgress?.Close();
        }

        private void StartPracticeVisit(PracticeVisitPlan plan)
        {
            if (plan == null || dialogue.IsTalking || SpaceTravel.IsTraveling)
                return;
            if (!RememberReviewOrigin())
                return;
            pendingPractice = plan;
            PracticeVisit.Shared.Begin(plan);
            if (!TravelForVisit(plan.SceneCode, plan.NpcCode))
            {
                pendingPractice = null;
                PracticeVisit.Shared.End();
            }
        }

        private void ResumePracticeVisit()
        {
            PracticeVisitPlan plan = pendingPractice;
            pendingPractice = null;
            StartCoroutine(
                ArriveAndTalk(plan.NpcCode, "연습", npc => StartPracticeTalk(plan, npc))
            );
        }

        private bool StartPracticeTalk(PracticeVisitPlan plan, NpcIdentity npc)
        {
            practicing = plan;
            bool started = dialogue.TryStart(
                plan.NpcCode,
                npc.LookTarget,
                ConversationInitiator.User,
                practiceScenarioId: plan.ScenarioId,
                practiceStart: id => practiceApi.StartAsync(plan.ScenarioId, id)
            );
            if (!started)
            {
                practicing = null;
                return false;
            }
            livePracticeProgress.Close();
            dialogue.SetPracticeReference(
                PracticePresentation.Goals(plan),
                0,
                PracticePresentation.Briefing(plan)
            );
            return true;
        }

        private void RefreshPracticeProgress()
        {
            if (practicing != null && livePracticeProgress != null)
                StartCoroutine(RefreshPracticeProgressAfterOpening());
        }

        // 첫 인사가 동기적으로 끝나도 도착 때 붙인 참고 카드 뒤에 진행 표시를 붙인다.
        private IEnumerator RefreshPracticeProgressAfterOpening()
        {
            yield return null;
            if (practicing != null && dialogue.ActivePracticeConversationId is long id)
                _ = RefreshPracticeProgressAsync(practicing, id);
        }

        private async Task RefreshPracticeProgressAsync(PracticeVisitPlan plan, long conversationId)
        {
            livePracticeProgress.Begin(plan, conversationId);
            bool loaded = await livePracticeProgress.RefreshAsync();
            if (
                this == null
                || practicing != plan
                || dialogue.ActivePracticeConversationId != conversationId
            )
                return;
            if (loaded)
            {
                dialogue.SetPracticeReference(
                    livePracticeProgress.Items,
                    livePracticeProgress.CompletedCount,
                    PracticePresentation.Briefing(plan)
                );
                PracticeVisit.Shared.ReportProgress(
                    livePracticeProgress.Items.Select(goal => goal.Used).ToArray()
                );
            }
            else if (livePracticeProgress.ErrorCode != null)
                Debug.LogWarning(
                    $"장소 연습 목표 표시 갱신 실패: {livePracticeProgress.ErrorCode}",
                    this
                );
        }

        // 장소 연습 대화가 끝났다. 피드백 창 대신 끝냈는지만 알리고 원래 자리로 돌아간다.
        private void ShowPracticeResult()
        {
            ReleaseReviewPresence();
            PracticeVisitPlan plan = practicing;
            practicing = null;
            PracticeVisit.Shared.End();
            _ = FinishPracticeAsync(plan);
        }

        // 마지막 대답 뒤 진행을 한 번 더 읽어 끝냈는지 정한다. 읽지 못하면 마지막으로 받은 진행을 쓴다.
        private async Task FinishPracticeAsync(PracticeVisitPlan plan)
        {
            if (livePracticeProgress.ConversationId > 0)
                await livePracticeProgress.RefreshAsync();
            if (this == null)
                return;
            bool done = livePracticeProgress.IsDone;
            livePracticeProgress.Close();
            BeginReviewReturn(
                null,
                null,
                plan != null ? PracticePresentation.Result(plan, done) : null
            );
        }
    }
}
