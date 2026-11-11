using System.Collections.Generic;

namespace A305.Npc
{
    /// <summary>
    /// [이웃]에서 NPC를 눌렀을 때 보이는 지금 위치 한 줄. 같은 게임 시각이면 어느 장면에서 보든 같도록 일과(<see cref="NpcDayPlan"/>)로 정한다.
    /// </summary>
    public static class NpcWhereabouts
    {
        public const string Unknown = "어디 있는지 알 수 없어요";

        private const string Passerby = "PASSERBY";
        private const string Town = "TOWN";
        private const string Taxi = "TAXI";

        // 생활 데이터의 근무지 코드(NpcLifeEntry.Workplace)와 화면에 보일 이름. 서버 장소는 거리 매장을 모두 마을로 두므로 따로 둔다.
        private static readonly Dictionary<string, string> WorkplaceNames = new Dictionary<
            string,
            string
        >
        {
            { "ARCADE", "오락실" },
            { "BAKERY", "빵집" },
            { "BANK", "은행" },
            { "BARBER", "이발소" },
            { "CAFE", "모퉁이 카페" },
            { "DINER", "햄버거 가게" },
            { "FLOWER_SHOP", "꽃집" },
            { "HOME_STORE", "생활용품점" },
            { "HOTEL", "호텔" },
            { "LAUNDROMAT", "빨래방" },
            { "POLICE_STATION", "경찰서" },
            { "POST_OFFICE", "우체국" },
            { "SHOP", "드럭스토어" },
        };

        /// <param name="life">NPC의 생활 데이터. 없으면 <see cref="Unknown"/></param>
        /// <param name="minutes">1일차 0시부터 센 게임 시각(분)</param>
        /// <param name="requiredForMission">미션이 지금 그 NPC를 근무지에서 만나야 하는지(<see cref="NpcDuty.RequiresPresence"/>)</param>
        public static string Describe(NpcLifeEntry life, double minutes, bool requiredForMission)
        {
            if (life == null)
            {
                return Unknown;
            }

            NpcDayPlan plan = NpcDayPlan.At(life, minutes, requiredForMission);
            bool wanders = life.Role == Passerby || life.Workplace == Town;
            string place = WorkplaceNames.TryGetValue(life.Workplace ?? "", out string name)
                ? name
                : "일터";
            switch (plan.Phase)
            {
                case NpcDayPhase.Working:
                    if (wanders)
                        return "마을을 돌아다니고 있어요";
                    return life.Workplace == Taxi
                        ? "택시를 몰고 있어요"
                        : $"{place}에서 일하고 있어요";
                case NpcDayPhase.Leisure when plan.IsErrand:
                    return "가게에 들르는 중이에요";
                case NpcDayPhase.GoingToWork when plan.IsErrand:
                    return "가게에 들렀다 돌아오는 길이에요";
                case NpcDayPhase.GoingToWork when plan.IsShortBreak:
                case NpcDayPhase.LeavingWork when plan.IsShortBreak:
                    return wanders || life.Workplace == Taxi
                        ? "잠깐 쉬고 있어요"
                        : $"{place} 앞에서 잠깐 쉬고 있어요";
                case NpcDayPhase.GoingToWork:
                    if (wanders)
                        return "마을로 나오는 길이에요";
                    return life.Workplace == Taxi
                        ? "일하러 가는 길이에요"
                        : $"{place}{Toward(place)} 출근하는 길이에요";
                case NpcDayPhase.LeavingWork:
                case NpcDayPhase.GoingHome:
                    return wanders ? "집으로 돌아가는 길이에요" : "퇴근하는 길이에요";
                case NpcDayPhase.Leisure:
                    return "마을에서 쉬고 있어요";
                default:
                    return wanders ? "집에서 쉬고 있어요" : "숙소에서 쉬고 있어요";
            }
        }

        // 받침이 없거나 ㄹ 받침이면 "로", 그 밖의 받침이면 "으로"(빵집으로, 오락실로, 카페로)
        private static string Toward(string place)
        {
            char last = place[place.Length - 1];
            if (last < '가' || last > '힣')
            {
                return "로";
            }

            int finalConsonant = (last - '가') % 28;
            return finalConsonant == 0 || finalConsonant == 8 ? "로" : "으로";
        }
    }
}
