using System.Collections.Generic;

namespace A305.Mission
{
    public sealed class FirstDayMissionState
    {
        // 드럭스토어에서 우산을 사는 단계(0부터 센다).
        private const int BuyUmbrellaStage = 5;

        public bool Started { get; set; }

        /// <summary>지금 퀘스트(하는 중이 아니면 다음에 시작할 퀘스트)의 코드. 퀘스트 목록의 퀘스트 Id와 같다.</summary>
        public string Quest { get; set; }
        public bool IsTownStory { get; set; }
        public const string ShowcaseQuestId = "SHOWCASE_RAINY_EVENING";
        public bool IsShowcase => Quest == ShowcaseQuestId;
        public bool ShouldOfferTownStory => Completed && !IsTownStory && !IsShowcase;
        public int Stage { get; set; }
        public bool Completed { get; set; }
        public string Scene { get; set; }
        public bool CanRest { get; set; }
        public bool Raining { get; set; }

        /// <summary>서버 예보 이름(CLEAR/CLOUDY/RAIN 등). 이 값을 보내기 전의 서버는 비워 둔다.</summary>
        public string Weather { get; set; }

        public int Balance { get; set; }

        /// <summary>가장 최근에 산 것(예: "우산"). 아직 산 것이 없으면 비어 있다.</summary>
        public string LastPurchase { get; set; }
        public string Situation { get; set; }
        public string Direction { get; set; }
        public List<FirstDayMissionStep> Steps { get; set; } = new List<FirstDayMissionStep>();

        /// <summary>지금 가야 할 장소 코드. 시작 전과 모두 끝낸 뒤에는 없다. Scene은 마지막으로 들어온 장소다.</summary>
        public string Destination
        {
            get
            {
                if (!Started || Completed)
                    return null;
                FirstDayMissionStep current = Steps.Find(step => step.Status == "CURRENT");
                if (
                    (IsTownStory || IsShowcase)
                    && current?.Scene == "TOWN"
                    && current.NpcCode != null
                )
                    return TownMapArt.Shops.ContainsKey(current.NpcCode) ? current.NpcCode : null;
                return current?.Scene;
            }
        }

        /// <summary>지금 단계에서 이야기할 NPC 코드. 시작 전과 모두 끝낸 뒤에는 없다.</summary>
        public string CurrentNpc =>
            Started && !Completed ? Steps.Find(step => step.Status == "CURRENT")?.NpcCode : null;

        /// <summary>지금 단계에서 찾아가 말해야 할 NPC 키. 시작 전과 모두 끝낸 뒤에는 없다.</summary>
        public string CurrentNpcCode =>
            Started && !Completed ? Steps.Find(step => step.Status == "CURRENT")?.NpcCode : null;

        /// <summary>
        /// 카페 의자·소파에 앉을 수 있는지. 호텔로 돌아가기 전까지 원하는 만큼 앉도록 미션 단계와 상관없이 카페에서 대화·요청 중이 아니면 앉는다.
        /// </summary>
        public bool CanSitInCafe(string sceneCode, bool busy, bool talking) =>
            ManagesSeatsIn(sceneCode) && !busy && !talking;

        /// <summary>
        /// 미션이 이 장소의 자리를 요청 중에 막고 휴식을 세는지. 휴식은 카페 좌석에서만 세므로 마을 벤치·호텔 소파는 미션과 상관없이 앉는다.
        /// </summary>
        public static bool ManagesSeatsIn(string sceneCode) => sceneCode == "CAFE";

        public static bool CanShowMiniMapIn(string sceneCode, bool insideShop) =>
            sceneCode == "TOWN" && !insideShop;

        /// <summary>이번 휴식이 미션의 '카페에서 잠깐 쉬기'로 인정되는지. 주문을 마친 뒤 처음 쉬는 한 번뿐이다.</summary>
        public bool CountsAsCafeRest =>
            !IsShowcase && Started && !Completed && Stage == 1 && CanRest;

        /// <summary>
        /// Weather를 보내기 전의 서버이거나 Unity에 없는 날씨일 때 대신 보여 줄 날씨. Raining으로 비와 맑음만 나눈다.
        /// </summary>
        public string FallbackWeather => Raining ? "RAIN" : "CLEAR";

        /// <summary>
        /// 이 장소에서 플레이어가 우산을 펴 쓰는지. 우산은 드럭스토어 단계를 마치면 생기고, 비 오는 마을에서만 편다.
        /// 예보 이름을 보내기 전의 서버는 Raining으로 비를 판단한다.
        /// </summary>
        /// <param name="insideShop">마을 매장 실내 장면인지. 서버 장소는 마을이지만 실내다.</param>
        public bool OpensUmbrellaIn(string sceneCode, bool insideShop = false) =>
            HasUmbrella
            && sceneCode == "TOWN"
            && !insideShop
            && (
                Raining || string.Equals(Weather, "RAIN", System.StringComparison.OrdinalIgnoreCase)
            );

        /// <summary>
        /// 앞 상태에서 하던 퀘스트를 이 상태에서 끝냈으면 그 퀘스트 코드. 퀘스트를 끝내면 다음 퀘스트를 시작하기 전(시작 전·다른 퀘스트)이거나
        /// 모두 마친 상태가 된다. 처음 받은 상태와 하던 퀘스트를 계속하는 상태는 null이다.
        /// </summary>
        public string QuestFinishedSince(FirstDayMissionState before) =>
            before != null
            && before.Started
            && !before.Completed
            && before.Quest != null
            && (Completed || !Started || Quest != before.Quest)
                ? before.Quest
                : null;

        private bool HasUmbrella =>
            Started
            && (IsShowcase ? !Completed && Stage >= 1 : Completed || Stage > BuyUmbrellaStage);
    }

    public sealed class FirstDayMissionStep
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Scene { get; set; }
        public string NpcCode { get; set; }
        public string Status { get; set; }
        public List<FirstDayMissionGoal> Goals { get; set; } = new List<FirstDayMissionGoal>();
    }

    public sealed class FirstDayMissionGoal
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }

        /// <summary>이 목표를 이루려면 말해야 할 정보(예: 예약자 이름, 가방 모양). 없으면 비어 있다.</summary>
        public string Hint { get; set; }
    }
}
