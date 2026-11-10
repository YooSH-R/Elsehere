using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.Weather;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>서버 미션 상태를 씬 진입·대화 응답·종료 때 읽는다. 완료 여부를 클라이언트에서 보내지 않는다.</summary>
    public sealed class FirstDayMissionController : MonoBehaviour
    {
        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private FirstDayMissionView view;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        [Tooltip("예보 날씨를 보여 줄 실외 장소에만 연결한다")]
        private WeatherPresenter weather;

        [SerializeField]
        private string sceneCode;

        private FirstDayMissionApi api;
        private FirstDayMissionState state;
        private bool busy;
        private bool refreshPending;
        private bool cafeRestPending;
        private bool dinerMealPending;
        private IFollowUpApi followUpApi;
        private RestSeat[] cafeSeats;
        private DinerMealRest dinerRest;
        private QuestNpcWaitSpot[] waitSpots;
        private UmbrellaHold umbrella;

        // 끝낸 퀘스트의 팡파레. 대화가 끝나면 띄운다.
        private QuestBoardState finishedBoard;
        private string finishedQuest;

        public void SetAvailable(bool available) => view.SetAvailable(available);

        private void Awake()
        {
            if (apiSettings == null || view == null || dialogue == null || player == null)
            {
                Debug.LogError("첫날 미션 API·화면·대화·플레이어 참조가 필요합니다.", this);
                enabled = false;
                return;
            }
            api = new FirstDayMissionApi(apiSettings.CreateClient());
            if (sceneCode == "TOWN")
                dinerRest = new DinerMealRest(
                    player,
                    dialogue,
                    FindAnyObjectByType<PasserbyOfferView>(),
                    ReceiveDinerMeal
                );
            cafeSeats = FirstDayMissionState.ManagesSeatsIn(sceneCode)
                ? FindObjectsByType<RestSeat>(FindObjectsSortMode.None)
                : Array.Empty<RestSeat>();
            foreach (RestSeat seat in cafeSeats)
                seat.RestCompleted += Rest;
            waitSpots = FindObjectsByType<QuestNpcWaitSpot>(FindObjectsSortMode.None);
            view.BindMap(sceneCode, player.transform);
            umbrella = player.GetComponent<UmbrellaHold>();
            followUpApi = new FollowUpApi(apiSettings.CreateClient());
            // 기다리는 카드의 X로 닫은 것을 서버에 알린다.
            var followUps = view.GetComponent<FollowUpCardView>();
            if (followUps != null)
                followUps.Bind(followUpApi);
            view.StartRequested += StartMission;
            view.RestRequested += Rest;
            view.RetryRequested += RequestRefresh;
            view.QuestBoardRequested += LoadQuestBoard;
            view.PracticeBoardRequested += LoadPracticeBoard;
            dialogue.NpcResponded += RequestRefresh;
            dialogue.Ended += OnEnded;
            dialogue.Started += OnStarted;
        }

        private void Start() => RequestRefresh();

        private void Update()
        {
            dinerRest?.Tick(Time.unscaledDeltaTime, state, busy);
            if (state?.CountsAsCafeRest != true)
                return;
            foreach (RestSeat seat in cafeSeats)
                if (seat != null && seat.IsPlayerSeated)
                    FollowUpStore.Shared.SetSeatedWaitRemaining(
                        "CAFE_STAFF",
                        seat.RemainingSeconds
                    );
        }

        private void ReceiveDinerMeal()
        {
            _ = FollowUpStore.Shared.CompleteSeatedWaitAsync(followUpApi, "DINER_STAFF");
            if (busy)
                dinerMealPending = true;
            else
                _ = ExecuteAsync(api.ReceiveDinerMealAsync);
        }

        private void OnDestroy()
        {
            dinerRest?.Dispose();
            if (cafeSeats != null)
            {
                foreach (RestSeat seat in cafeSeats)
                {
                    if (seat != null)
                        seat.RestCompleted -= Rest;
                }
            }
            if (view != null)
            {
                view.StartRequested -= StartMission;
                view.RestRequested -= Rest;
                view.RetryRequested -= RequestRefresh;
                view.QuestBoardRequested -= LoadQuestBoard;
                view.PracticeBoardRequested -= LoadPracticeBoard;
            }
            if (dialogue != null)
            {
                dialogue.NpcResponded -= RequestRefresh;
                dialogue.Ended -= OnEnded;
                dialogue.Started -= OnStarted;
            }
        }

        private void OnStarted(string npcCode) => Render();

        private void OnEnded(string npcCode)
        {
            RequestRefresh();
            ShowFinishedQuest();
        }

        private void StartMission() =>
            _ = ExecuteAsync(() =>
                api.StartCurrentAsync(sceneCode, state != null && state.IsTownStory)
            );

        private void Rest()
        {
            if (state?.CountsAsCafeRest != true)
                return;
            _ = FollowUpStore.Shared.CompleteSeatedWaitAsync(followUpApi, "CAFE_STAFF");
            if (busy)
                cafeRestPending = true;
            else
                _ = ExecuteAsync(api.RestAsync);
        }

        /// <summary>서버에서 지금 퀘스트 진행을 다시 읽는다. 튜토리얼 저장처럼 이 화면 밖에서 퀘스트가 시작됐을 때도 부른다.</summary>
        public void RequestRefresh()
        {
            if (busy)
            {
                refreshPending = true;
                return;
            }
            _ = ExecuteAsync(() => api.LoadCurrentAsync(sceneCode));
        }

        // 수첩 대신 목록을 열 때 진행 상태도 다시 읽어, 불러오지 못했던 퀘스트를 다시 시도한다.
        private void LoadQuestBoard()
        {
            _ = LoadQuestBoardAsync();
            if (state == null)
                RequestRefresh();
        }

        private async Task LoadQuestBoardAsync()
        {
            if (api == null)
                return;
            try
            {
                QuestBoardState board = await api.GetQuestBoardAsync();
                if (this != null)
                    view.ShowQuestBoard(board);
            }
            // 서버에 닿지 못한 것처럼 예상한 실패는 안내만 하고, 그 밖의 오류만 로그로 남긴다.
            catch (ApiException exception)
            {
                if (this != null)
                    view.ShowMessage(exception.Message);
            }
            catch (Exception exception)
            {
                if (this != null)
                {
                    Debug.LogException(exception, this);
                    view.ShowMessage("퀘스트 목록을 불러오지 못했어요. 다시 열어 주세요.");
                }
            }
        }

        private void LoadPracticeBoard() => _ = LoadPracticeBoardAsync();

        // 장소 연습 목록(PRC-01)은 탭을 열 때마다 다시 읽어 방금 끝낸 상황도 보인다.
        private async Task LoadPracticeBoardAsync()
        {
            if (api == null)
                return;
            try
            {
                PracticeBoardState board = await api.GetPracticeBoardAsync();
                if (this != null)
                    view.ShowPracticeBoard(board);
            }
            catch (ApiException exception)
            {
                if (this != null)
                    view.ShowMessage(exception.Message);
            }
            catch (Exception exception)
            {
                if (this != null)
                {
                    Debug.LogException(exception, this);
                    view.ShowMessage("장소 연습 목록을 불러오지 못했어요. 다시 열어 주세요.");
                }
            }
        }

        // 퀘스트 목록에서 끝낸 퀘스트와 열린 다음 퀘스트를 읽어 팡파레를 준비한다. 대화 중이면 대화가 끝난 뒤 띄운다.
        private async Task CelebrateAsync(string quest)
        {
            try
            {
                QuestBoardState board = await api.GetQuestBoardAsync();
                if (this == null)
                    return;
                finishedBoard = board;
                finishedQuest = quest;
                if (!dialogue.IsTalking)
                    ShowFinishedQuest();
            }
            // 목록을 읽지 못하면 팡파레 없이 코인만 보인다.
            catch (Exception exception)
            {
                if (this == null)
                    return;
                if (!(exception is ApiException))
                    Debug.LogException(exception, this);
                view.ReleaseQuestReward();
            }
        }

        private void ShowFinishedQuest()
        {
            if (finishedQuest == null)
                return;
            view.ShowQuestComplete(finishedBoard, finishedQuest);
            finishedQuest = null;
            finishedBoard = null;
        }

        private async Task ExecuteAsync(Func<Task<FirstDayMissionState>> action)
        {
            if (busy || api == null)
                return;
            busy = true;
            Render();
            try
            {
                FirstDayMissionState response = await action();
                if (this == null)
                    return;
                string finished = response.QuestFinishedSince(state);
                state = response;
                // 미션이 찾아가라고 한 직원은 교대 밖이어도 자리를 지키고 응대한다. 장면을 옮겨도 남는다.
                NpcDuty.MissionNpcCode = state.CurrentNpcCode;
                if (finished != null)
                {
                    // 보상은 팡파레와 함께 더한다.
                    view.ExpectQuestReward();
                    _ = CelebrateAsync(finished);
                }
                view.ShowMessage("");
                if (weather != null)
                    weather.Show(state.Weather, state.FallbackWeather);
                if (umbrella != null)
                    umbrella.SetOpen(state.OpensUmbrellaIn(sceneCode));
            }
            catch (ApiException exception)
            {
                if (this != null)
                    view.ShowMessage(exception.Message);
            }
            catch (Exception exception)
            {
                if (this != null)
                {
                    Debug.LogException(exception, this);
                    view.ShowMessage("퀘스트를 불러오지 못했어요. 퀘스트 목록을 다시 열어 주세요.");
                }
            }
            finally
            {
                busy = false;
                if (this != null)
                {
                    Render();
                    // 퀘스트를 시작하거나 진행이 바뀌면 열려 있는 목록도 새로 받는다.
                    // 진행은 방금 읽었으므로 목록만 받는다. 진행을 다시 읽으면 실패할 때마다 끝없이 되풀이한다.
                    if (view.IsQuestBoardOpen)
                        _ = LoadQuestBoardAsync();
                    if (dinerMealPending)
                    {
                        dinerMealPending = false;
                        ReceiveDinerMeal();
                    }
                    if (cafeRestPending)
                    {
                        cafeRestPending = false;
                        Rest();
                    }
                    if (refreshPending)
                    {
                        refreshPending = false;
                        RequestRefresh();
                    }
                }
            }
        }

        private void Render()
        {
            if (state != null)
            {
                view.Refresh(state, busy, dialogue.IsTalking);
                // 지금 단계에서 만나야 하는 행인을 정한 자리에서 기다리게 한다.
                foreach (QuestNpcWaitSpot spot in waitSpots)
                {
                    if (spot != null)
                        spot.Apply(state);
                }
            }
            if (cafeSeats == null)
                return;
            bool canSit = state != null && state.CanSitInCafe(sceneCode, busy, dialogue.IsTalking);
            foreach (RestSeat seat in cafeSeats)
            {
                if (seat != null)
                    seat.SetAvailable(canSit, state?.CountsAsCafeRest == true);
            }
        }
    }
}
