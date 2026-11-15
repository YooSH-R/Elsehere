using System;
using System.Linq;
using A305.Dialogue;
using A305.World;

namespace A305.Mission
{
    /// <summary>
    /// 기다림 카드 자리에 보이는 돌발 부탁 한 줄과 길 안내(docs/npc-life-design.md 7.11). 무엇을 어디서 할지는 서버가 준 단계를 그대로 쓴다.
    /// 수락한 심부름은 가게를 목적지로 안내하고, 산 것을 받은 뒤에는 부탁한 NPC를 [이웃]의 찾는 이웃 표시로 가리킨다.
    /// </summary>
    public static class FavorCard
    {
        private const string RequesterSuffix = "의 부탁";

        /// <summary>카드에 보일 부탁인지. NPC가 꺼내기만 한 부탁은 대사로만 알리고, 학습자가 수락한 뒤부터 보인다.</summary>
        public static bool Shows(FavorUpdate favor) =>
            favor != null
            && (favor.Status == FavorUpdate.Accepted || favor.Status == FavorUpdate.Got);

        /// <param name="nowGameMinutes">1일차 0시부터 센 지금 게임 시각(분)</param>
        public static FollowUpCardRow Row(FavorUpdate favor, double nowGameMinutes)
        {
            string countdown = favor.ExpiresAtGameMinute is long deadline
                ? FollowUpCardText.Countdown(RealSecondsLeft(deadline - nowGameMinutes))
                : null;
            return new FollowUpCardRow(
                favor.TitleKo,
                NextStep(favor)?.TextKo ?? "",
                countdown,
                favor.Status == FavorUpdate.Got
            );
        }

        /// <summary>목적지 안내로 갈 가게 코드. 수락한 심부름에서 아직 사지 않았을 때만 있다.</summary>
        public static string Destination(FavorUpdate favor)
        {
            FavorStep next = Shows(favor) ? NextStep(favor) : null;
            return next != null && next.NpcCode == null ? next.PlaceCode : null;
        }

        /// <summary>찾는 이웃 표시로 가리킬 부탁한 NPC 코드. 산 것을 받아 돌아갈 때만 있다.</summary>
        public static string NpcToFind(FavorUpdate favor) =>
            favor?.Status == FavorUpdate.Got ? NextStep(favor)?.NpcCode : null;

        /// <summary>부탁한 NPC 이름(지도 핀 글자). 카드 제목의 "Jake의 부탁"에서 읽는다.</summary>
        public static string RequesterName(FavorUpdate favor)
        {
            string label = favor.RequesterLabel;
            return label.EndsWith(RequesterSuffix, StringComparison.Ordinal)
                ? label.Substring(0, label.Length - RequesterSuffix.Length)
                : label;
        }

        private static FavorStep NextStep(FavorUpdate favor) =>
            favor.Steps?.FirstOrDefault(step => step != null && !step.Done);

        // 기한은 게임 시각이라 대화하는 동안(게임 시각이 멈춤)에는 줄지 않는다. 카드에는 대화 밖에서 흐르는 실제 시간으로 보인다.
        private static int RealSecondsLeft(double gameMinutes) =>
            Math.Max(0, (int)Math.Ceiling(gameMinutes / GameClock.GameMinutesPerRealSecond));
    }
}
