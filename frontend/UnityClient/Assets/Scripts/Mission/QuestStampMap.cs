using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 도장 지도에 얹을 것의 자리(<see cref="TownMapArt"/> 그림 좌표). 가 본 곳마다 도장을 한 번, 지금 갈 곳에 번호 핀을 두고,
    /// 지나온 곳과 지금 갈 곳을 차례로 잇는다. 마을 가게는 담당 직원으로 칸을 찾고, 칸이 없는 거리(행인)는 지도에 두지 않는다.
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

        public static QuestPin? Pin(QuestEntry quest)
        {
            int index = quest.Stops.FindIndex(stop => stop.Current);
            Vector2? centre = index < 0 ? null : Centre(quest.Stops[index]);
            return centre.HasValue ? new QuestPin(index + 1, centre.Value) : null;
        }

        public static List<Vector2> Path(QuestEntry quest)
        {
            var points = new List<Vector2>();
            foreach (QuestStop stop in quest.Stops.Where(stop => stop.Done || stop.Current))
            {
                Vector2? centre = Centre(stop);
                if (centre.HasValue && (points.Count == 0 || points[^1] != centre.Value))
                    points.Add(centre.Value);
            }
            return points;
        }

        private static Vector2? Centre(QuestStop stop) =>
            TownMapArt.Tile(stop.Scene == "TOWN" ? stop.NpcCode : stop.Scene)?.center;
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
