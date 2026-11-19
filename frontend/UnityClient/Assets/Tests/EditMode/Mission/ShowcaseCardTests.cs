using System.Collections.Generic;
using System.Text.RegularExpressions;
using A305.Wayfinding;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 시연 퀘스트를 하는 동안 왼쪽 위 카드와 알약이 이것이 시연이라는 것과 따라갈 순서(호텔 › 택시 › 햄버거 가게)를 보인다.
    /// 일반 이야기 카드는 그대로다.
    /// </summary>
    public sealed class ShowcaseCardTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;
        private FirstDayMissionView view;

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            JourneyStatus.Shared.Clear();
            GuideDestination.Shared.Clear();
        }

        [Test]
        public void ShowcaseCard_SaysItIsTheShowcaseAndShowsTheOrderToFollow()
        {
            Refresh(Showcase(stage: 1));

            Assert.That(
                Field<TMP_Text>("stepChip").text,
                Is.EqualTo("시연"),
                "단계 위치는 순서 줄과 진행 막대가 보이므로 칩은 시연이라는 것만 알린다."
            );
            TMP_Text route = Field<TMP_Text>("direction");
            Assert.That(route.gameObject.activeSelf, Is.True, "따라갈 순서를 보여야 합니다.");
            Assert.That(WithoutTags(route.text), Is.EqualTo("호텔 › 택시 › 햄버거 가게"));
            Assert.That(route.text, Does.Contain("<b>택시</b>"), "지금 갈 곳을 강조합니다.");
            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("시연"));
        }

        [Test]
        public void RegularStoryCard_KeepsItsStepChipAndPill()
        {
            FirstDayMissionState state = Showcase(stage: 1);
            state.Quest = "CHECK_IN";
            state.Direction = "택시 기사에게 요청하세요.";
            Refresh(state);

            Assert.That(Field<TMP_Text>("stepChip").text, Is.EqualTo("2 / 3"));
            Assert.That(Field<TMP_Text>("direction").text, Is.EqualTo("택시 기사에게 요청하세요."));
            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("이야기"));
        }

        private static string WithoutTags(string text) => Regex.Replace(text, "<[^>]+>", "");

        private void Refresh(FirstDayMissionState state)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            view.Refresh(state, false, false);
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private static FirstDayMissionState Showcase(int stage)
        {
            var state = new FirstDayMissionState
            {
                Quest = FirstDayMissionState.ShowcaseQuestId,
                Started = true,
                Stage = stage,
                Scene = "TOWN",
                Situation = "호텔 앞 택시에 타고 Maple Burger로 가 달라고 말해 보세요.",
                Direction = "호텔 앞 택시에 타고 Maple Burger로 가 달라고 말해 보세요.",
            };
            (string title, string scene, string npc)[] steps =
            {
                ("호텔에서 저녁 나들이 준비", "HOTEL", "HOTEL_STAFF"),
                ("택시로 햄버거 가게 가기", "TOWN", "TAXI_DRIVER"),
                ("햄버거와 사이드 주문", "TOWN", "DINER_STAFF"),
            };
            for (int i = 0; i < steps.Length; i++)
                state.Steps.Add(
                    new FirstDayMissionStep
                    {
                        Index = i,
                        Title = steps[i].title,
                        Scene = steps[i].scene,
                        NpcCode = steps[i].npc,
                        Status =
                            i < stage ? "DONE"
                            : i == stage ? "CURRENT"
                            : "UPCOMING",
                        Goals = new List<FirstDayMissionGoal>
                        {
                            new FirstDayMissionGoal { Title = "목표" },
                        },
                    }
                );
            return state;
        }
    }
}
