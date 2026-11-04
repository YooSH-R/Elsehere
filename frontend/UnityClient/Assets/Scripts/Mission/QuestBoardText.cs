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
