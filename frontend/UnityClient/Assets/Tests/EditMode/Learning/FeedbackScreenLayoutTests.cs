using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 대화 피드백 화면은 확정 시안(F1 요약·F2 문장 교정, N3)이다. 탭은 요약·문장 교정·전체 대화 셋이고, 확대 버튼은 머리줄의 X 왼쪽에 둔다.
    /// 대화는 대화 노트에 저절로 남아 따로 담는 [복습장에 추가] 버튼은 없다. 요약 탭은 요약 카드(잘했어요·더 해 봐요) 아래에 "이 상황에서 자주 써요" 표현 줄을,
    /// 문장 교정 탭은 발화마다 카드를 쌓는다. 크기·자리는 시안 px에 1.2를 곱한 판 안의 단위다.
    /// </summary>
    public class FeedbackScreenLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const float Px = 1.2f;
        private const string Summary =
            "Mia에게 아이스 라테를 주문하고, 두고 간 파란 가방을 찾아 달라고 했어요.";

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.DestroyImmediate(instance);

        [Test]
        public void Header_ShowsTheThreeTabsOfTheMockup()
        {
            Show();

            Assert.That(Field<TMP_Text>("titleText").text, Is.EqualTo("대화 피드백"));
            Assert.That(
                Field<RectTransform>("sectionRow")
                    .GetComponentsInChildren<Button>(true)
                    .Select(tab => tab.GetComponentInChildren<TMP_Text>(true).text),
                Is.EqualTo(new[] { "요약", "문장 교정", "전체 대화" })
            );
        }

        // 확대 버튼은 [복습장에 추가]와 겹치지 않게 머리줄의 X 바로 왼쪽(사이 14)에 X와 같은 둥근 버튼으로 둔다.
        [Test]
        public void ExpandButton_IsARoundButtonLeftOfTheClose()
        {
            Show();
            var expandButton = Field<Button>("expandButton");
            var closeButton = Field<Button>("topCloseButton");
            var expandRect = (RectTransform)expandButton.transform;
            var closeRect = (RectTransform)closeButton.transform;
            Rect expand = Box(expandRect);
            Rect close = Box(closeRect);

            // 창 좌표로 왕복 변환하면 같은 크기여도 창의 배율에 따라 반올림 오차가 생긴다.
            Assert.That(expandRect.rect.size, Is.EqualTo(closeRect.rect.size));
            Assert.That(expand.center.y, Is.EqualTo(close.center.y).Within(.01f));
            Assert.That(close.xMin - expand.xMax, Is.EqualTo(14 * Px).Within(.01f));
            Assert.That(
                expandButton.GetComponent<Image>().sprite,
                Is.EqualTo(closeButton.GetComponent<Image>().sprite),
                "X와 같은 둥근 바탕"
            );
            Assert.That(IconOf(expandButton), Is.EqualTo("FeedbackExpandIcon"));

            view.ToggleExpanded();

            Assert.That(IconOf(expandButton), Is.EqualTo("FeedbackCollapseIcon"));
        }

        // 끝난 대화는 대화 노트에 저절로 남으므로 피드백 창에서 따로 담는 [복습장에 추가] 버튼을 두지 않는다.
        [Test]
        public void Footer_HasNoReviewBookButton()
        {
            Show();

            Assert.That(Panel().Find("ReviewSaveButton"), Is.Null);
        }

        [Test]
        public void SummaryBody_IsReadableAtTheDefaultWindowSize()
        {
            Show();
            TMP_Text summary = Field<RectTransform>("summaryCard")
                .Find("Text")
                .GetComponent<TMP_Text>();

            Assert.That(
                summary.fontSize * view.PanelRect.localScale.x,
                Is.GreaterThanOrEqualTo(22)
            );
        }

        [Test]
        public void SummaryTab_ShowsTheSummaryCardWithGoodAndMoreNotesSideBySide()
        {
            Show();
            Transform card = Field<RectTransform>("summaryCard");

            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Assert.That(Text(card, "Text"), Is.EqualTo(Summary));
            Assert.That(Text(card, "Good/Head"), Is.EqualTo("잘했어요"));
            Assert.That(
                Text(card, "Good/Body"),
                Is.EqualTo("가방의 색과 열쇠고리까지 자세히 말했어요.")
            );
            Assert.That(Text(card, "More/Head"), Is.EqualTo("더 해 봐요"));
            Assert.That(
                Text(card, "More/Body"),
                Is.EqualTo("“I think lost”처럼 주어를 빠뜨린 문장이 있었어요.")
            );
            Rect good = Box((RectTransform)card.Find("Good"));
            Rect more = Box((RectTransform)card.Find("More"));
            Assert.That(good.yMax, Is.EqualTo(more.yMax).Within(.01f));
            Assert.That(more.xMin - good.xMax, Is.EqualTo(12 * Px).Within(.01f));
        }

        // 표현 줄은 요약 카드 아래 옅은 제목(이 상황에서 자주 써요) 밑에 놓인다.
        [Test]
        public void SituationHeading_IsAMutedTitleBelowTheSummaryCard()
        {
            Show();
            TMP_Text heading = SituationTitle();
            Rect card = Box(Field<RectTransform>("summaryCard"));
            Rect title = Box(heading.rectTransform);
            // 시안 .h3의 rgba(47,37,27,.6)을 판 위에 섞은 불투명 색
            var muted = new Color32(128, 117, 106, 255);

            Assert.That(heading.text, Is.EqualTo("이 상황에서 자주 써요"));
            Assert.That(heading.font.name, Does.Contain("Jua"));
            Assert.That((Color32)heading.color, Is.EqualTo(muted));
            Assert.That(card.yMin - title.yMax, Is.EqualTo(18 * Px).Within(.5f));
            Assert.That(
                Rows().Max(row => Box((RectTransform)row.transform).yMax),
                Is.LessThan(title.yMin)
            );
        }

        [Test]
        public void SituationRow_ShowsMeaningAndUsageAfterThePhraseWithAPick()
        {
            Show();
            GameObject row = Rows().First();

            Assert.That(Text(row.transform, "Phrase"), Is.EqualTo("I left it on the table."));
            Assert.That(
                Text(row.transform, "Meaning"),
                Is.EqualTo("테이블에 두고 왔어요 · 물건 둔 곳을 말할 때")
            );
            Assert.That(ArtOf(row.GetComponentInChildren<Button>()), Is.EqualTo("FeedbackPick"));
        }

        // 담은 표현은 흰 [✓ 담음]이 되고, 다른 탭에 다녀와도 그대로다.
        [Test]
        public void SavedExpression_StaysSavedAfterSwitchingTabs()
        {
            Show();
            view.ShowSaved("That's a relief!");
            view.SelectSection(FeedbackSection.Corrections);
            view.SelectSection(FeedbackSection.Summary);

            Button pick = Rows()[2].GetComponentInChildren<Button>();
            Assert.That(ArtOf(pick), Is.EqualTo("FeedbackPicked"));
            Assert.That(pick.interactable, Is.False);
            Assert.That(
                ArtOf(Rows()[0].GetComponentInChildren<Button>()),
                Is.EqualTo("FeedbackPick")
            );
        }

        [Test]
        public void CorrectionsTab_ShowsOneCardPerUtteranceWithItsChips()
        {
            Show();
            view.SelectSection(FeedbackSection.Corrections);
            List<GameObject> cards = Cards();

            Assert.That(cards, Has.Count.EqualTo(3));
            Assert.That(
                Chips(cards[0]),
                Is.EqualTo(new[] { "FeedbackChipSaid", "FeedbackChipBetter" })
            );
            Assert.That(
                Chips(cards[1]),
                Is.EqualTo(new[] { "FeedbackChipSaidKorean", "FeedbackChipEnglish" })
            );
            Assert.That(Chips(cards[2]), Is.EqualTo(new[] { "FeedbackChipGood" }));
            TMP_Text corrected = cards[0].transform.Find("Said").GetComponent<TMP_Text>();
            TMP_Text korean = cards[1].transform.Find("Said").GetComponent<TMP_Text>();
            Assert.That(corrected.text, Is.EqualTo("I think lost my small bag in here"));
            Assert.That(
                corrected.fontStyle & FontStyles.Strikethrough,
                Is.Not.EqualTo(FontStyles.Normal)
            );
            Assert.That(korean.text, Is.EqualTo("가방 안에 여권 지갑이 있어요"));
            Assert.That(korean.fontStyle & FontStyles.Strikethrough, Is.EqualTo(FontStyles.Normal));
            Assert.That(
                Text(cards[1].transform, "Better"),
                Is.EqualTo("My passport wallet is in the bag.")
            );
            Assert.That(
                Text(cards[2].transform, "Said"),
                Is.EqualTo("Can I get an iced latte, please?")
            );
            Assert.That(Pick(cards[0]).gameObject.activeInHierarchy, Is.True);
            Assert.That(Pick(cards[1]).gameObject.activeInHierarchy, Is.True);
            Assert.That(
                Pick(cards[2]).gameObject.activeInHierarchy,
                Is.False,
                "고칠 것이 없으면 담을 문장도 없다."
            );
            for (int i = 1; i < cards.Count; i++)
            {
                Rect above = Box((RectTransform)cards[i - 1].transform);
                Rect below = Box((RectTransform)cards[i].transform);
                Assert.That(above.yMin - below.yMax, Is.EqualTo(12 * Px).Within(.01f));
            }
        }

        [Test]
        public void CorrectionCardPick_SavesTheEnglishSentenceAndShowsItSaved()
        {
            Show();
            view.SelectSection(FeedbackSection.Corrections);
            var saved = new List<string>();
            view.SaveRequested += (english, _) => saved.Add(english);

            Pick(Cards()[1]).onClick.Invoke();
            view.ShowSaved("My passport wallet is in the bag.");

            Assert.That(saved, Is.EqualTo(new[] { "My passport wallet is in the bag." }));
            Assert.That(ArtOf(Pick(Cards()[1])), Is.EqualTo("FeedbackPicked"));
            Assert.That(ArtOf(Pick(Cards()[0])), Is.EqualTo("FeedbackPick"));
        }

        // 전체 대화에서 한국어로 한 말을 누르면 카드와 같이 "영어로는"으로 보이고, 잘 표현했다고 칭찬하지 않는다.
        [TestCase("My passport wallet is in the bag.")]
        [TestCase(null)]
        public void KoreanSentenceDetail_IsLabelledLikeTheCorrectionCard(string english)
        {
            Show(english);
            view.SelectSection(FeedbackSection.Conversation);
            view.SelectReview(1);

            string[] headings = instance
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name == "Heading")
                .Select(text => text.text)
                .ToArray();

            Assert.That(headings, Does.Contain("내가 한 말 · 한국어"));
            Assert.That(headings.Contains("영어로는"), Is.EqualTo(english != null));
            Assert.That(headings, Does.Not.Contain("잘 표현했어요"));
            Assert.That(headings, Does.Not.Contain("자연스러웠던 점"));
            Assert.That(headings, Does.Not.Contain("이렇게 말해요"));
        }

        // 전체 대화 탭은 NPC와 사용자의 모든 발화를 순서대로 보여 준다.
        [Test]
        public void ConversationTab_ShowsTheWholeTranscript()
        {
            Show();
            view.SelectSection(FeedbackSection.Conversation);

            Assert.That(Rows(), Is.Empty);
            Transform cards = Panel().Find("PageCard/Content/ConversationCards");
            Assert.That(cards, Is.Not.Null);
            Assert.That(
                cards
                    .GetComponentsInChildren<TMP_Text>()
                    .Where(text => text.name == "Utterance")
                    .Select(text => text.text),
                Is.EqualTo(
                    new[]
                    {
                        "Hi again! Did you forget something?",
                        "I think lost my small bag in here",
                        "가방 안에 여권 지갑이 있어요",
                        "Can I get an iced latte, please?",
                    }
                )
            );
        }

        // 영어 표현 뒤에 자리가 있으면 뜻을 같은 줄에, 모자라면 다음 줄에 둔다.
        [TestCase(300, 200, 600, true)]
        [TestCase(500, 200, 600, false)]
        public void Meaning_FollowsThePhraseWhenItFits(
            float phrase,
            float meaning,
            float width,
            bool sameLine
        )
        {
            Assert.That(
                SituationRowLayout.MeaningOnSameLine(phrase, meaning, width),
                Is.EqualTo(sameLine)
            );
        }

        private void Show(string koreanInEnglish = "My passport wallet is in the bag.")
        {
            if (instance == null)
            {
                instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                );
                view = instance.GetComponentInChildren<FeedbackView>(true);
            }
            view.SetSaveAvailable(true);
            view.Show(
                ConversationFeedback.FromServer(
                    "Mia",
                    3,
                    new ServerFeedback(
                        new FeedbackData
                        {
                            Status = "DONE",
                            Summary = Summary,
                            Reviews = new FeedbackReviews
                            {
                                Good = new List<string>
                                {
                                    "가방의 색과 열쇠고리까지 자세히 말했어요.",
                                },
                                Improve = new List<string>
                                {
                                    "“I think lost”처럼 주어를 빠뜨린 문장이 있었어요.",
                                },
                            },
                            SituationExpressions = new List<SituationExpressionData>
                            {
                                Situation(
                                    "I left it on the table.",
                                    "테이블에 두고 왔어요",
                                    "물건 둔 곳을 말할 때"
                                ),
                                Situation(
                                    "Has anyone turned in a bag?",
                                    "혹시 가방 맡겨진 거 있나요?",
                                    "분실물 물을 때"
                                ),
                                Situation("That's a relief!", "다행이에요!", "찾았을 때"),
                            },
                        },
                        new List<DetailedFeedbackItem>
                        {
                            new DetailedFeedbackItem
                            {
                                UserText = "I think lost my small bag in here",
                                BetterText = "I think I lost my small bag in here.",
                                Reason = "lost 앞에도 주어 I를 넣어요.",
                            },
                            new DetailedFeedbackItem
                            {
                                UserText = "가방 안에 여권 지갑이 있어요",
                                BetterText = koreanInEnglish,
                                Reason = "한국어로 말한 문장을 영어로 바꿨어요.",
                            },
                            new DetailedFeedbackItem
                            {
                                UserText = "Can I get an iced latte, please?",
                                Reason = "Can I get ~ please?로 정중하게 주문했어요.",
                            },
                        },
                        null
                    )
                ),
                new List<DialogueLine>
                {
                    DialogueLine.FromNpc("Hi again! Did you forget something?", "또 오셨네요!"),
                    DialogueLine.FromUser("I think lost my small bag in here"),
                    DialogueLine.FromUser("가방 안에 여권 지갑이 있어요"),
                    DialogueLine.FromUser("Can I get an iced latte, please?"),
                }
            );
            Canvas.ForceUpdateCanvases();
        }

        private static SituationExpressionData Situation(
            string english,
            string meaning,
            string usage
        ) =>
            new SituationExpressionData
            {
                Expression = english,
                MeaningKo = meaning,
                UsageKo = usage,
            };

        private Transform Panel() => Field<GameObject>("panel").transform;

        // 판 안 좌표(왼쪽 아래 기준)의 상자. 판은 화면에 맞춰 줄어들므로 판 기준으로 비교한다.
        private Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = Panel().InverseTransformPoint(corners[0]);
            Vector3 max = Panel().InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private TMP_Text SituationTitle() =>
            Field<RectTransform>("situationRows").Find("Header").GetComponent<TMP_Text>();

        private List<GameObject> Rows() => Visible("SituationRow");

        private List<GameObject> Cards() => Visible("CorrectionCard");

        private List<GameObject> Visible(string name) =>
            instance
                .GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == name && child.gameObject.activeInHierarchy)
                .Select(child => child.gameObject)
                .ToList();

        private static string[] Chips(GameObject card) =>
            new[] { "SaidChip", "BetterChip" }
                .Select(name => card.transform.Find(name))
                .Where(chip => chip.gameObject.activeSelf)
                .Select(chip => chip.GetComponent<Image>().sprite.name)
                .ToArray();

        private static Button Pick(GameObject card) =>
            card.transform.Find("SaveButton").GetComponent<Button>();

        private static string ArtOf(Button button) =>
            button.transform.Find("Art").GetComponent<Image>().sprite.name;

        private static string IconOf(Button button) =>
            button.transform.Find("Icon").GetComponent<Image>().sprite.name;

        private static string Text(Transform parent, string path) =>
            parent.Find(path).GetComponent<TMP_Text>().text;

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;
    }
}
