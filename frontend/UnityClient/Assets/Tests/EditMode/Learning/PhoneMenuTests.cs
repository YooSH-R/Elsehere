using A305.Player;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 오른쪽 위 휴대폰 메뉴. 잠금 화면처럼 큰 게임 시계와 날짜, 여행 예산, 오늘의 여정을 보이고,
    /// 단어장·대화 노트·마을 지도·내 정보·이웃 앱과 작은 집으로·조작법 버튼을 둔다. 앱은 모두 메뉴 안에 있어 누르면 메뉴가 접힌다.
    /// </summary>
    public sealed class PhoneMenuTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        private GameObject instance;
        private PhoneMenuView phone;

        [SetUp]
        public void CreateHud()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            phone = instance.GetComponentInChildren<PhoneMenuView>(true);
        }

        [TearDown]
        public void DestroyHud()
        {
            Object.DestroyImmediate(instance);
            JourneyStatus.Shared.Clear();
            PlayerNickname.Reset();
        }

        // 시안처럼 날짜 뒤에 닉네임을 붙인다. 닉네임을 모르면 날짜만 보인다.
        [Test]
        public void Date_EndsWithTheNickname()
        {
            PlayerNickname.Set("다빈");

            phone.Refresh();

            Assert.That(
                Field<TMP_Text>("date").text,
                Is.EqualTo(GameClockText.DayAndHalf(GameClock.Shared.Minutes) + " · 다빈")
            );
        }

        [Test]
        public void Refresh_ShowsTheGameTimeLikeALockScreen()
        {
            phone.Refresh();

            double minutes = GameClock.Shared.Minutes;
            Assert.That(Field<TMP_Text>("clock").text, Is.EqualTo(GameClockText.Time(minutes)));
            Assert.That(
                Field<TMP_Text>("date").text,
                Is.EqualTo(GameClockText.DayAndHalf(minutes))
            );
        }

        [Test]
        public void Refresh_ShowsTheSharedJourneyAndBudget()
        {
            var marks = new[] { JourneyMark.Current, JourneyMark.Upcoming };
            JourneyStatus.Shared.Show("1 / 2", "호텔 직원에게 요청하세요.", marks, 20);

            phone.Refresh();

            Assert.That(Field<GameObject>("journeyCard").activeSelf, Is.True);
            Assert.That(Field<TMP_Text>("journeyChip").text, Is.EqualTo("1 / 2"));
            Assert.That(
                Field<TMP_Text>("journeyDirection").text,
                Is.EqualTo("호텔 직원에게 요청하세요.")
            );
            Assert.That(Field<TMP_Text>("budget").text, Is.EqualTo("20"));
            int shown = 0;
            foreach (Transform mark in Field<RectTransform>("journeyProgress"))
            {
                if (!mark.gameObject.activeSelf)
                    continue;
                string state = marks[shown] == JourneyMark.Current ? "currentMark" : "upcomingMark";
                Assert.That(mark.GetComponent<Image>().sprite, Is.SameAs(Field<Sprite>(state)));
                shown++;
            }
            Assert.That(shown, Is.EqualTo(2));
        }

        // 단계가 시안(7칸)보다 많아도(마을 이야기 12단계) 진행 칸을 줄여 카드 안에 넣는다.
        [Test]
        public void ManySteps_StayInsideTheJourneyCard()
        {
            var marks = new JourneyMark[12];
            JourneyStatus.Shared.Show(
                "12 / 12",
                "마을의 이웃들과 자유롭게 대화해 보세요.",
                marks,
                76
            );

            phone.Refresh();

            var progress = Field<RectTransform>("journeyProgress");
            // 닫혀 있는 폰을 열어 배치를 계산한다.
            for (Transform part = progress; part != null; part = part.parent)
                part.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(progress);
            int shown = 0;
            foreach (RectTransform mark in progress)
            {
                if (!mark.gameObject.activeSelf)
                    continue;
                shown++;
                Assert.That(
                    mark.anchoredPosition.x + mark.rect.width * (1 - mark.pivot.x),
                    Is.LessThanOrEqualTo(progress.rect.width + .05f),
                    $"{shown}번째 칸"
                );
            }
            Assert.That(shown, Is.EqualTo(12));
        }

        [Test]
        public void WithoutAJourney_HidesTheJourneyWidget()
        {
            phone.Refresh();

            Assert.That(Field<GameObject>("journeyCard").activeSelf, Is.False);
        }

        [Test]
        public void TownMapApp_AsksTheMissionToOpenTheMap()
        {
            bool requested = false;
            JourneyStatus.Shared.MapRequested += () => requested = true;

            phone.OpenTownMap();

            Assert.That(requested, Is.True);
        }

        // 피드백 목록과 복습장을 [대화 노트] 하나로 합쳐 학습 앱은 다섯 개이고, 세 칸씩 왼쪽부터 채운다.
        [Test]
        public void LearningApps_AreFiveWithTheConversationNoteAndNoReviewBook()
        {
            Transform menu = phone.transform;
            string[] names =
            {
                "VocabularyButton",
                "HistoryButton",
                "MapButton",
                "MyPageButton",
                "NeighborsButton",
            };

            Assert.That(menu.Find("ReviewButton"), Is.Null);
            for (int i = 0; i < names.Length; i++)
            {
                var app = (RectTransform)menu.Find(names[i]);
                Assert.That(
                    app.anchoredPosition,
                    Is.EqualTo(new Vector2(38 + i % 3 * 122, -(338 + i / 3 * 148)) * 1.2f),
                    names[i]
                );
            }
            Assert.That(
                menu.Find("HistoryButton/Label").GetComponent<TMP_Text>().text,
                Is.EqualTo("대화 노트")
            );
        }

        // 앱은 모두 메뉴(폰) 안에 있어야 눌렀을 때 메뉴가 접힌다.
        [TestCase("VocabularyButton")]
        [TestCase("HistoryButton")]
        [TestCase("MapButton")]
        [TestCase("HomeButton")]
        [TestCase("MyPageButton")]
        public void EveryApp_SitsInsideThePhone(string app)
        {
            Transform menu = phone.transform;

            Assert.That(menu.Find(app), Is.Not.Null, app);
            Assert.That(Field<Button>("mapButton").transform.parent, Is.SameAs(menu));
        }

        [Test]
        public void ControlsApp_HasAVisibleIconAndKeepsTheGuideConnection()
        {
            Transform app = phone.transform.Find("ControlsButton");
            Transform icon = app.Find("Icon");

            Assert.That(icon, Is.Not.Null, "조작법 앱의 누름 영역과 이름만 있고 아이콘이 없어요.");
            var image = icon.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.color.a, Is.GreaterThan(0f));
            Assert.That(image.raycastTarget, Is.False);
            var guide = instance.GetComponent<ControlsGuideView>();
            Assert.That(
                new SerializedObject(guide).FindProperty("openButton").objectReferenceValue,
                Is.SameAs(app.GetComponent<Button>())
            );
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(phone).FindProperty(name).objectReferenceValue;
    }
}
