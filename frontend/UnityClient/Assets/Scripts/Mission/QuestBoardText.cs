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

        /// <summary>순서 목록에서 고른 단계의 카드 머리 글자. 예: 지나온 곳 · 1 / 7, 지금 할 일 · 3 / 7, 다음에 할 일 · 7 / 7, 시작하면 처음 할 일</summary>
        public static string StopLabel(QuestEntry quest, int index)
        {
            string order = $"{index + 1} / {quest.Stops.Count}";
            if (quest.Stops[index].Done)
                return $"지나온 곳 · {order}";
            if (index != quest.NextStopIndex)
                return $"다음에 할 일 · {order}";
            return quest.Status == QuestEntry.Available
                ? "시작하면 처음 할 일"
                : $"지금 할 일 · {order}";
        }

        /// <summary>도장 지도 오른쪽 위 카드의 머리 글자. 예: 지금 할 일 · 3 / 7, 시작하면 처음 할 일, 완료</summary>
        public static string NextLabel(QuestEntry quest)
        {
            int next = quest.NextStopIndex;
            if (next < 0)
                return quest.Status == QuestEntry.Done ? "완료" : "";
            return StopLabel(quest, next);
        }

        /// <summary>카드 제목: 다음에 들를 곳(장소 · 사람). 끝냈으면 완료 안내, 갈 곳이 없으면 퀘스트 이름이다.</summary>
        public static string NextTitle(QuestEntry quest)
        {
            int next = quest.NextStopIndex;
            if (next >= 0)
                return StopTitle(quest.Stops[next]);
            return quest.Status == QuestEntry.Done ? "모든 곳에서 도장을 받았어요" : quest.Title;
        }

        /// <summary>카드 설명: 다음에 들를 곳에서 할 일. 갈 곳이 없으면 퀘스트 설명이고, 둘 다 없으면 빈 글자다.</summary>
        public static string NextTodo(QuestEntry quest)
        {
            int next = quest.NextStopIndex;
            return (next >= 0 ? quest.Stops[next].Todo : quest.Description) ?? "";
        }

        public const string CancelTitle = "퀘스트를 그만둘까요?";
        public const string CancelConfirm = "그만두기";

        /// <summary>그만두면 서버가 그 퀘스트를 처음으로 되돌린다.</summary>
        public static string CancelBody(QuestEntry quest) =>
            $"'{quest.Title}'의 진행이 처음으로 돌아가요.\n다시 시작하면 처음부터 해요.";

        /// <summary>
        /// 카드의 잠긴 까닭. 줄 가운데에서 어색하게 끊기지 않게 따옴표로 묶은 퀘스트 이름을 한 줄에 두고, 그 뒤와 문장마다 줄을 바꾼다.
        /// 예: '호텔 체크인과 잃어버린 가방' ⏎ 퀘스트를 마치면 열려요.
        /// </summary>
        public static string LockReason(QuestEntry quest) =>
            string.IsNullOrEmpty(quest.LockReason)
                ? ""
                : quest.LockReason.Replace("' ", "'\n").Replace(". ", ".\n");

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
