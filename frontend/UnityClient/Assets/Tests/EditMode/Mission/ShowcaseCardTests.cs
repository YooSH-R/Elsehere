using System.Collections.Generic;
using System.Linq;
using A305.Wayfinding;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 정해진 순서대로 하는 퀘스트(비 오는 저녁)의 왼쪽 위 카드는 다른 이야기 퀘스트 카드와 같은 모양이다.
    /// 단계 칩과 단계 이름, 짧은 요청 한 줄, 그 아래 상황 설명, 같은 글자의 목표 줄을 보이고 '시연'이라고 쓰지 않는다.
    /// </summary>
    public sealed class ShowcaseCardTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string Situation =
            "호텔 앞 택시에 타고 Maple Burger로 가 달라고 말해 보세요.";

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
        public void OrderedQuestCard_LooksLikeTheOtherStoryCards()
        {
            FirstDayMissionState regular = Showcase(stage: 1);
            regular.Quest = "CHECK_IN";
            Refresh(regular);
            (string font, float size) regularGoal = FirstGoalStyle();
            Object.DestroyImmediate(instance);

            Refresh(Showcase(stage: 1));

            Assert.That(Field<TMP_Text>("stepChip").text, Is.EqualTo("2 / 3"));
            Assert.That(Field<TMP_Text>("direction").text, Is.EqualTo("택시 기사에게 요청하세요."));
            TMP_Text situation = Field<TMP_Text>("situation");
            Assert.That(
                situation.gameObject.activeSelf,
                Is.True,
                "요청 줄 아래에 상황 설명을 보입니다."
            );
            Assert.That(situation.text, Is.EqualTo(Situation));
            Assert.That(
                FirstGoalStyle(),
                Is.EqualTo(regularGoal),
                "목표 줄도 다른 이야기 카드와 같은 글자로 보입니다."
            );
            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("이야기"));
            Assert.That(
                instance.GetComponentsInChildren<TMP_Text>().Select(text => text.text),
                Has.None.Contains("시연"),
                "화면에 '시연'이라고 쓰지 않습니다."
            );
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

        private (string font, float size) FirstGoalStyle()
        {
            TMP_Text goal = Field<RectTransform>("goals")
                .GetChild(0)
                .GetComponentInChildren<TMP_Text>(true);
            return (goal.font.name, goal.fontSize);
        }

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
                Situation = Situation,
                Direction = "택시 기사에게 요청하세요.",
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
