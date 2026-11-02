using System.Collections.Generic;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드는 단계 칩(지금 단계 / 전체), 장소, 할 일, 상황, 단계마다 한 칸인 진행 막대, 목표 체크리스트를 보인다.
    /// 카드를 누르면 여행 수첩이 열린다. 오른쪽 위에는 게임 시계와 코인을 보인다.
    /// </summary>
    public sealed class QuestCardTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private static readonly string[] StepTitles =
        {
            "호텔에 도착했어요",
            "카페에서 잠깐 쉬어요",
            "작은 가방이 없어요",
            "카페에서 가방을 찾아요",
            "갑자기 비가 내려요",
            "우산을 사요",
            "이제 체크인해요",
        };

        private GameObject instance;
        private FirstDayMissionView view;

        [TearDown]
        public void DestroyMission() => Object.DestroyImmediate(instance);

        [Test]
        public void StepChip_ShowsTheCurrentStepOutOfAll()
        {
            Refresh(Journey(current: 5));

            TMP_Text chip = Field<TMP_Text>("stepChip");
            Assert.That(chip.text, Is.EqualTo("6 / 7"));
            Assert.That(chip.transform.parent.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void StepChip_BeforeTheJourneyStarts_IsHidden()
        {
            FirstDayMissionState state = Journey(current: 0);
            state.Started = false;
            state.Steps.ForEach(step => step.Status = "UPCOMING");
            Refresh(state);

            Assert.That(
                Field<TMP_Text>("stepChip").transform.parent.gameObject.activeSelf,
                Is.False
            );
        }

        [Test]
        public void Place_ShowsTheShortTitleOfTheCurrentStep()
        {
            Refresh(Journey(current: 5));

            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo("우산 구매"));
        }

        [Test]
        public void Progress_HasOneMarkPerStepColoredByItsStatus()
        {
            Refresh(Journey(current: 5));

            var marks = new List<Image>();
            foreach (Transform mark in Field<RectTransform>("progress"))
            {
                if (mark.gameObject.activeSelf)
                    marks.Add(mark.GetComponent<Image>());
            }
            Assert.That(marks, Has.Count.EqualTo(7));
            for (int i = 0; i < 5; i++)
                Assert.That(marks[i].color, Is.EqualTo(FirstDayMissionView.DoneMark), $"{i}번째");
            Assert.That(marks[5].color, Is.EqualTo(FirstDayMissionView.CurrentMark));
            Assert.That(marks[6].color, Is.EqualTo(FirstDayMissionView.UpcomingMark));
        }

        [Test]
        public void Coins_ShowTheBalance()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 20;
            Refresh(state);

            Assert.That(Field<TMP_Text>("coins").text, Is.EqualTo("20"));
        }

        [Test]
        public void Clock_ShowsTheGameTime()
        {
            Refresh(Journey(current: 5));

            Assert.That(
                Field<TMP_Text>("clock").text,
                Is.EqualTo(GameClockText.Format(GameClock.Shared.Minutes))
            );
        }

        // 왼쪽 위 지도 버튼을 없애고 카드 자체를 눌러 수첩을 연다. 카드는 화면 왼쪽 위 모서리에 붙는다.
        [Test]
        public void QuestCard_OpensTheJournalAndSitsInTheTopLeftCorner()
        {
            Refresh(Journey(current: 5));

            Transform card = instance.transform.Find("Content/CurrentGoal");
            Assert.That(Field<Button>("journalButton").transform, Is.SameAs(card));
            Assert.That(instance.transform.Find("Content/MapButton"), Is.Null);
            view.UpdateLayout();
            Assert.That(((RectTransform)card).anchoredPosition, Is.EqualTo(new Vector2(24, -24)));
        }

        private void Refresh(FirstDayMissionState state)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            view.Refresh(state, false, false);
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private static FirstDayMissionState Journey(int current)
        {
            var state = new FirstDayMissionState
            {
                Started = true,
                Stage = current,
                Situation = "비가 쏟아져요. 우산을 사야 해요.",
                Direction = "드럭스토어 점원에게 요청하세요.",
            };
            for (int i = 0; i < StepTitles.Length; i++)
            {
                state.Steps.Add(
                    new FirstDayMissionStep
                    {
                        Index = i,
                        Title = StepTitles[i],
                        Scene = "TOWN",
                        Status =
                            i < current ? "DONE"
                            : i == current ? "CURRENT"
                            : "UPCOMING",
                    }
                );
            }
            state
                .Steps[current]
                .Goals.Add(new FirstDayMissionGoal { Id = "price", Title = "가격 묻기" });
            return state;
        }
    }
}
