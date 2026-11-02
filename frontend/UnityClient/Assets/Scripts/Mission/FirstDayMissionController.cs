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
            view.BindMap(sceneCode, player.transform);
            umbrella = player.GetComponent<UmbrellaHold>();
            view.StartRequested += StartMission;
            view.RestRequested += Rest;
            view.RetryRequested += RequestRefresh;
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
            _ = ExecuteAsync(async () =>
            {
                await api.StartAsync();
                return await api.VisitAsync(sceneCode);
            });

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
            _ = ExecuteAsync(() => api.VisitAsync(sceneCode));
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
                    view.ShowMessage("퀘스트를 불러오지 못했어요. 수첩에서 다시 시도해 주세요.");
                }
            }
            finally
            {
                busy = false;
                if (this != null)
                {
                    Render();
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
                view.Refresh(state, busy, dialogue.IsTalking);
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
