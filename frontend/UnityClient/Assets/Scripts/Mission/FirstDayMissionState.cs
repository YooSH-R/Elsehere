using System.Collections.Generic;

namespace A305.Mission
{
    public sealed class FirstDayMissionState
    {
        public bool Started { get; set; }
        public int Stage { get; set; }
        public bool Completed { get; set; }
        public string Scene { get; set; }
        public bool CanRest { get; set; }
        public bool Raining { get; set; }

        /// <summary>서버 예보 이름(CLEAR/CLOUDY/RAIN 등). 이 값을 보내기 전의 서버는 비워 둔다.</summary>
        public string Weather { get; set; }

        public int Balance { get; set; }
        public string Situation { get; set; }
        public string Direction { get; set; }
        public List<FirstDayMissionStep> Steps { get; set; } = new List<FirstDayMissionStep>();

        /// <summary>지금 가야 할 장소 코드. 시작 전과 모두 끝낸 뒤에는 없다. Scene은 마지막으로 들어온 장소다.</summary>
        public string Destination =>
            Started && !Completed ? Steps.Find(step => step.Status == "CURRENT")?.Scene : null;

        /// <summary>
        /// 카페 의자에 앉을 수 있는지. 호텔로 돌아가기 전까지 원하는 만큼 앉도록 미션 단계와 상관없이 카페에서 대화·요청 중이 아니면 앉는다.
        /// </summary>
        public bool CanSitInCafe(string sceneCode, bool busy, bool talking) =>
            sceneCode == "CAFE" && !busy && !talking;

        /// <summary>이번 휴식이 미션의 '카페에서 잠깐 쉬기'로 인정되는지. 주문을 마친 뒤 처음 쉬는 한 번뿐이다.</summary>
        public bool CountsAsCafeRest => Started && !Completed && Stage == 1 && CanRest;

        /// <summary>
        /// Weather를 보내기 전의 서버이거나 Unity에 없는 날씨일 때 대신 보여 줄 날씨. Raining으로 비와 맑음만 나눈다.
        /// </summary>
        public string FallbackWeather => Raining ? "RAIN" : "CLEAR";
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
    }
}
