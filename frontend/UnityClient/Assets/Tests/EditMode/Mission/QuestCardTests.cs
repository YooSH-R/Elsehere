using System.Collections.Generic;
using System.Linq;
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
            PracticeVisit.Shared.End();
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
        public void CoinSoundPlaysOnlyWhenTheBalanceIncreases()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 91;
            Refresh(state);
            var sounds = new List<UiSound>();
            UiSoundEvents.Requested += sounds.Add;
            try
            {
                view.Refresh(state, false, talking: true);
                view.Refresh(state, false, talking: false);
                Assert.That(sounds, Is.Empty);

                state.Balance = 88;
                state.LastPurchase = "음료";
                view.Refresh(state, false, talking: true);
                Assert.That(sounds, Is.Empty);

                state.Balance = 98;
                view.Refresh(state, false, talking: false);
                Assert.That(sounds, Is.EqualTo(new[] { UiSound.Coin }));
            }
            finally
            {
                UiSoundEvents.Requested -= sounds.Add;
            }
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
        public void QuestCard_UsesReadableTextSizes()
        {
            Refresh(Journey(current: 1));

            Assert.That(Field<TMP_Text>("stepChip").fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(Field<TMP_Text>("place").fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(Field<TMP_Text>("direction").fontSize, Is.GreaterThanOrEqualTo(36));
            Assert.That(Field<TMP_Text>("situation").fontSize, Is.GreaterThanOrEqualTo(22));
            TMP_Text goal = Field<RectTransform>("goals")
                .GetChild(0)
                .GetComponentInChildren<TMP_Text>();
            Assert.That(goal.fontSize, Is.GreaterThanOrEqualTo(25));
        }

        [Test]
        public void CardKeepsNpcGuidanceAndGoalsVisibleAcrossQuestChanges()
        {
            FirstDayMissionState state = Journey(current: 1);
            state.Direction = "카페 직원 Mia에게 이야기해 보세요.";
            state.Steps[1].Goals[0].Completed = true;
            state
                .Steps[1]
                .Goals.Add(
                    new FirstDayMissionGoal
                    {
                        Id = "drink",
                        Title = "마실 음료 이름 말하기",
                        Hint = "마실 음료 이름",
                    }
                );
            Refresh(state);

            Assert.That(
                instance.transform.Find("Content/CurrentGoal/Header/CollapseButton"),
                Is.Null
            );

            Assert.That(Field<TMP_Text>("stepChip").text, Is.EqualTo("2 / 7"));
            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo("카페에서 휴식"));
            Assert.That(Field<TMP_Text>("direction").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>("direction").text, Does.Contain("Mia"));
            Assert.That(Field<TMP_Text>("situation").gameObject.activeSelf, Is.True);
            Assert.That(Field<RectTransform>("progress").parent.gameObject.activeSelf, Is.True);
            RectTransform goals = Field<RectTransform>("goals");
            Assert.That(goals.GetChild(0).gameObject.activeSelf, Is.True);
            Assert.That(goals.GetChild(1).gameObject.activeSelf, Is.True);
            Assert.That(
                goals.GetChild(1).GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo("마실 음료 이름 말하기")
            );
            Assert.That(HintOf(goals.GetChild(1)).text, Is.EqualTo("마실 음료 이름"));
            Assert.That(Field<QuestBoardView>("questBoard").IsOpen, Is.False);

            state.IsTownStory = true;
            state.Direction = "빨래방 주인 Gus에게 이야기해 보세요.";
            state.Steps[1].Title = "빨래방에서 세탁 문의";
            view.Refresh(state, false, false);
            Assert.That(Field<TMP_Text>("direction").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>("direction").text, Does.Contain("Gus"));
            Assert.That(goals.GetChild(0).gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CardHidesRepeatedSituationAndShowsNewDetailsOnRefresh()
        {
            FirstDayMissionState state = Journey(current: 0);
            state.Direction = "마을로 나가 식당 종업원 Rosa에게 이야기해 보세요.";
            state.Situation = "마을로 나가 식당 종업원 Rosa에게 이야기 해 보세요";
            Refresh(state);

            Assert.That(Field<TMP_Text>("direction").text, Is.EqualTo(state.Direction));
            TMP_Text situation = Field<TMP_Text>("situation");
            Assert.That(situation.gameObject.activeSelf, Is.False);

            state.Situation = "아침 식사는 음식과 음료를 모두 주문해야 해요.";
            view.Refresh(state, false, false);

            Assert.That(situation.gameObject.activeSelf, Is.True);
            Assert.That(situation.text, Is.EqualTo(state.Situation));
        }

        [Test]
        public void CardWhenRestingShowsTheCurrentAction()
        {
            FirstDayMissionState state = Journey(current: 1);
            state.CanRest = true;
            Refresh(state);

            Assert.That(Field<RectTransform>("goals").gameObject.activeSelf, Is.False);
            Assert.That(Field<TMP_Text>("direction").gameObject.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>("direction").text, Does.Contain("의자에 앉아"));
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

        // 높이는 같은 64px를 유지하고, 시계만 글자 양옆의 24px 여백에 맞춰 넓어진다.
        [Test]
        public void StatusChips_KeepTheirHeightAndLeaveRoomAroundTheClock()
        {
            Refresh(Journey(current: 5));
            var clock = (RectTransform)instance.transform.Find("Content/Status/Clock");
            var coins = (RectTransform)instance.transform.Find("Content/Status/Coins");
            TMP_Text label = clock.Find("Label").GetComponent<TMP_Text>();
            SettleClockLayout(label, (RectTransform)clock.parent);

            Assert.That(
                clock.rect.size.x,
                Is.GreaterThanOrEqualTo(label.preferredWidth + 48 * 1.2f - .5f)
            );
            Assert.That(clock.rect.size.y, Is.EqualTo(64 * 1.2f).Within(.5f));
            Assert.That(coins.rect.size.x, Is.EqualTo(174 * 1.2f).Within(.5f));
            Assert.That(coins.rect.size.y, Is.EqualTo(clock.rect.size.y).Within(.5f));
            Assert.That(label.fontSize, Is.EqualTo(21 * 1.2f).Within(.01f));
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(
                clock.TransformPoint(clock.rect.center).y,
                Is.EqualTo(coins.TransformPoint(coins.rect.center).y).Within(.1f)
            );
            Assert.That(
                clock.TransformPoint(clock.rect.xMax, 0, 0).x,
                Is.LessThan(coins.TransformPoint(coins.rect.xMin, 0, 0).x)
            );
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

        [Test]
        public void WhileTalking_HidesTheClockAndCoinsEvenWhenTheBalanceChanges()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            view.BindMap("TOWN", instance.transform);

            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 96;
            view.Refresh(state, false, talking: true);
            GameObject status = Field<GameObject>("statusBar");
            Assert.That(status.activeSelf, Is.False);
            Assert.That(Field<GameObject>("miniMapPanel").activeSelf, Is.False);

            state.Balance = 88;
            state.LastPurchase = "우산";
            view.Refresh(state, false, talking: true);
            Assert.That(status.activeSelf, Is.False);

            view.Refresh(state, false, talking: false);
            Assert.That(status.activeSelf, Is.True);
            Assert.That(Field<TMP_Text>("coins").text, Is.EqualTo("88"));
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

        // 물건을 사면 코인 칩 옆에 줄어든 만큼과 산 것을 보인다.
        [Test]
        public void CoinChange_ShowsTheAmountAndWhatWasBoughtDuringExploration()
        {
            FirstDayMissionState before = Journey(current: 5);
            before.Balance = 96;
            Refresh(before);
            FirstDayMissionState after = Journey(current: 5);
            after.Balance = 88;
            after.LastPurchase = "우산";

            view.Refresh(after, false, talking: false);

            TMP_Text amount = Field<TMP_Text>("coinFloat");
            TMP_Text receipt = Field<TMP_Text>("coinReceipt");
            Assert.That(amount.gameObject.activeInHierarchy, Is.True);
            Assert.That(amount.text, Is.EqualTo("−8"));
            Assert.That(receipt.gameObject.activeInHierarchy, Is.True);
            Assert.That(receipt.text, Does.Contain("우산"));
            Assert.That(Field<GameObject>("statusBar").activeSelf, Is.True);
        }

        [Test]
        public void Clock_ShowsTheGameTime()
        {
            Refresh(Journey(current: 5));

            TMP_Text clock = Field<TMP_Text>("clock");
            clock.ForceMeshUpdate();
            Assert.That(
                clock.GetParsedText(),
                Is.EqualTo(GameClockText.Format(GameClock.Shared.Minutes))
            );
        }

        [Test]
        public void Clock_KeepsTheTimeAlignedAndFitsLongDays()
        {
            var sharedProperty = typeof(GameClock).GetProperty(nameof(GameClock.Shared));
            GameClock previousClock = GameClock.Shared;
            var gameClock = new GameClock();
            sharedProperty.SetValue(null, gameClock);
            try
            {
                FirstDayMissionState state = Journey(current: 5);
                Refresh(state);
                TMP_Text label = Field<TMP_Text>("clock");
                var chip = (RectTransform)label.transform.parent;
                var status = (RectTransform)chip.parent;
                RectTransform coins = Field<RectTransform>("coinChip");

                var morning = MeasureClock();
                var twoDigitHour = AtMinute(11 * 60 + 11);
                var afternoon = AtMinute(13 * 60 + 59);
                var tenDays = AtMinute(9 * 1440 + 12 * 60);
                var longDay = AtMinute(999999 * 1440 + 12 * 60);

                Assert.That(twoDigitHour.Colon, Is.EqualTo(morning.Colon).Within(.1f));
                Assert.That(afternoon.Colon, Is.EqualTo(morning.Colon).Within(.1f));
                Assert.That(twoDigitHour.Width, Is.EqualTo(morning.Width).Within(.1f));
                Assert.That(tenDays.TextFits, Is.True);
                Assert.That(longDay.TextFits, Is.True);
                Assert.That(label.GetParsedText(), Is.EqualTo("1000000일차 · 오후 12:00"));
                Assert.That(longDay.Width, Is.GreaterThan(tenDays.Width));
                Assert.That(longDay.CoinRight, Is.EqualTo(morning.CoinRight).Within(.1f));

                (float Colon, float Width, bool TextFits, float CoinRight) AtMinute(int minutes)
                {
                    gameClock.Flow(
                        (minutes + .1 - gameClock.Minutes) / GameClock.GameMinutesPerRealSecond
                    );
                    view.Refresh(state, false, false);
                    return MeasureClock();
                }

                (float Colon, float Width, bool TextFits, float CoinRight) MeasureClock()
                {
                    SettleClockLayout(label, status);
                    int colon = label.GetParsedText().IndexOf(':');
                    float colonX = label.textInfo.characterInfo[colon].origin;
                    return (
                        label.rectTransform.TransformPoint(colonX, 0, 0).x,
                        chip.rect.width,
                        label.preferredWidth <= label.rectTransform.rect.width + .1f,
                        coins.TransformPoint(coins.rect.xMax, 0, 0).x
                    );
                }
            }
            finally
            {
                sharedProperty.SetValue(null, previousClock);
            }
        }

        private static void SettleClockLayout(TMP_Text label, RectTransform status)
        {
            // TMP 글자 폭과 부모 레이아웃이 서로 갱신된 뒤 최종 위치를 측정한다.
            for (int i = 0; i < 3; i++)
            {
                label.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(status);
            }
            label.ForceMeshUpdate();
        }

        // 폰 메뉴의 코인 위젯이 같은 잔액을 보이도록 함께 알린다. 퀘스트 진행은 왼쪽 위 알약만 보인다.
        [Test]
        public void Refresh_SharesTheBalanceWithThePhone()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 20;
            Refresh(state);

            Assert.That(JourneyStatus.Shared.Balance, Is.EqualTo(20));
        }

        [Test]
        public void Unavailable_ForgetsTheSharedBalance()
        {
            FirstDayMissionState state = Journey(current: 5);
            state.Balance = 20;
            Refresh(state);

            view.SetAvailable(false);

            Assert.That(JourneyStatus.Shared.Balance, Is.Zero);
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

        // 왼쪽 위 모서리(28)에는 책 [퀘스트] 버튼(오른쪽 위 HUD와 같은 64)을 두고, 알약과 펼친 카드는 그 오른쪽 12에 붙인다.
        // 카드 본문을 누르면 퀘스트 창이 열린다.
        [Test]
        public void PillAndCard_SitRightOfTheQuestButtonInTheTopLeft()
        {
            Refresh(Journey(current: 5));
            view.UpdateLayout();

            var button = (RectTransform)Field<Button>("questIcon").transform;
            var pill = (RectTransform)Field<Button>("questPill").transform;
            Transform card = instance.transform.Find("Content/CurrentGoal");
            Assert.That(Field<Button>("journalButton").transform, Is.SameAs(card));
            Assert.That(button.anchoredPosition, Is.EqualTo(new Vector2(28, -28) * 1.2f));
            Assert.That(button.rect.size, Is.EqualTo(new Vector2(64, 64) * 1.2f));
            Vector2 beside = new Vector2(28 + 64 + 12, -28) * 1.2f;
            Assert.That(Vector2.Distance(pill.anchoredPosition, beside), Is.LessThan(.01f), "알약");
            Assert.That(
                Vector2.Distance(((RectTransform)card).anchoredPosition, beside),
                Is.LessThan(.01f),
                "펼친 카드"
            );
        }

        // 책 [퀘스트] 버튼은 하는 퀘스트가 있든 없든 늘 왼쪽 위에 있고, 누르면 퀘스트 창이 열린다. 튜토리얼 안내도 이 버튼을 가리킨다.
        // 하는 퀘스트가 없으면(시작 전, 끝낸 뒤) 버튼만 있다.
        [Test]
        public void QuestButton_IsAlwaysThere_AndOpensTheQuestWindow()
        {
            FirstDayMissionState notStarted = Journey(current: 0);
            notStarted.Started = false;
            Refresh(notStarted);
            Transform card = instance.transform.Find("Content/CurrentGoal");
            Button icon = Field<Button>("questIcon");
            Assert.That(card.gameObject.activeSelf, Is.False, "시작 전");
            Assert.That(Field<Button>("questPill").gameObject.activeSelf, Is.False, "시작 전");
            Assert.That(icon.gameObject.activeSelf, Is.True, "시작 전");
            Assert.That(view.QuestButtonRect, Is.SameAs(icon.transform));
            icon.onClick.Invoke();
            Assert.That(
                Field<QuestBoardView>("questBoard").IsOpen,
                Is.True,
                "아이콘을 누르면 퀘스트 목록"
            );
            Field<QuestBoardView>("questBoard").Close();

            FirstDayMissionState finished = Journey(current: 6);
            finished.Completed = true;
            view.Refresh(finished, false, false);
            Assert.That(card.gameObject.activeSelf, Is.False, "끝낸 뒤");
            Assert.That(icon.gameObject.activeSelf, Is.True, "끝낸 뒤");

            view.Refresh(Journey(current: 2), false, false);
            Assert.That(card.gameObject.activeSelf, Is.True, "진행 중");
            Assert.That(icon.gameObject.activeSelf, Is.True, "진행 중에도 버튼");
            Assert.That(view.QuestButtonRect, Is.SameAs(icon.transform));

            PracticeVisit.Shared.Begin(DinerTable());
            view.Refresh(Journey(current: 2), false, false);
            Assert.That(icon.gameObject.activeSelf, Is.True, "장소 연습 중에도 버튼");
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

        // 목표 아래에 무엇을 말해야 하는지 작게 보인다. 말할 내용이 없는 목표는 이름만 보인다.
        [Test]
        public void Goal_ShowsWhatToSayUnderItsName()
        {
            FirstDayMissionState state = Journey(current: 3);
            state.Steps[3].Goals[0].Hint = "파란색 숄더백, 빨간 열쇠고리";
            state
                .Steps[3]
                .Goals.Add(new FirstDayMissionGoal { Id = "own", Title = "본인 물건 확인" });

            Refresh(state);

            RectTransform goals = Field<RectTransform>("goals");
            TMP_Text first = HintOf(goals.GetChild(0));
            Assert.That(first.text, Is.EqualTo("파란색 숄더백, 빨간 열쇠고리"));
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(HintOf(goals.GetChild(1)).gameObject.activeSelf, Is.False);
        }

        // 하던 이야기는 평소 책 버튼 옆 한 줄 알약('이야기' · 진행 칸 · ⌄)으로 접어 둔다. 알약에는 아이콘·단계 글자를 두지 않는다.
        [Test]
        public void StoryInProgress_ShowsOnlyAOneLinePill()
        {
            Refresh(Journey(current: 2));

            Button pill = Field<Button>("questPill");
            Assert.That(pill.gameObject.activeSelf, Is.True);
            Assert.That(Card.gameObject.activeSelf, Is.False);
            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("이야기"));
            Assert.That(
                pill.GetComponentsInChildren<TMP_Text>().Select(text => text.text),
                Is.EqualTo(new[] { "이야기" }),
                "알약 글자는 이름 하나"
            );
            Assert.That(
                pill.GetComponentsInChildren<Image>().Count(image => image.name == "Icon"),
                Is.Zero
            );
            Assert.That(
                PillMarks(),
                Is.EqualTo(new[] { "story", "story", "story", "off", "off", "off", "off" }),
                "끝낸 단계와 지금 단계를 칠합니다."
            );
        }

        // 알약을 누르면 카드를 펼치고, 펼친 카드의 접기 버튼으로 다시 접는다. 카드를 누르면 지금처럼 퀘스트 창이 열린다.
        [Test]
        public void Pill_ExpandsTheCard_AndItsFoldButtonFoldsItBack()
        {
            Refresh(Journey(current: 2));

            Field<Button>("questPill").onClick.Invoke();
            Button fold = Field<Button>("foldButton");
            Assert.That(Card.gameObject.activeSelf, Is.True);
            Assert.That(Field<Button>("questPill").gameObject.activeSelf, Is.False);
            Assert.That(fold.transform.IsChildOf(Card), Is.True, "펼친 카드에 늘 접기 버튼");
            Assert.That(fold.gameObject.activeSelf, Is.True);
            Assert.That(
                Field<Image>("autoFoldBar").gameObject.activeSelf,
                Is.False,
                "직접 펼친 카드는 저절로 접지 않습니다."
            );

            fold.onClick.Invoke();
            Assert.That(Card.gameObject.activeSelf, Is.False);
            Assert.That(Field<Button>("questPill").gameObject.activeSelf, Is.True);
        }

        // 이야기를 시작할 때만 카드를 펼쳐 알린다(5초 뒤 저절로 접힘). 하던 이야기를 불러올 때는 알약으로 둔다.
        [Test]
        public void StartingAStory_ExpandsTheCard_ButLoadingOneInProgressDoesNot()
        {
            Refresh(Journey(current: 6));
            Assert.That(Card.gameObject.activeSelf, Is.False, "하던 이야기");

            FirstDayMissionState finished = Journey(current: 6);
            finished.Completed = true;
            view.Refresh(finished, false, false);
            FirstDayMissionState next = Journey(current: 0);
            next.IsTownStory = true;
            view.Refresh(next, false, false);

            Assert.That(Card.gameObject.activeSelf, Is.True, "새로 시작한 이야기");
            Assert.That(Field<Image>("autoFoldBar").gameObject.activeSelf, Is.True);
        }

        // 장소 연습 방문 중에는 이야기 대신 그 연습만 보이고, 방문이 끝나면 이야기 알약으로 돌아온다(시안 1-B ③).
        [Test]
        public void PracticeVisit_ShowsOnlyThePractice_ThenTheStoryAgain()
        {
            PracticeVisit.Shared.Begin(DinerTable());
            Refresh(Journey(current: 2));

            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("장소 연습"));
            Assert.That(PillMarks(), Is.EqualTo(new[] { "off", "off" }), "목표마다 한 칸");

            PracticeVisit.Shared.ReportProgress(new[] { true, false });
            view.Refresh(Journey(current: 2), false, false);
            Assert.That(PillMarks(), Is.EqualTo(new[] { "practice", "off" }), "해낸 목표");

            PracticeVisit.Shared.End();
            view.Refresh(Journey(current: 2), false, false);
            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("이야기"));
            Assert.That(PillMarks(), Has.Length.EqualTo(7));
        }

        // [다시 연습] 중에는 장소 연습 칩 대신 '다시 연습' 칩·알약과 지난 상황을 시연 카드 모양으로 보인다.
        [Test]
        public void RetryVisit_ShowsTheRetryNameInsteadOfPlacePractice()
        {
            PracticeVisitPlan retry = DinerTable();
            retry.IsReview = true;
            PracticeVisit.Shared.Begin(retry);
            Refresh(Journey(current: 2));
            Field<Button>("questPill").onClick.Invoke();

            Assert.That(Field<TMP_Text>("pillTitle").text, Is.EqualTo("다시 연습"));
            Assert.That(Field<GameObject>("practiceChip").activeSelf, Is.False);
            // 시연 카드처럼 위 칩과 제목 한 줄, 긴 설명 없이 목표 줄만 보인다.
            TMP_Text chip = Field<TMP_Text>("stepChip");
            Assert.That(chip.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(chip.text, Is.EqualTo("다시 연습"));
            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo("자리 잡기"));
            Assert.That(Field<TMP_Text>("direction").gameObject.activeSelf, Is.False);
        }

        // 펼친 연습 카드: 파란 '장소 연습' 칩, 장소 · 상황, 상황 설명, 목표마다 한 칸인 진행 막대와 목표 줄(해낸 목표는 체크)
        [Test]
        public void PracticeCard_ShowsThePlaceTheSituationAndWhichGoalsAreDone()
        {
            PracticeVisit.Shared.Begin(DinerTable());
            PracticeVisit.Shared.ReportProgress(new[] { true, false });
            Refresh(Journey(current: 2));
            Field<Button>("questPill").onClick.Invoke();

            Assert.That(Field<GameObject>("practiceChip").activeSelf, Is.True);
            Assert.That(
                Field<TMP_Text>("stepChip").transform.parent.gameObject.activeSelf,
                Is.False
            );
            Assert.That(Field<TMP_Text>("place").text, Is.EqualTo("식당 · 자리 잡기"));
            Assert.That(
                Field<TMP_Text>("direction").text,
                Is.EqualTo("몇 명인지 말하고 창가 자리를 부탁해요.")
            );
            Assert.That(Field<TMP_Text>("situation").gameObject.activeSelf, Is.False);
            RectTransform progress = Field<RectTransform>("progress");
            Assert.That(
                progress.Cast<Transform>().Count(mark => mark.gameObject.activeSelf),
                Is.EqualTo(2)
            );
            Assert.That(
                progress.GetChild(0).GetComponent<Image>().sprite,
                Is.SameAs(Field<Sprite>("practiceDoneMark"))
            );
            Assert.That(
                progress.GetChild(1).GetComponent<Image>().sprite,
                Is.SameAs(Field<Sprite>("upcomingMark"))
            );
            RectTransform goals = Field<RectTransform>("goals");
            TMP_Text done = goals.GetChild(0).GetComponentInChildren<TMP_Text>(true);
            TMP_Text left = goals.GetChild(1).GetComponentInChildren<TMP_Text>(true);
            Assert.That(done.text, Is.EqualTo("인원 말하고 창가 자리 부탁하기"));
            Assert.That(done.fontStyle, Is.EqualTo(FontStyles.Strikethrough));
            Assert.That(left.fontStyle, Is.EqualTo(FontStyles.Normal));
            Assert.That(HintOf(goals.GetChild(1)).text, Is.EqualTo("얼마나 기다려야 하는지 묻기"));
        }

        // 연습 카드는 목표 칸을 카드 폭에 맞춰 늘린다. 연습이 끝나 이야기 카드로 돌아오면 단계 칸은 다시 시안 폭(46.86 + 빛 번짐 자리)이다.
        [Test]
        public void StoryProgress_AfterAPractice_GoesBackToTheMockupMarkWidth()
        {
            PracticeVisit.Shared.Begin(DinerTable());
            Refresh(Journey(current: 2));
            view.ExpandQuestCard();
            RectTransform progress = Field<RectTransform>("progress");
            // 화면에서는 캔버스가 카드와 진행 칸 줄을 따로 계산한다.
            void Lay()
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Card);
                LayoutRebuilder.ForceRebuildLayoutImmediate(progress);
            }
            Lay();
            float practiceMark = ((RectTransform)progress.GetChild(0)).rect.width;

            PracticeVisit.Shared.End();
            view.Refresh(Journey(current: 2), false, false);
            view.ExpandQuestCard();
            Lay();

            Assert.That(practiceMark, Is.GreaterThan((46.86f + 24) * 1.2f), "연습 칸은 늘어남");
            foreach (Transform mark in progress)
                Assert.That(
                    ((RectTransform)mark).rect.width,
                    Is.EqualTo((46.86f + 24) * 1.2f).Within(.5f)
                );
        }

        // 세로 휴대폰처럼 좁은 화면에서는 왼쪽 위 알약·펼친 카드가 오른쪽 위 시계·코인 칩과 겹치지 않게 통째로 줄인다.
        // 카드는 폭을 좁혀 줄을 바꾸지 않고 글자까지 같은 비율로 작게 그린다.
        [Test]
        public void NarrowScreen_KeepsThePillAndCardLeftOfTheClock()
        {
            Refresh(Journey(current: 2));
            instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)instance.transform).sizeDelta = new Vector2(1080, 1920);
            var status = (RectTransform)instance.transform.Find("Content/Status");

            var pill = (RectTransform)Field<Button>("questPill").transform;
            float StatusLeft() => Corner(status, 0).x;
            // 화면에서는 캔버스가 매 프레임 하는 레이아웃 계산을 칩·알약·카드마다 직접 한다.
            void Lay()
            {
                foreach (RectTransform part in new[] { status, pill, (RectTransform)Card })
                    LayoutRebuilder.ForceRebuildLayoutImmediate(part);
                view.UpdateLayout();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Card);
            }

            Lay();
            Assert.That(Corner(pill, 2).x, Is.LessThanOrEqualTo(StatusLeft()), "알약");

            view.ExpandQuestCard();
            Lay();
            Assert.That(
                Corner((RectTransform)Card, 2).x,
                Is.LessThanOrEqualTo(StatusLeft()),
                "카드"
            );
            Assert.That(
                ((RectTransform)Card).rect.width,
                Is.EqualTo(410 * 1.2f).Within(.01f),
                "카드 폭은 그대로 두고"
            );
            Assert.That(Card.localScale.x, Is.LessThan(1f), "글자까지 같은 비율로 줄입니다");
        }

        private static Vector3 Corner(RectTransform rect, int index)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[index];
        }

        // 책 버튼은 혼자 있을 때 넓고 밝게 빛나고, 옆에 알약·카드가 뜨면 그 창에 빛이 비치지 않게 작고 옅게 빛난다.
        [Test]
        public void QuestButtonGlow_IsSoftWhileAPillOrCardIsBeside()
        {
            FirstDayMissionState notStarted = Journey(current: 0);
            notStarted.Started = false;
            Refresh(notStarted);
            Image glow = Field<Image>("questButtonArt");
            Assert.That(glow.sprite, Is.SameAs(Field<Sprite>("questButtonGlow")), "혼자");

            view.Refresh(Journey(current: 2), false, false);
            Assert.That(
                glow.sprite,
                Is.SameAs(Field<Sprite>("questButtonSoftGlow")),
                "알약·카드 옆"
            );
            view.FoldQuestCard();
            Assert.That(glow.sprite, Is.SameAs(Field<Sprite>("questButtonSoftGlow")), "알약 옆");
        }

        private Transform Card => instance.transform.Find("Content/CurrentGoal");

        // 알약 진행 칸을 칠한 색으로 읽는다: 이야기(코랄)·연습(파랑)·빈 칸
        private string[] PillMarks()
        {
            var marks = Field<RectTransform>("pillMarks");
            Sprite story = Field<Sprite>("pillMarkStory");
            Sprite practice = Field<Sprite>("pillMarkPractice");
            return marks
                .Cast<Transform>()
                .Where(mark => mark.gameObject.activeSelf)
                .Select(mark => mark.GetComponent<Image>().sprite)
                .Select(sprite =>
                    sprite == story ? "story"
                    : sprite == practice ? "practice"
                    : "off"
                )
                .ToArray();
        }

        private static PracticeVisitPlan DinerTable() =>
            new PracticeVisitPlan
            {
                ScenarioId = "DINER_TABLE",
                PlaceName = "식당",
                NpcName = "Rosa",
                Title = "자리 잡기",
                Description = "몇 명인지 말하고 창가 자리를 부탁해요.",
                Goals = new[]
                {
                    new PracticeVisitGoal(
                        "인원 말하고 창가 자리 부탁하기",
                        "몇 명인지 말하고 창가 자리를 부탁하기"
                    ),
                    new PracticeVisitGoal("기다리는 시간 묻기", "얼마나 기다려야 하는지 묻기"),
                },
            };

        private static TMP_Text HintOf(Transform row)
        {
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Hint")
                    return text;
            }
            Assert.Fail("목표 줄에 Hint 글자가 없습니다.");
            return null;
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
