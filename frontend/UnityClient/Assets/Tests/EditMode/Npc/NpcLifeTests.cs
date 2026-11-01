using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC 생활 규칙. AI 하루 시뮬레이션(ai/worker/tests/test_npc_simulate.py)과 같은 규칙으로 일과·욕구·할 일을 정하는지 확인한다.
    /// </summary>
    public class NpcLifeTests
    {
        private const double Hour = 60;
        private static readonly string[] SelfCare = { "HUNGER", "ENERGY", "SOCIAL", "FUN" };

        private static readonly List<NpcScheduleBlock> StaffDay = new List<NpcScheduleBlock>
        {
            Block("08:00", "12:00", "WORK"),
            Block("12:00", "12:30", "BREAK"),
            Block("12:30", "17:00", "WORK"),
            Block("17:00", "08:00", "OFF"),
        };

        [Test]
        public void ActivityAt_FollowsTheScheduleAcrossMidnight()
        {
            NpcLifeEntry staff = Npc("CAFE", StaffDay);

            Assert.That(staff.ActivityAt(9 * Hour), Is.EqualTo("WORK"));
            Assert.That(staff.ActivityAt(12 * Hour + 10), Is.EqualTo("BREAK"));
            Assert.That(staff.ActivityAt(17 * Hour), Is.EqualTo("OFF"));
            Assert.That(staff.ActivityAt(3 * Hour), Is.EqualTo("OFF"));
            Assert.That(
                staff.ActivityAt(24 * Hour + 9 * Hour),
                Is.EqualTo("WORK"),
                "다음 날에도 같은 일과"
            );
        }

        [Test]
        public void Advance_NeedsDropAtTheNpcRate()
        {
            NpcLifeData rules = Rules(Need("HUNGER", dropPerHour: 6, start: 70));
            NpcLifeEntry npc = AllDay("TOWN", "FREE");
            npc.NeedRates["HUNGER"] = 1.5f;
            var life = new NpcLife(rules, npc, 0);

            life.Advance(Hour);

            Assert.That(life.Need("HUNGER"), Is.EqualTo(61f).Within(.001f), "70 − 6 × 1.5");
        }

        [Test]
        public void ChooseNext_PicksWhatFillsTheMostUrgentNeed()
        {
            NpcLifeData rules = Rules(
                Need("HUNGER", start: 20),
                Need("ENERGY", start: 60),
                Action("REST", "TOWN", "ENERGY", 40),
                Action("EAT", "TOWN", "HUNGER", 40)
            );
            var life = new NpcLife(rules, AllDay("TOWN", "FREE"), 0);

            Assert.That(life.ChooseNext(Anything)?.Action, Is.EqualTo("EAT"));
        }

        [Test]
        public void ChooseNext_WhileWorking_OnlyWorks()
        {
            NpcLifeData rules = Rules(
                Need("ENERGY", start: 10),
                Need("WORK", start: 50),
                Action("REST", "HOTEL", "ENERGY", 40),
                Action("SERVE_GUEST", "HOTEL", "WORK", 30, onlyRole: "STAFF")
            );
            var life = new NpcLife(rules, AllDay("HOTEL", "WORK"), 0);

            Assert.That(life.ChooseNext(Anything)?.Action, Is.EqualTo("SERVE_GUEST"));
        }

        [Test]
        public void ChooseNext_SkipsAnotherRolesWorkAnotherPlaceAndObjectsNotInTheScene()
        {
            NpcLifeData rules = Rules(
                Need("ENERGY", start: 10),
                Need("WORK", start: 10),
                Action("SERVE_GUEST", "HOTEL", "WORK", 30, onlyRole: "STAFF"),
                Action("REST", "TOWN", "ENERGY", 40, objectKey: "bench")
            );
            NpcLifeEntry passerbyInHotel = AllDay("HOTEL", "WORK");
            passerbyInHotel.Role = "PASSERBY";

            Assert.That(new NpcLife(rules, passerbyInHotel, 0).ChooseNext(Anything), Is.Null);
            Assert.That(
                new NpcLife(rules, AllDay("TOWN", "FREE"), 0).ChooseNext(key => key != "bench"),
                Is.Null,
                "장면에 그 사물이 없으면 고르지 않는다"
            );
        }

        [Test]
        public void ChooseNext_NotUrgentEnough_DoesNothing()
        {
            NpcLifeData rules = Rules(
                Need("HUNGER", start: 95),
                Action("EAT", "TOWN", "HUNGER", 40)
            );
            var life = new NpcLife(rules, AllDay("TOWN", "FREE"), 0);

            Assert.That(life.ChooseNext(Anything), Is.Null, "40 × 0.05² = 0.1 < 기준 1");
        }

        [Test]
        public void BeginUsing_ForTheActionMinutes_FillsTheNeedThenIsFreeAgain()
        {
            NpcLifeData rules = Rules(
                Need("HUNGER", start: 20),
                Action("EAT", "TOWN", "HUNGER", 40, minutes: 10)
            );
            var life = new NpcLife(rules, AllDay("TOWN", "FREE"), 0);
            life.ChooseNext(Anything);

            life.BeginUsing(5);
            life.Advance(14);
            Assert.That(life.IsUsing, Is.True);
            Assert.That(life.Need("HUNGER"), Is.EqualTo(20f));

            life.Advance(15);
            Assert.That(life.Need("HUNGER"), Is.EqualTo(60f));
            Assert.That(life.Doing, Is.Null);
            Assert.That(life.IsUsing, Is.False);
        }

        [Test]
        public void GiveUp_DoesNotFillTheNeed()
        {
            NpcLifeData rules = Rules(
                Need("HUNGER", start: 20),
                Action("EAT", "TOWN", "HUNGER", 40, minutes: 10)
            );
            var life = new NpcLife(rules, AllDay("TOWN", "FREE"), 0);
            life.ChooseNext(Anything);
            life.BeginUsing(0);

            life.GiveUp();
            life.Advance(20);

            Assert.That(life.Need("HUNGER"), Is.EqualTo(20f));
            Assert.That(life.Doing, Is.Null);
        }

        [Test]
        public void OffDuty_StaysInItsPlaceAndLooksAfterItselfLikeFreeTime()
        {
            NpcLifeData rules = Rules(
                Need("ENERGY", start: 40),
                Action("REST", "CAFE", "ENERGY", 15, objectKey: "chair")
            );
            var life = new NpcLife(rules, Npc("CAFE", StaffDay), 20 * Hour);

            Assert.That(life.Activity, Is.EqualTo("OFF"));
            Assert.That(life.ChooseNext(Anything)?.Action, Is.EqualTo("REST"));
        }

        [Test]
        public void Advance_WhenTheShiftStartsAfterOffDuty_NeedsStartOver()
        {
            // 하루 시뮬레이션은 퇴근 동안을 세지 않고 다시 나타날 때 시작값에서 센다. 여기서도 근무 시작에 시작값으로 돌아간다.
            NpcLifeData rules = Rules(Need("HUNGER", dropPerHour: 6, start: 70));
            var life = new NpcLife(rules, Npc("CAFE", StaffDay), 7 * Hour);

            life.Advance(9 * Hour);

            Assert.That(
                life.Need("HUNGER"),
                Is.EqualTo(64f).Within(.001f),
                "8시에 70으로, 그 뒤 한 시간에 6"
            );
        }

        [Test]
        public void Advance_TimeGoingBackwards_WaitsUntilItCatchesUp()
        {
            // 서버 시각에 맞추면 대화 중 앞서 흐른 만큼 거꾸로 갈 수 있다. 그 사이를 두 번 세지 않는다.
            NpcLifeData rules = Rules(Need("HUNGER", dropPerHour: 6, start: 70));
            var life = new NpcLife(rules, AllDay("TOWN", "FREE"), 0);

            life.Advance(Hour);
            life.Advance(Hour - 30);
            life.Advance(Hour + 10);

            Assert.That(
                life.Need("HUNGER"),
                Is.EqualTo(63f).Within(.001f),
                "70분어치만 줄어 70 − 7"
            );
        }

        [Test]
        public void ExportedData_HasEveryServiceNpcInItsOwnPlace()
        {
            string json = File.ReadAllText(
                Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json")
            );

            NpcLifeData data = NpcLifeData.Parse(json);

            Assert.That(data.MinScore, Is.EqualTo(1));
            Assert.That(data.FindNpc("CAFE_STAFF").Scene, Is.EqualTo("CAFE"));
            Assert.That(data.FindNpc("HOTEL_STAFF").ActivityAt(9 * Hour), Is.EqualTo("WORK"));
            Assert.That(data.FindNpc("TOWN_PASSERBY").Role, Is.EqualTo("PASSERBY"));
            Assert.That(
                data.Actions.Exists(action => action.ObjectKey == "coffee_machine"),
                Is.True
            );
            Assert.That(data.Activities["WORK"].CanFill, Is.EqualTo(new[] { "WORK" }));
        }

        private static bool Anything(string objectKey) => true;

        private static NpcLifeData Rules(params object[] parts)
        {
            var data = new NpcLifeData { MinScore = 1 };
            foreach (string need in new[] { "HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK" })
            {
                data.Needs[need] = new NpcNeedRule { DropPerHour = 0, Start = 70 };
            }

            foreach (object part in parts)
            {
                if (part is KeyValuePair<string, NpcNeedRule> need)
                    data.Needs[need.Key] = need.Value;
                else if (part is NpcActionRule action)
                    data.Actions.Add(action);
            }

            data.Activities["WORK"] = new NpcActivityRule { CanFill = new List<string> { "WORK" } };
            data.Activities["BREAK"] = new NpcActivityRule { CanFill = new List<string>(SelfCare) };
            data.Activities["FREE"] = new NpcActivityRule { CanFill = new List<string>(SelfCare) };
            return data;
        }

        private static KeyValuePair<string, NpcNeedRule> Need(
            string need,
            float dropPerHour = 0,
            float start = 70
        )
        {
            return new KeyValuePair<string, NpcNeedRule>(
                need,
                new NpcNeedRule { DropPerHour = dropPerHour, Start = start }
            );
        }

        private static NpcActionRule Action(
            string action,
            string scene,
            string need,
            float amount,
            float minutes = 10,
            string onlyRole = null,
            string objectKey = "thing"
        )
        {
            return new NpcActionRule
            {
                ObjectKey = objectKey,
                Scene = scene,
                Action = action,
                Fills = new Dictionary<string, float> { [need] = amount },
                Minutes = minutes,
                OnlyRole = onlyRole,
            };
        }

        /// <summary>하루 종일 한 활동만 하는 직원</summary>
        private static NpcLifeEntry AllDay(string scene, string activity)
        {
            return Npc(scene, new List<NpcScheduleBlock> { Block("00:00", "00:00", activity) });
        }

        private static NpcLifeEntry Npc(string scene, List<NpcScheduleBlock> schedule)
        {
            return new NpcLifeEntry
            {
                NpcId = "TEST_NPC",
                Role = "STAFF",
                Scene = scene,
                Schedule = schedule,
            };
        }

        private static NpcScheduleBlock Block(string from, string to, string activity)
        {
            return new NpcScheduleBlock
            {
                From = from,
                To = to,
                Activity = activity,
            };
        }
    }
}
