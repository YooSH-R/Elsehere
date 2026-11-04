using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트 목록 카드는 들르는 곳을 처음 들르는 순서대로 한 번씩 보이고(가 본 곳은 강조), 보상과 도장 수를 적는다.
    /// 도장 지도는 가 본 곳마다 도장을 한 번 찍고, 지금 갈 곳에 번호 핀을 꽂고, 지나온 길을 잇는다. 그림이 없는 거리(행인)는 지도에 찍지 않는다.
    /// </summary>
    public sealed class QuestBoardTests
    {
        [Test]
        public void Card_ListsEachPlaceOnceAndCountsRewardAndStamps()
        {
            QuestEntry quest = CheckInQuest(doneStops: 2);

            Assert.That(
                QuestBoardText
                    .Route(quest)
                    .Select(stop => $"{stop.Place}{(stop.Visited ? "✓" : "")}"),
                Is.EqualTo(new[] { "호텔✓", "카페✓", "거리", "드럭스토어" })
            );
            Assert.That(QuestBoardText.Meta(quest), Is.EqualTo("보상 10코인 · 도장 7개"));
            Assert.That(QuestBoardText.StampCount(quest), Is.EqualTo("도장 2 / 7"));
        }

        [Test]
        public void StampMap_StampsVisitedPlacesOnceAndPinsTheNextStop()
        {
            QuestEntry quest = CheckInQuest(doneStops: 2);

            List<QuestStamp> stamps = QuestStampMap.Stamps(quest);
            QuestPin pin = QuestStampMap.Pin(quest).Value;

            Assert.That(stamps.Select(stamp => stamp.Place), Is.EqualTo(new[] { "호텔", "카페" }));
            Assert.That(stamps[0].Centre, Is.EqualTo(TownMapArt.Tile("HOTEL").Value.center));
            Assert.That(pin.Number, Is.EqualTo(3));
            Assert.That(pin.Centre, Is.EqualTo(TownMapArt.Tile("HOTEL").Value.center));
            Assert.That(
                QuestStampMap.Path(quest),
                Is.EqualTo(
                    new[]
                    {
                        TownMapArt.Tile("HOTEL").Value.center,
                        TownMapArt.Tile("CAFE").Value.center,
                        TownMapArt.Tile("HOTEL").Value.center,
                    }
                )
            );
        }

        [Test]
        public void StampMap_PinsATownShopByItsStaff_AndSkipsTheStreet()
        {
            QuestEntry street = CheckInQuest(doneStops: 4);
            var shop = new QuestEntry
            {
                Stops =
                {
                    new QuestStop
                    {
                        Place = "꽃집",
                        Scene = "TOWN",
                        NpcCode = "FLOWER_SHOP_STAFF",
                        Current = true,
                    },
                },
            };

            Assert.That(QuestStampMap.Pin(street), Is.Null, "거리 행인은 지도에 자리가 없습니다.");
            Assert.That(
                QuestStampMap.Pin(shop).Value.Centre,
                Is.EqualTo(TownMapArt.Tile("FLOWER_SHOP_STAFF").Value.center)
            );
        }

        // 첫날 퀘스트의 일곱 곳: 호텔·카페·호텔·카페·거리·드럭스토어·호텔
        private static QuestEntry CheckInQuest(int doneStops)
        {
            (string place, string scene)[] stops =
            {
                ("호텔", "HOTEL"),
                ("카페", "CAFE"),
                ("호텔", "HOTEL"),
                ("카페", "CAFE"),
                ("거리", "TOWN"),
                ("드럭스토어", "SHOP"),
                ("호텔", "HOTEL"),
            };
            var quest = new QuestEntry { Reward = 10, Status = "IN_PROGRESS" };
            for (int i = 0; i < stops.Length; i++)
            {
                quest.Stops.Add(
                    new QuestStop
                    {
                        Place = stops[i].place,
                        Scene = stops[i].scene,
                        NpcCode = stops[i].scene == "TOWN" ? "TOWN_PASSERBY" : null,
                        Done = i < doneStops,
                        Current = i == doneStops,
                    }
                );
            }
            return quest;
        }
    }
}
