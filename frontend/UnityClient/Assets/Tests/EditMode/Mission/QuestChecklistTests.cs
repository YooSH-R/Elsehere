using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드가 '호텔 직원에게 요청하세요.' 같은 요청 안내와 함께 이번 단계 목표를 보여 주는지 확인한다.
    /// NPC가 할 일을 먼저 말하지 않으므로 학습자는 이 목록을 보고 요청한다. 시안(g1·g4)처럼 목표마다 한 줄에 동그라미 체크와 이름을 두고,
    /// 끝낸 목표는 초록 체크·옅은 글자·취소선으로 보인다.
    /// </summary>
    public sealed class QuestChecklistTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string Request = "호텔 직원에게 요청하세요.";

        private GameObject instance;

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            A305.World.JourneyStatus.Shared.Clear();
        }

        [Test]
        public void CurrentGoals_AppearOneRowPerGoalUnderTheRequest()
        {
            List<Transform> rows = Refresh(HotelArrival(reservationDone: true), canRest: false);

            Assert.That(Field<TMP_Text>("direction").text, Is.EqualTo(Request));
            Assert.That(
                rows.ConvertAll(Label),
                Is.EqualTo(new[] { "예약 확인", "캐리어 맡기기", "카페 위치 묻기" })
            );
        }

        [Test]
        public void CompletedGoal_ShowsAGreenCheckAndAStrikeThrough()
        {
            List<Transform> rows = Refresh(HotelArrival(reservationDone: true), canRest: false);

            Assert.That(Check(rows[0]), Is.SameAs(Field<Sprite>("goalDone")));
            Assert.That(
                Text(rows[0]).fontStyle & FontStyles.Strikethrough,
                Is.EqualTo(FontStyles.Strikethrough)
            );
            Assert.That(Check(rows[1]), Is.SameAs(Field<Sprite>("goalOpen")));
            Assert.That(
                Text(rows[1]).fontStyle & FontStyles.Strikethrough,
                Is.EqualTo(FontStyles.Normal)
            );
            Assert.That(
                Text(rows[0]).color,
                Is.Not.EqualTo(Text(rows[1]).color),
                "끝낸 목표는 옅게 보입니다."
            );
        }

        [Test]
        public void GoalNames_UseOnlyGlyphsTheFontCanDraw()
        {
            List<Transform> rows = Refresh(HotelArrival(reservationDone: true), canRest: false);

            foreach (Transform row in rows)
            {
                TMP_Text label = Text(row);
                Assert.That(
                    label.font.HasCharacters(
                        label.text.Replace(" ", ""),
                        out uint[] missing,
                        true,
                        true
                    ),
                    Is.True,
                    label.text
                );
            }
        }

        // 카페에서 쉴 차례에는 다 끝낸 주문 목록 대신 의자에 앉으라는 안내만 보인다.
        [Test]
        public void WhileResting_ShowsOnlyTheRestHint()
        {
            Refresh(HotelArrival(reservationDone: true), canRest: true);

            Assert.That(Field<TMP_Text>("direction").text, Does.Contain("의자"));
            Assert.That(Field<RectTransform>("goals").gameObject.activeSelf, Is.False);
        }

        private List<Transform> Refresh(FirstDayMissionState state, bool canRest)
        {
            state.CanRest = canRest;
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            instance.GetComponent<FirstDayMissionView>().Refresh(state, false, false);
            var rows = new List<Transform>();
            foreach (Transform row in Field<RectTransform>("goals"))
            {
                if (row.gameObject.activeSelf)
                    rows.Add(row);
            }
            return rows;
        }

        private static TMP_Text Text(Transform row) => row.GetComponentInChildren<TMP_Text>(true);

        private static string Label(Transform row) => Text(row).text;

        private static Sprite Check(Transform row) =>
            row.GetComponentInChildren<Image>(true).sprite;

        private T Field<T>(string name)
            where T : Object =>
            (T)
                new SerializedObject(instance.GetComponent<FirstDayMissionView>())
                    .FindProperty(name)
                    .objectReferenceValue;

        private static FirstDayMissionState HotelArrival(bool reservationDone) =>
            new FirstDayMissionState
            {
                Started = true,
                Stage = 0,
                Situation = "체크인까지 시간이 남았어요.",
                Direction = Request,
                Steps = new List<FirstDayMissionStep>
                {
                    new FirstDayMissionStep
                    {
                        Title = "호텔에 도착했어요",
                        Scene = "HOTEL",
                        Status = "CURRENT",
                        Goals = new List<FirstDayMissionGoal>
                        {
                            new FirstDayMissionGoal
                            {
                                Id = "confirm_reservation",
                                Title = "예약 확인",
                                Completed = reservationDone,
                            },
                            new FirstDayMissionGoal
                            {
                                Id = "store_suitcase",
                                Title = "캐리어 맡기기",
                            },
                            new FirstDayMissionGoal { Id = "ask_cafe", Title = "카페 위치 묻기" },
                        },
                    },
                },
            };
    }
}
