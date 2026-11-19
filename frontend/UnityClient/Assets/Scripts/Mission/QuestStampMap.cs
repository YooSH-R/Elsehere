using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 도장과 번호 핀의 자리(<see cref="TownMapArt"/> 그림 좌표)를 찾는다.
    /// 마을 가게는 담당 직원으로 칸을 찾고, 택시는 호텔 앞에서 기다리므로 호텔 칸에 둔다. 칸이 없는 거리(행인)는 지도에 두지 않는다.
    /// </summary>
    public static class QuestStampMap
    {
        public static List<QuestStamp> Stamps(QuestEntry quest) =>
            quest
                .Stops.Where(stop => stop.Done)
                .GroupBy(stop => stop.Place)
                .Select(place => (place.Key, centre: Centre(place.First())))
                .Where(place => place.centre.HasValue)
                .Select(place => new QuestStamp(place.Key, place.centre.Value))
                .ToList();

        /// <summary>다음에 들를 곳(하는 중이면 지금 들를 곳, 시작 전이면 처음 들를 곳)의 번호 핀. 지도에 칸이 없으면 null이다.</summary>
        public static QuestPin? Pin(QuestEntry quest)
        {
            int index = quest.NextStopIndex;
            Vector2? centre = index < 0 ? null : Centre(quest.Stops[index]);
            return centre.HasValue ? new QuestPin(index + 1, centre.Value) : null;
        }

        /// <summary>
        /// 지도 창(view)에 지도 그림(map)을 놓을 자리. focus(그림 위 자리, 왼쪽 위 0·아래로 +)가 창 가운데에 오게 하되,
        /// 그림 밖이 보이지 않게 가장자리에서 멈춘다. 창 왼쪽 위에서 그림 왼쪽 위까지(왼쪽으로 -, 위로 +)를 돌려준다.
        /// </summary>
        public static Vector2 Pan(Vector2 focus, Vector2 view, Vector2 map) =>
            new Vector2(
                Mathf.Clamp(view.x / 2 - focus.x, Mathf.Min(0, view.x - map.x), 0),
                Mathf.Clamp(focus.y - view.y / 2, 0, Mathf.Max(0, map.y - view.y))
            );

        private static Vector2? Centre(QuestStop stop) => TownMapArt.Tile(Tile(stop))?.center;

        private static string Tile(QuestStop stop) =>
            stop.Scene != "TOWN" ? stop.Scene
            : stop.NpcCode == "TAXI_DRIVER" ? "HOTEL"
            : stop.NpcCode;
    }

    public readonly struct QuestStamp
    {
        public QuestStamp(string place, Vector2 centre)
        {
            Place = place;
            Centre = centre;
        }

        public string Place { get; }
        public Vector2 Centre { get; }
    }

    public readonly struct QuestPin
    {
        public QuestPin(int number, Vector2 centre)
        {
            Number = number;
            Centre = centre;
        }

        /// <summary>몇 번째로 들르는 곳인지(1부터)</summary>
        public int Number { get; }
        public Vector2 Centre { get; }
    }
}
