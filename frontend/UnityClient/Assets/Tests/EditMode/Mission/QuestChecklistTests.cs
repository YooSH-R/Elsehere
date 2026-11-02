using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드가 '호텔 직원에게 요청하세요.' 같은 요청 안내 아래 이번 단계 목표를 체크리스트로 보여 주는지 확인한다.
    /// NPC가 할 일을 먼저 말하지 않으므로 학습자는 이 목록을 보고 요청한다.
    /// </summary>
    public sealed class QuestChecklistTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string Request = "호텔 직원에게 요청하세요.";

        private GameObject instance;

        [TearDown]
        public void DestroyMission() => Object.DestroyImmediate(instance);

        [Test]
        public void CurrentGoals_AppearAsAChecklistUnderTheRequest()
        {
            TMP_Text direction = Refresh(HotelArrival(reservationDone: true), canRest: false);

            string plain = Plain(direction.text);
            Assert.That(plain, Does.StartWith(Request + "\n"));
            Assert.That(plain, Does.Contain("✓ 예약 확인"));
            Assert.That(plain, Does.Contain("○ 캐리어 맡기기"));
            Assert.That(plain, Does.Contain("○ 카페 위치 묻기"));
            Assert.That(plain.IndexOf("예약 확인"), Is.LessThan(plain.IndexOf("캐리어 맡기기")));
        }

        // 달성한 목표는 남은 목표와 색으로도 구분한다.
        [Test]
        public void CompletedGoal_IsMarkedGreen()
        {
            TMP_Text direction = Refresh(HotelArrival(reservationDone: true), canRest: false);

            Assert.That(direction.text, Does.Contain("<color=#699B75>✓ 예약 확인</color>"));
        }

        // 목록이 두 줄이 되어도 '캐리어 / 맡기기'처럼 한 항목이 갈라지지 않게 항목 안의 띄어쓰기는 줄바꿈하지 않는다.
        [Test]
        public void ChecklistItems_DoNotBreakInTheMiddle()
        {
            TMP_Text direction = Refresh(HotelArrival(reservationDone: false), canRest: false);

            Assert.That(direction.text, Does.Contain("캐리어 맡기기"));
            Assert.That(direction.text, Does.Contain("카페 위치 묻기"));
        }

        [Test]
        public void Checklist_UsesOnlyGlyphsTheFontCanDraw()
        {
            TMP_Text direction = Refresh(HotelArrival(reservationDone: true), canRest: false);
            string glyphs = Plain(direction.text).Replace(" ", "").Replace("\n", "");

            Assert.That(
                direction.font.HasCharacters(glyphs, out uint[] missing, true, true),
                Is.True,
                "글꼴에 없는 글자: "
                    + string.Concat(
                        System.Array.ConvertAll(missing ?? new uint[0], code => (char)code)
                    )
            );
        }

        // 카페에서 쉴 차례에는 다 끝낸 주문 목록 대신 의자에 앉으라는 안내만 보인다.
        [Test]
        public void WhileResting_ShowsOnlyTheRestHint()
        {
            TMP_Text direction = Refresh(HotelArrival(reservationDone: true), canRest: true);

            Assert.That(direction.text, Does.Not.Contain("✓").And.Not.Contain("○"));
        }

        private TMP_Text Refresh(FirstDayMissionState state, bool canRest)
        {
            state.CanRest = canRest;
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var view = instance.GetComponent<FirstDayMissionView>();
            view.Refresh(state, false, false);
            return (TMP_Text)
                new SerializedObject(view).FindProperty("direction").objectReferenceValue;
        }

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

        // 서식 태그를 빼고, 항목 안 줄바꿈 금지 공백은 보통 공백으로 읽는다.
        private static string Plain(string text) =>
            Regex.Replace(text, "<[^>]+>", "").Replace(' ', ' ');
    }
}
