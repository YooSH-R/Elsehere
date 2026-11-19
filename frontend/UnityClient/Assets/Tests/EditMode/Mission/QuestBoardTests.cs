using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트 목록 카드는 들르는 곳을 처음 들르는 순서대로 한 번씩 보이고(가 본 곳은 강조), 보상과 도장 수를 적는다.
    /// 도장 지도는 가 본 곳마다 도장을 한 번 찍고, 지금 갈 곳에 번호 핀을 꽂는다. 그림이 없는 거리(행인)는 지도에 찍지 않는다.
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

        // 시작하지 않은 퀘스트는 처음 들를 곳에 1번 핀을 꽂아 어디서 시작하는지 보인다.
        [Test]
        public void StampMap_PinsTheFirstStopOfAQuestNotStartedYet()
        {
            QuestEntry quest = NotStarted(CheckInQuest(doneStops: 0));

            QuestPin pin = QuestStampMap.Pin(quest).Value;

            Assert.That(pin.Number, Is.EqualTo(1));
            Assert.That(pin.Centre, Is.EqualTo(TownMapArt.Tile("HOTEL").Value.center));
        }

        // 택시는 호텔 앞에서 기다리므로 택시 기사 차례의 핀은 호텔에 꽂는다.
        [Test]
        public void StampMap_PinsTheTaxiInFrontOfTheHotel()
        {
            var quest = new QuestEntry
            {
                Stops =
                {
                    new QuestStop
                    {
                        Place = "거리",
                        Scene = "TOWN",
                        NpcCode = "TAXI_DRIVER",
                        Current = true,
                    },
                },
            };

            Assert.That(
                QuestStampMap.Pin(quest).Value.Centre,
                Is.EqualTo(TownMapArt.Tile("HOTEL").Value.center)
            );
        }

        // 도장 지도 오른쪽 위 카드: 하는 중이면 지금 할 일(몇 번째인지·장소와 사람·할 일), 시작 전이면 처음 할 일,
        // 끝냈으면 완료와 퀘스트 설명을 보인다.
        [Test]
        public void NextStepCard_SaysWhatToDoNow()
        {
            QuestEntry running = CheckInQuest(doneStops: 2);
            QuestEntry fresh = NotStarted(CheckInQuest(doneStops: 0));
            QuestEntry done = CheckInQuest(doneStops: 7);
            done.Status = QuestEntry.Done;
            done.Description = "체크인하고, 잃어버린 가방을 찾아요.";

            Assert.That(QuestBoardText.NextLabel(running), Is.EqualTo("지금 할 일 · 3 / 7"));
            Assert.That(QuestBoardText.NextTitle(running), Is.EqualTo("호텔 · Jake"));
            Assert.That(QuestBoardText.NextTodo(running), Is.EqualTo("할 일 3"));
            Assert.That(QuestBoardText.NextLabel(fresh), Is.EqualTo("시작하면 처음 할 일"));
            Assert.That(QuestBoardText.NextTitle(fresh), Is.EqualTo("호텔 · Jake"));
            Assert.That(QuestBoardText.NextTodo(fresh), Is.EqualTo("할 일 1"));
            Assert.That(QuestBoardText.NextLabel(done), Is.EqualTo("완료"));
            Assert.That(QuestBoardText.NextTitle(done), Is.EqualTo("모든 곳에서 도장을 받았어요"));
            Assert.That(QuestBoardText.NextTodo(done), Is.EqualTo(done.Description));
        }

        // 순서 목록에서 고른 곳에도 그 차례의 번호 핀을 꽂는다.
        [Test]
        public void StampMap_PinsAStopPickedFromTheList()
        {
            QuestEntry quest = CheckInQuest(doneStops: 2);

            QuestPin pin = QuestStampMap.Pin(quest, 5).Value;

            Assert.That(pin.Number, Is.EqualTo(6));
            Assert.That(pin.Centre, Is.EqualTo(TownMapArt.Tile("SHOP").Value.center));
        }

        // 지도는 퀘스트에서 들르는 건물 칸만 원래 색으로 보인다. 같은 곳은 한 번, 칸이 없는 거리는 빼고 처음 들르는 순서대로다.
        [Test]
        public void StampMap_ListsTheQuestBuildingsToColourOnce()
        {
            QuestEntry quest = CheckInQuest(doneStops: 2);

            Assert.That(
                QuestStampMap.Tiles(quest),
                Is.EqualTo(
                    new[]
                    {
                        TownMapArt.Tile("HOTEL").Value,
                        TownMapArt.Tile("CAFE").Value,
                        TownMapArt.Tile("SHOP").Value,
                    }
                )
            );
        }

        // 순서 목록에서 고른 단계의 카드 머리 글자: 지나온 곳, 지금 할 일, 다음에 할 일, 시작 전이면 처음 할 일
        [Test]
        public void StepCard_SaysWhetherThePickedStepIsDoneNowOrLater()
        {
            QuestEntry running = CheckInQuest(doneStops: 2);
            QuestEntry fresh = NotStarted(CheckInQuest(doneStops: 0));

            Assert.That(QuestBoardText.StopLabel(running, 0), Is.EqualTo("지나온 곳 · 1 / 7"));
            Assert.That(QuestBoardText.StopLabel(running, 2), Is.EqualTo("지금 할 일 · 3 / 7"));
            Assert.That(QuestBoardText.StopLabel(running, 6), Is.EqualTo("다음에 할 일 · 7 / 7"));
            Assert.That(QuestBoardText.StopLabel(fresh, 0), Is.EqualTo("시작하면 처음 할 일"));
            Assert.That(QuestBoardText.StopLabel(fresh, 1), Is.EqualTo("다음에 할 일 · 2 / 7"));
        }

        // 서버가 할 일이나 설명을 비워 보내도 카드 글자는 빈 줄이다(null이 아니다).
        [Test]
        public void NextStepCard_LeavesTheTodoEmptyWhenThereIsNone()
        {
            QuestEntry running = CheckInQuest(doneStops: 2);
            running.Stops[2].Todo = null;
            QuestEntry done = CheckInQuest(doneStops: 7);
            done.Status = QuestEntry.Done;

            Assert.That(QuestBoardText.NextTodo(running), Is.EqualTo(""));
            Assert.That(QuestBoardText.NextTodo(done), Is.EqualTo(""));
        }

        private static QuestEntry NotStarted(QuestEntry quest)
        {
            quest.Status = QuestEntry.Available;
            quest.Stops.ForEach(stop => stop.Current = false);
            return quest;
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
                        NpcName = "Jake",
                        Todo = $"할 일 {i + 1}",
                        Done = i < doneStops,
                        Current = i == doneStops,
                    }
                );
            }
            return quest;
        }
    }
}
