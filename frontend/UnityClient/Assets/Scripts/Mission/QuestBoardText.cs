using System.Collections.Generic;
using System.Linq;

namespace A305.Mission
{
    /// <summary>퀘스트 목록 카드와 도장 지도의 글자.</summary>
    public static class QuestBoardText
    {
        /// <summary>들르는 곳을 처음 들르는 순서대로 한 번씩. 한 번이라도 가 본 곳은 Visited다.</summary>
        public static List<RoutePlace> Route(QuestEntry quest) =>
            quest
                .Stops.GroupBy(stop => stop.Place)
                .Select(place => new RoutePlace(place.Key, place.Any(stop => stop.Done)))
                .ToList();

        public static string Meta(QuestEntry quest) =>
            $"보상 {quest.Reward}코인 · 도장 {quest.Stops.Count}개";

        public static string StampCount(QuestEntry quest) =>
            $"도장 {quest.Stops.Count(stop => stop.Done)} / {quest.Stops.Count}";

        /// <summary>도장 지도 아래 칸의 제목. 예: 호텔 · Jake</summary>
        public static string StopTitle(QuestStop stop) =>
            string.IsNullOrEmpty(stop.NpcName) ? stop.Place : $"{stop.Place} · {stop.NpcName}";

        public const string CancelTitle = "퀘스트를 그만둘까요?";
        public const string CancelConfirm = "그만두기";

        /// <summary>그만두면 서버가 그 퀘스트를 처음으로 되돌린다.</summary>
        public static string CancelBody(QuestEntry quest) =>
            $"'{quest.Title}'의 진행이 처음으로 돌아가요.\n다시 시작하면 처음부터 해요.";

        /// <summary>카드의 잠긴 까닭. 문장이 줄 가운데에서 끊기지 않게 문장마다 줄을 바꾼다.</summary>
        public static string LockReason(QuestEntry quest) =>
            string.IsNullOrEmpty(quest.LockReason) ? "" : quest.LockReason.Replace(". ", ".\n");

        public const string SwitchTitle = "하던 퀘스트를 그만둘까요?";
        public const string SwitchConfirm = "그만두고 시작";

        public const string PracticeConfirm = "그만두고 연습";

        /// <summary>정해진 순서대로만 하는 퀘스트를 하는 중에는 장소 연습을 하려면 그 퀘스트를 그만둬야 한다.</summary>
        public static string PracticeBody(QuestEntry running) =>
            $"'{running.Title}' 퀘스트는 정해진 순서대로만 진행해요.\n장소 연습을 하려면 이 퀘스트를 그만둬야 해요.";

        /// <summary>다른 퀘스트를 시작하면 서버가 하던 퀘스트를 처음으로 되돌린다.</summary>
        public static string SwitchBody(QuestEntry running) =>
            $"새 퀘스트를 시작하면 하던 '{running.Title}'의 진행이 처음으로 돌아가요.";
    }

    public readonly struct RoutePlace
    {
        public RoutePlace(string place, bool visited)
        {
            Place = place;
            Visited = visited;
        }

        public string Place { get; }
        public bool Visited { get; }
    }
}
