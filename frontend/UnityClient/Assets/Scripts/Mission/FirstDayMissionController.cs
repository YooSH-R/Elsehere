using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
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
        private RestSeat[] cafeSeats;
        private QuestNpcWaitSpot[] waitSpots;
        private UmbrellaHold umbrella;

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
            cafeSeats = FirstDayMissionState.ManagesSeatsIn(sceneCode)
                ? FindObjectsByType<RestSeat>(FindObjectsSortMode.None)
                : Array.Empty<RestSeat>();
            foreach (RestSeat seat in cafeSeats)
                seat.RestCompleted += Rest;
            waitSpots = FindObjectsByType<QuestNpcWaitSpot>(FindObjectsSortMode.None);
            view.BindMap(sceneCode, player.transform);
            umbrella = player.GetComponent<UmbrellaHold>();
            view.StartRequested += StartMission;
            view.RestRequested += Rest;
            view.RetryRequested += RequestRefresh;
            view.QuestBoardRequested += LoadQuestBoard;
            dialogue.NpcResponded += RequestRefresh;
            dialogue.Ended += OnEnded;
            dialogue.Started += OnStarted;
        }

        private void Start() => RequestRefresh();

        private void OnDestroy()
        {
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
            }
            if (dialogue != null)
            {
                dialogue.NpcResponded -= RequestRefresh;
                dialogue.Ended -= OnEnded;
                dialogue.Started -= OnStarted;
            }
        }

        private void OnStarted(string npcCode) => Render();

        private void OnEnded(string npcCode) => RequestRefresh();

        private void StartMission() =>
            _ = ExecuteAsync(() =>
                api.StartCurrentAsync(sceneCode, state != null && state.IsTownStory)
            );

        private void Rest()
        {
            if (state != null && state.CountsAsCafeRest && !busy)
                _ = ExecuteAsync(api.RestAsync);
        }

        private void RequestRefresh()
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
                state = response;
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
                    if (view.IsQuestBoardOpen)
                        LoadQuestBoard();
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
                    seat.SetAvailable(canSit);
            }
        }
    }
}
