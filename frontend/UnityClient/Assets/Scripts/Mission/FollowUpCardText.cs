using A305.Dialogue;
using A305.Npc;
using A305.World;

namespace A305.Mission
{
    /// <summary>기다리는 카드의 한 줄에 보일 글자. 준비 중이면 남은 시간이 있고, 다 되면 없다.</summary>
    public readonly struct FollowUpCardRow
    {
        public FollowUpCardRow(string title, string detail, string countdown, bool isReady)
        {
            Title = title;
            Detail = detail;
            Countdown = countdown;
            IsReady = isReady;
        }

        /// <summary>준비 중: "아이스 라테", 다 됨: "아이스 라테가 준비됐어요"</summary>
        public string Title { get; }

        /// <summary>준비 중: "Mia · 카페", 다 됨: "카페 Mia에게 받으러 가세요"</summary>
        public string Detail { get; }

        /// <summary>남은 시간 "0:42". 다 되었으면 null이다.</summary>
        public string Countdown { get; }

        public bool IsReady { get; }
    }

    /// <summary>
    /// 왼쪽 퀘스트 카드 아래 기다리는 카드의 글자. NPC가 준비해 건네기로 한 것마다 받을 것·누가 어디서·남은 시간을 보이고,
    /// 다 되면 받으러 가라는 알림으로 바꾼다. 장소는 마을 매장 직원이면 가게 이름, 아니면 씬 이름으로 쓴다.
    /// </summary>
    public static class FollowUpCardText
    {
        public const string Header = "받을 것";

        // 마을 매장은 모두 마을 씬이라 가게 이름이 없는 마을 NPC(행인 등)만 씬 이름으로 쓴다.
        private const string TownName = "마을";

        // 경찰서 창구의 경찰관(서버 PoliceDesk). 기다리는 것은 출동해 다녀온 결과라 받을 물건이 아니라 들을 이야기다.
        private const string PolicePrefix = "POLICE_";
        private const string PoliceStation = "경찰서";

        /// <summary>남은 시간을 분:초로 쓴다. 기다리는 시간은 몇 분 안이라 시는 쓰지 않는다.</summary>
        public static string Countdown(int seconds)
        {
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        public static FollowUpCardRow Row(WaitingFollowUp followUp, double nowSeconds)
        {
            string place = Place(followUp);
            string name = followUp.NpcName;
            if (IsPoliceCall(followUp) && PoliceDispatchOrder.IsAway(followUp.NpcCode))
                return new FollowUpCardRow(
                    followUp.ItemKo,
                    $"순찰차가 돌아오면 {place} {name}에게 들으러 가세요",
                    null,
                    false
                );
            if (!followUp.IsReady(nowSeconds))
            {
                return new FollowUpCardRow(
                    followUp.ItemKo,
                    Join(name, place),
                    Countdown(followUp.RemainingSeconds(nowSeconds)),
                    false
                );
            }

            string item = followUp.ItemKo;
            return new FollowUpCardRow(
                $"{item}{PasserbyOfferText.SubjectParticle(item)} 준비됐어요",
                IsPoliceCall(followUp)
                    ? $"{place} {name}에게 들으러 가세요"
                    : PickUpGuide(name, place),
                null,
                true
            );
        }

        private static bool IsPoliceCall(WaitingFollowUp followUp) =>
            followUp.NpcCode != null && followUp.NpcCode.StartsWith(PolicePrefix);

        private static string Place(WaitingFollowUp followUp)
        {
            if (IsPoliceCall(followUp))
            {
                return PoliceStation;
            }

            string shop = TownMapArt.PlaceName(followUp.NpcCode);
            if (shop != null)
            {
                return shop;
            }

            return followUp.SceneCode == "TOWN"
                ? TownName
                : TownMapArt.PlaceName(followUp.SceneCode);
        }

        private static string Join(string name, string place)
        {
            if (string.IsNullOrEmpty(place))
            {
                return name ?? "";
            }

            return string.IsNullOrEmpty(name) ? place : $"{name} · {place}";
        }

        // 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '에게'를 쓴다.
        private static string PickUpGuide(string name, string place)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.IsNullOrEmpty(place) ? "받으러 가세요" : $"{place}에서 받으러 가세요";
            }

            return string.IsNullOrEmpty(place)
                ? $"{name}에게 받으러 가세요"
                : $"{place} {name}에게 받으러 가세요";
        }
    }
}
