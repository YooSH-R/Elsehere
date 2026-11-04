using System.Collections.Generic;
using A305.Wayfinding;
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
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            JourneyStatus.Shared.Clear();
            GuideDestination.Shared.Clear();
        }

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
        public void TownStoryPreservesFirstDayBelongings()
        {
            FirstDayMissionState state = Journey(current: 0);
            state.IsTownStory = true;
            state.Balance = 12;
            Refresh(state);

            string belongings = Field<TMP_Text>("belongings").text;
            Assert.That(belongings, Does.Contain("캐리어 · 찾음"));
            Assert.That(belongings, Does.Contain("파란 가방 · 찾음"));
            Assert.That(belongings, Does.Contain("우산 · 소지 중"));
            Assert.That(belongings, Does.Contain("12코인"));
        }

        [Test]
        public void TownStoryProgressFitsAllTwelveStepsInsideTheCard()
        {
            FirstDayMissionState state = Journey(current: 0);
            state.IsTownStory = true;
            for (int i = state.Steps.Count; i < 12; i++)
                state.Steps.Add(new FirstDayMissionStep { Index = i, Status = "UPCOMING" });
            Refresh(state);
            RectTransform progress = Field<RectTransform>("progress");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(progress);

            RectTransform last = (RectTransform)progress.GetChild(11);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                progress,
                last
            );
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(progress.rect.xMax + 20));
        }

        [Test]
        public void Place_ShowsTheShortTitleOfTheCurrentStep()
        {
            Refresh(Journey(current: 5));

            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo("우산 구매"));
        }

        [Test]
        public void Progress_HasOneMarkPerStepDrawnByItsStatus()
        {
            Refresh(Journey(current: 5));

            var marks = new List<Image>();
            foreach (Transform mark in Field<RectTransform>("progress"))
            {
                if (mark.gameObject.activeSelf)
                    marks.Add(mark.GetComponent<Image>());
            }
            Assert.That(marks, Has.Count.EqualTo(7));
            // 진행 칸은 시안 그림(끝냄 초록·지금 코랄·남음 옅은 갈색)으로 그린다.
            for (int i = 0; i < 5; i++)
                Assert.That(marks[i].sprite, Is.SameAs(Field<Sprite>("doneMark")), $"{i}번째");
            Assert.That(marks[5].sprite, Is.SameAs(Field<Sprite>("currentMark")));
            Assert.That(marks[6].sprite, Is.SameAs(Field<Sprite>("upcomingMark")));
        }

        // 시안 문구: 첫 단계는 "호텔에 도착", 비가 내리는 단계는 "비 오는 거리"
        [TestCase(0, "호텔에 도착")]
        [TestCase(4, "비 오는 거리")]
        public void Place_UsesTheMockupNames(int current, string expected)
        {
            Refresh(Journey(current));

            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo(expected));
        }

        // 시안의 시계 칩과 코인 칩은 같은 크기(174×64)다.
        [Test]
        public void StatusChips_ShareTheMockupSize()
        {
            Refresh(Journey(current: 5));
            var clock = (RectTransform)instance.transform.Find("Content/Status/Clock");
            var coins = (RectTransform)instance.transform.Find("Content/Status/Coins");
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)clock.parent);

            Assert.That(clock.rect.size.x, Is.EqualTo(174 * 1.2f).Within(.5f));
            Assert.That(clock.rect.size.y, Is.EqualTo(64 * 1.2f).Within(.5f));
            Assert.That(coins.rect.size.x, Is.EqualTo(clock.rect.size.x).Within(.5f));
            Assert.That(coins.rect.size.y, Is.EqualTo(clock.rect.size.y).Within(.5f));
        }

        // 나란히 놓인 시계 글자와 코인 숫자는 같은 크기로 읽힌다(코인 숫자만 크게 튀지 않는다).
        [Test]
        public void CoinAmount_IsTheSameSizeAsTheClock()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var clock = instance
                .transform.Find("Content/Status/Clock/Label")
                .GetComponent<TMP_Text>();
            var amount = instance
                .transform.Find("Content/Status/Coins/Row/Amount")
                .GetComponent<TMP_Text>();

            Assert.That(amount.fontSize, Is.EqualTo(clock.fontSize).Within(.01f));
        }

        // 코인 그림도 옆 숫자와 어울리는 크기로 읽힌다. 코인만 크게 튀지 않는다(글자 크기의 1.5배 이하).
        [Test]
        public void CoinIcon_IsScaledToTheAmountText()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var amount = instance
                .transform.Find("Content/Status/Coins/Row/Amount")
                .GetComponent<TMP_Text>();
            var coin = instance
                .transform.Find("Content/Status/Coins/Row/Coin")
                .GetComponent<UnityEngine.UI.LayoutElement>();

            Assert.That(coin.preferredHeight, Is.LessThanOrEqualTo(amount.fontSize * 1.5f));
            Assert.That(coin.preferredWidth, Is.EqualTo(coin.preferredHeight).Within(.01f));
        }

        // 시안(g4)처럼 대화 중에는 오른쪽 위 시계·코인 칩과 미니맵을 숨긴다.
        [Test]
        public void WhileTalking_HidesTheClockCoinsAndMiniMap()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            view.BindMap("TOWN", instance.transform);

            view.Refresh(Journey(current: 5), false, talking: true);

            Assert.That(instance.transform.Find("Content/Status").gameObject.activeSelf, Is.False);
            Assert.That(Field<GameObject>("miniMapPanel").activeSelf, Is.False);
            view.Refresh(Journey(current: 5), false, talking: false);
            Assert.That(instance.transform.Find("Content/Status").gameObject.activeSelf, Is.True);
            Assert.That(Field<GameObject>("miniMapPanel").activeSelf, Is.True);
        }

        [Test]
        public void Coins_ShowTheBalance()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 20;
            Refresh(state);

            Assert.That(Field<TMP_Text>("coins").text, Is.EqualTo("20"));
        }

        // 물건을 사면 코인 칩 옆에 줄어든 만큼과 산 것을 보인다. 대화 중에는 숨어 있던 코인 칩도 잠깐 보인다.
        [Test]
        public void CoinChange_ShowsTheAmountAndWhatWasBought_EvenWhileTalking()
        {
            FirstDayMissionState before = Journey(current: 5);
            before.Balance = 96;
            Refresh(before);
            FirstDayMissionState after = Journey(current: 5);
            after.Balance = 88;
            after.LastPurchase = "우산";

            view.Refresh(after, false, talking: true);

            TMP_Text amount = Field<TMP_Text>("coinFloat");
            TMP_Text receipt = Field<TMP_Text>("coinReceipt");
            Assert.That(amount.gameObject.activeInHierarchy, Is.True);
            Assert.That(amount.text, Is.EqualTo("−8"));
            Assert.That(receipt.gameObject.activeInHierarchy, Is.True);
            Assert.That(receipt.text, Does.Contain("우산"));
            Assert.That(
                Field<GameObject>("statusBar").activeSelf,
                Is.True,
                "대화 중에도 잠깐 보인다"
            );
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

        // 폰 메뉴의 오늘의 여정 위젯이 같은 내용을 보이도록 여정을 함께 알린다.
        [Test]
        public void Refresh_SharesTheJourneyWithThePhone()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 20;
            Refresh(state);

            JourneyStatus shared = JourneyStatus.Shared;
            Assert.That(shared.HasJourney, Is.True);
            Assert.That(shared.StepChip, Is.EqualTo("6 / 7"));
            Assert.That(shared.Direction, Is.EqualTo("드럭스토어 점원에게 요청하세요."));
            Assert.That(shared.Balance, Is.EqualTo(20));
            Assert.That(shared.Marks.Count, Is.EqualTo(7));
            Assert.That(shared.Marks[4], Is.EqualTo(JourneyMark.Done));
            Assert.That(shared.Marks[5], Is.EqualTo(JourneyMark.Current));
            Assert.That(shared.Marks[6], Is.EqualTo(JourneyMark.Upcoming));
        }

        [Test]
        public void Unavailable_ForgetsTheSharedJourney()
        {
            Refresh(Journey(current: 5));

            view.SetAvailable(false);

            Assert.That(JourneyStatus.Shared.HasJourney, Is.False);
        }

        // 길 안내는 미션을 모른 채 공용 목적지만 보므로, 지금 단계의 장소를 알린다.
        [Test]
        public void Refresh_GuidesToThePlaceOfTheCurrentStep()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Steps[5].Scene = "SHOP";

            Refresh(state);

            Assert.That(GuideDestination.Shared.PlaceCode, Is.EqualTo("SHOP"));
        }

        [Test]
        public void Refresh_AfterTheJourneyEnds_StopsGuiding()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Steps[5].Scene = "SHOP";
            Refresh(state);
            state.Completed = true;

            view.Refresh(state, false, false);

            Assert.That(GuideDestination.Shared.PlaceCode, Is.Null);
        }

        [Test]
        public void Unavailable_StopsGuiding()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Steps[5].Scene = "SHOP";
            Refresh(state);

            view.SetAvailable(false);

            Assert.That(GuideDestination.Shared.PlaceCode, Is.Null);
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
            // 시안: 왼쪽·위 28px(캔버스 1.2배)
            Vector2 corner = ((RectTransform)card).anchoredPosition;
            Assert.That(corner.x, Is.EqualTo(33.6f).Within(.01f));
            Assert.That(corner.y, Is.EqualTo(-33.6f).Within(.01f));
        }

        // 서버 상태를 받기 전에도 예전 지도 버튼이 아니라 카드 자체를 누르라고 알린다.
        [Test]
        public void QuestCard_BeforeTheServerAnswers_TellsToPressTheCard()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();

            string direction = Field<TMP_Text>("direction").text;
            Assert.That(direction, Does.Contain("카드를 눌러"));
            Assert.That(direction, Does.Not.Contain("지도"));
        }

        // 서버 상태를 받기 전 할 일 문구는 한 줄에 들어가 카드를 키우지 않는다.
        [Test]
        public void QuestCard_BeforeTheServerAnswers_FitsTheDirectionOnOneLine()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                (RectTransform)instance.transform.Find("Content/CurrentGoal")
            );

            TMP_Text direction = Field<TMP_Text>("direction");
            Assert.That(direction.GetTextInfo(direction.text).lineCount, Is.EqualTo(1));
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
