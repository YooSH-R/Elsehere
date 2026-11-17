using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.Weather;
using A305.World;
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

        // 부탁한 NPC를 찾도록 이 컨트롤러가 켠 찾는 이웃 표시. 없으면 null이다.
        private string favorTrackedNpc;
        private RestSeat[] cafeSeats;
        private DinerMealRest dinerRest;
        private QuestNpcWaitSpot[] waitSpots;
        private UmbrellaHold umbrella;
        private TaxiRide taxi;
        private string pendingTaxiArrival;
        private bool umbrellaInTaxi;

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
            if (sceneCode == "TOWN" && !PlaceScenes.IsTownShop(gameObject.scene.name))
            {
                taxi = FindAnyObjectByType<TaxiRide>();
                if (taxi != null)
                    taxi.ArrivedAtDestination += OnTaxiArrived;
            }
            followUpApi = new FollowUpApi(apiSettings.CreateClient());
            // 기다리는 카드의 X로 닫은 것과 그만둔 돌발 부탁을 서버에 알린다.
            var followUps = view.GetComponent<FollowUpCardView>();
            if (followUps != null)
            {
                followUps.Bind(followUpApi);
                followUps.Bind(new FavorApi(apiSettings.CreateClient()));
            }
            FavorStore.Shared.Changed += OnFavorChanged;
            FavorStore.Shared.Completed += OnFavorCompleted;
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
            if (taxi != null && umbrellaInTaxi != taxi.IsPassenger)
            {
                umbrellaInTaxi = taxi.IsPassenger;
                UpdateUmbrella();
                UpdateTaxiSignal();
            }
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
            if (taxi != null)
                taxi.ArrivedAtDestination -= OnTaxiArrived;
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
            FavorStore.Shared.Changed -= OnFavorChanged;
            FavorStore.Shared.Completed -= OnFavorCompleted;
        }

        // 돌발 부탁이 생기거나 바뀌면 길 안내를 다시 정한다. 수락한 심부름은 가게를, 산 것을 받은 뒤에는 부탁한 NPC를 가리킨다.
        private void OnFavorChanged()
        {
            Render();
            FavorUpdate favor = FavorStore.Shared.Current;
            string npcToFind = FavorCard.NpcToFind(favor);
            if (npcToFind != null && !NpcTracking.Shared.IsTracking(npcToFind))
            {
                NpcTracking.Shared.Track(npcToFind, FavorCard.RequesterName(favor), null);
                favorTrackedNpc = npcToFind;
            }
            else if (npcToFind == null && favorTrackedNpc != null)
            {
                // 부탁 때문에 찾던 NPC만 거둔다. 학습자가 [이웃]에서 고른 다른 이웃은 그대로 둔다.
                if (NpcTracking.Shared.IsTracking(favorTrackedNpc))
                    NpcTracking.Shared.Clear();
                favorTrackedNpc = null;
            }
        }

        // 부탁을 들어줬다. 부탁한 NPC의 욕구를 채우고, 대화를 마치면 늘어난 코인을 그 부탁의 보상으로 보인다.
        private void OnFavorCompleted(FavorUpdate favor)
        {
            view.ExpectFavorReward(favor.RequesterLabel);
            NpcLife life = NpcLives.Shared.Find(favor.RequesterNpcCode);
            if (life == null || favor.Fills == null)
                return;
            var fills = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var fill in favor.Fills)
                fills[fill.Key] = fill.Value;
            life.FillForFavor(fills);
            if (dialogue.ActiveNpcCode == favor.RequesterNpcCode)
                dialogue.ShowNpcNeeds(life.Snapshot());
        }

        private void OnStarted(string npcCode) => Render();

        private void OnEnded(string npcCode)
        {
            RequestRefresh();
            ShowFinishedQuest();
        }

        private void StartMission() =>
            _ = ExecuteAsync(StartSelectedMissionAsync, startingQuest: true);

        private async Task<FirstDayMissionState> StartSelectedMissionAsync()
        {
            if (view.SelectedQuestId == FirstDayMissionState.ShowcaseQuestId)
            {
                await api.StartShowcaseAsync();
                return await api.LoadCurrentAsync(sceneCode);
            }
            if (state?.IsShowcase == true)
            {
                await api.StopShowcaseAsync();
                FirstDayMissionState original = await api.LoadCurrentAsync(sceneCode);
                return await api.StartCurrentAsync(sceneCode, original.IsTownStory);
            }
            return await api.StartCurrentAsync(sceneCode, state != null && state.IsTownStory);
        }

        private void OnTaxiArrived(string destination)
        {
            if (state?.IsShowcase != true || state.Stage != 1 || destination != "DINER")
                return;
            pendingTaxiArrival = destination;
            FlushTaxiArrival();
        }

        private void FlushTaxiArrival()
        {
            if (busy || pendingTaxiArrival == null)
                return;
            string destination = pendingTaxiArrival;
            pendingTaxiArrival = null;
            _ = ExecuteAsync(() => api.ConfirmShowcaseTaxiArrivalAsync(destination));
        }

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

        private async Task ExecuteAsync(
            Func<Task<FirstDayMissionState>> action,
            bool startingQuest = false
        )
        {
            if (busy || api == null)
                return;
            busy = true;
            Render();
            try
            {
                FirstDayMissionState response = await action();
                // 시연 서버에서는 시연을 시작하기 전에도 자동 설문 안내를 띄우지 않는다.
                SurveyPrompt.AutomaticInvitationsPaused = response.IsShowcase;
                if (this == null)
                    return;
                // 목록에서 다른 퀘스트를 시작한 것은 이전 퀘스트의 완료가 아니다.
                string finished = startingQuest ? null : response.QuestFinishedSince(state);
                state = response;
                NpcPopulation.UsesShowcasePopulation = state.IsShowcase;
                NpcOutings.Shared.SetClosedToVisitors(
                    FirstDayMissionState.ShowcaseRestaurant,
                    state.ClosesShowcaseRestaurantToVisitors
                );
                dialogue.UsesShowcasePresentation = state.IsShowcase && state.Started;
                if (
                    state.IsShowcase
                    && state.Stage == 2
                    && gameObject.scene.name == "MapleDinerInterior"
                )
                {
                    foreach (
                        NpcIdentity npc in FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                    )
                    {
                        if (npc.Code != "TOWN_JOHN_PARK")
                            continue;
                        var welcome = npc.GetComponent<ShowcaseRestaurantWelcome>();
                        if (welcome == null)
                            welcome = npc.gameObject.AddComponent<ShowcaseRestaurantWelcome>();
                        welcome.Configure(player, dialogue);
                    }
                }
                else
                {
                    // 주문을 마치거나 시연을 멈추면 John도 평소 손님으로 돌아간다.
                    foreach (
                        ShowcaseRestaurantWelcome welcome in FindObjectsByType<ShowcaseRestaurantWelcome>(
                            FindObjectsSortMode.None
                        )
                    )
                        welcome.End();
                }
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
                UpdateUmbrella();
                UpdateTaxiSignal();
                if (startingQuest)
                    view.ShowStartedQuest();
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
                    FlushTaxiArrival();
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

        // 시연 중 손님을 태운 택시는 본길 신호를 초록으로 붙잡아 신호에 걸리지 않는다. 시연이 아니면 신호를 지킨다.
        private void UpdateTaxiSignal()
        {
            if (taxi == null)
                return;
            // 택시는 LoopingCar를 늘 함께 둔다(RequireComponent).
            TrafficLights lights = taxi.GetComponent<LoopingCar>().Lights;
            if (lights != null)
                lights.HoldsMainGreen = state?.HoldsGreenForTaxiRide == true && taxi.IsPassenger;
        }

        private void UpdateUmbrella()
        {
            if (umbrella == null || state == null)
                return;
            bool insideShop = PlaceScenes.IsTownShop(gameObject.scene.name);
            // 시연에서는 택시 안에서 접은 우산도 편 우산도 숨긴다. 시연이 아니면 원래대로 둔다.
            bool hiddenInTaxi = state.IsShowcase && taxi != null && taxi.IsPassenger;
            if (state.CarriesFoldedUmbrellaIn(sceneCode, insideShop))
                umbrella.SetFolded(!hiddenInTaxi);
            else
                umbrella.SetOpen(!hiddenInTaxi && state.OpensUmbrellaIn(sceneCode, insideShop));
        }

        private void Render()
        {
            if (taxi != null)
            {
                if (
                    state?.IsShowcase == true
                    && state.Started
                    && !state.Completed
                    && state.Stage == 1
                )
                    taxi.PrepareShowcasePickup();
                else
                    taxi.ReleaseShowcasePickup();
            }
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
