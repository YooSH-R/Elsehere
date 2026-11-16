#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    /// <summary>
    /// 긴 피드백은 고정된 본문 영역 안에서 스크롤한다. 표현을 잘라 숨기지 않고 하단 버튼은 본문 밖에 둔다.
    /// </summary>
    public class FeedbackScrollTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string Summary =
            "카페 가는 길을 묻는 Ben에게 곧장 가서 모퉁이에서 왼쪽으로 돌라고 알려 줬어요.";

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.Destroy(instance);

        [UnityTest]
        public IEnumerator EveryFeedbackSection_CanExpandAndRestoreWithoutLosingScroll()
        {
            yield return Show(
                situationCount: 5,
                summary: string.Join(" ", Enumerable.Repeat(Summary, 12))
            );
            foreach (FeedbackSection section in System.Enum.GetValues(typeof(FeedbackSection)))
            {
                view.SelectSection(section);
                yield return null;
                ScrollRect scroll = instance.GetComponentInChildren<ScrollRect>();
                Vector2 initialSize = view.PanelRect.rect.size;
                scroll.verticalNormalizedPosition = .35f;
                var expand = instance
                    .GetComponentsInChildren<Button>()
                    .SingleOrDefault(button => button.name == "ExpandButton");
                Assert.That(expand, Is.Not.Null, "모든 탭에 확대 버튼이 있어야 합니다.");
                expand.onClick.Invoke();
                yield return null;
                Assert.That(view.PanelRect.rect.width, Is.GreaterThan(initialSize.x));
                Assert.That(view.PanelRect.rect.height, Is.GreaterThan(initialSize.y));
                if (scroll.content.rect.height > scroll.viewport.rect.height)
                    Assert.That(
                        scroll.verticalNormalizedPosition,
                        Is.EqualTo(.35f).Within(.02f),
                        section.ToString()
                    );
                Assert.That(view.CloseButtonRect.gameObject.activeInHierarchy, Is.True);
                expand.onClick.Invoke();
                yield return null;
                Assert.That(view.PanelRect.rect.size.x, Is.EqualTo(initialSize.x).Within(1f));
            }
        }

        [UnityTest]
        public IEnumerator NaturalSentence_ShowsOnceWithoutAFakeCorrectionCard()
        {
            const string sentence = "I have a reservation under Kim.";
            foreach (string better in new string[] { null, sentence })
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                );
                view = instance.GetComponentInChildren<FeedbackView>(true);
                view.Show(
                    ConversationFeedback.FromServer(
                        "Jake",
                        1,
                        new ServerFeedback(
                            new FeedbackData { Status = "DONE", Summary = "예약 확인" },
                            new List<DetailedFeedbackItem>
                            {
                                new DetailedFeedbackItem
                                {
                                    UserText = sentence,
                                    BetterText = better,
                                    Reason = "예약자 이름을 자연스럽게 전달했어요.",
                                },
                            },
                            null
                        )
                    ),
                    new[] { DialogueLine.FromUser(sentence) }
                );
                view.SelectSection(FeedbackSection.Conversation);
                view.SelectReview(0);
                yield return null;
                yield return null;
                TMP_Text[] shown = instance
                    .GetComponentsInChildren<TMP_Text>()
                    .Where(text => text.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(shown.Count(text => text.text == sentence), Is.EqualTo(1));
                Assert.That(shown.Any(text => text.text == "이렇게 말해요"), Is.False);
                // 문장 교정 탭과 같은 카드에 초록 "잘했어요" 칩을 붙인다.
                Assert.That(
                    ShownCorrectionCard().Find("SaidChip").GetComponent<Image>().sprite,
                    Is.EqualTo(Field<Sprite>("goodChipSprite"))
                );
            }
        }

        [UnityTest]
        public IEnumerator SentenceWithoutExamples_UsesTheCorrectionCardInTheConversation()
        {
            const string said = "너 외로워?";
            const string english = "Are you lonely?";
            const string idiom = "Feeling a bit lonely?";
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponentInChildren<FeedbackView>(true);
            var saved = new List<string>();
            view.SaveRequested += (sentence, meaning) => saved.Add(sentence);
            view.SetSaveAvailable(true);
            view.Show(
                ConversationFeedback.FromServer(
                    "Jake",
                    1,
                    new ServerFeedback(
                        new FeedbackData { Status = "DONE" },
                        new List<DetailedFeedbackItem>
                        {
                            new DetailedFeedbackItem
                            {
                                UserText = said,
                                BetterText = english,
                                Reason = "외로운지 물을 때는 Are you lonely?라고 하면 돼요.",
                                Idiom = new IdiomData { Text = idiom, MeaningKo = "좀 외로워?" },
                            },
                        },
                        null
                    )
                ),
                new[] { DialogueLine.FromUser(said) }
            );
            view.SelectSection(FeedbackSection.Conversation);
            view.SelectReview(0);
            yield return null;
            yield return null;

            // 상황별 예문이 없는 문장도 문장 교정 탭과 같은 카드 한 장으로 보인다.
            var card = (RectTransform)ShownCorrectionCard();
            TMP_Text[] shown = instance
                .GetComponentsInChildren<TMP_Text>()
                .Where(text => text.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(
                shown.Any(text => text.text == "영어로는" || text.text == "이 표현을 쓰는 이유"),
                Is.False,
                "예전 칸 배치를 함께 그리지 않습니다."
            );
            Button pick = card.Find("SaveButton").GetComponent<Button>();
            Assert.That(pick.isActiveAndEnabled, Is.True);
            pick.onClick.Invoke();
            Assert.That(saved, Is.EqualTo(new[] { english }));

            // 관용 표현 칸은 카드 아래에 남긴다.
            TMP_Text idiomText = shown.SingleOrDefault(text => text.text == idiom);
            Assert.That(idiomText, Is.Not.Null);
            var cardCorners = new Vector3[4];
            var idiomCorners = new Vector3[4];
            card.GetWorldCorners(cardCorners);
            idiomText.rectTransform.GetWorldCorners(idiomCorners);
            Assert.That(idiomCorners[1].y, Is.LessThanOrEqualTo(cardCorners[0].y + .01f));
        }

        [UnityTest]
        public IEnumerator ConditionalExamples_UseSentenceStyleAndSaveTheSelectedMeaning()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponentInChildren<FeedbackView>(true);
            var examples = new List<FeedbackExpressionExample>
            {
                new FeedbackExpressionExample
                {
                    ConditionKo = "호텔에서 우산을 빌리고 싶을 때",
                    Expression = "Could I borrow an umbrella, please?",
                    MeaningKo = "우산을 빌릴 수 있을까요?",
                },
                new FeedbackExpressionExample
                {
                    ConditionKo = "우산을 살 수 있는 곳을 물을 때",
                    Expression = "Where can I buy an umbrella?",
                    MeaningKo = "우산을 어디에서 살 수 있나요?",
                },
            };
            var saved = new List<KeyValuePair<string, string>>();
            view.SaveRequested += (english, meaning) =>
                saved.Add(new KeyValuePair<string, string>(english, meaning));
            view.Show(
                ConversationFeedback.FromServer(
                    "Jake",
                    1,
                    new ServerFeedback(
                        new FeedbackData { Status = "DONE" },
                        new List<DetailedFeedbackItem>
                        {
                            new DetailedFeedbackItem
                            {
                                UserText = "umbrella please",
                                Verdict = "needs_clarification",
                                Reason = "빌릴지 판매처를 물을지 분명하게 말해 보세요.",
                                ExpressionExamples = examples,
                            },
                        },
                        null
                    )
                ),
                new[] { DialogueLine.FromUser("umbrella please") }
            );
            view.SelectSection(FeedbackSection.Corrections);
            yield return null;
            yield return null;

            TMP_Text template = Field<RectTransform>("correctionCardTemplate")
                .Find("Better")
                .GetComponent<TMP_Text>();
            foreach (FeedbackExpressionExample example in examples)
            {
                TMP_Text[] shown = instance.GetComponentsInChildren<TMP_Text>();
                TMP_Text english = shown.SingleOrDefault(text => text.text == example.Expression);
                Assert.That(english, Is.Not.Null, "영어 예문을 회색 설명 본문에 합치지 않습니다.");
                Assert.That(english.font, Is.EqualTo(template.font));
                Assert.That(english.fontSize, Is.EqualTo(template.fontSize).Within(.01f));
                Assert.That(shown.Any(text => text.text == example.MeaningKo), Is.True);
            }
            Button[] picks = instance
                .GetComponentsInChildren<Button>()
                .Where(button => button.name == "SaveButton" && button.isActiveAndEnabled)
                .OrderByDescending(button => button.transform.position.y)
                .ToArray();
            Assert.That(picks, Has.Length.EqualTo(2));
            picks[0].onClick.Invoke();
            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].Key, Is.EqualTo(examples[0].Expression));
            Assert.That(saved[0].Value, Is.EqualTo(examples[0].MeaningKo));

            view.SelectSection(FeedbackSection.Conversation);
            view.SelectReview(0);
            yield return null;
            yield return null;
            foreach (FeedbackExpressionExample example in examples)
            {
                TMP_Text english = instance
                    .GetComponentsInChildren<TMP_Text>()
                    .SingleOrDefault(text => text.text == example.Expression);
                Assert.That(
                    english,
                    Is.Not.Null,
                    "전체 대화의 발화 피드백에서도 예문을 분리합니다."
                );
                Assert.That(english.font, Is.EqualTo(template.font));
                Assert.That(english.fontSize, Is.GreaterThanOrEqualTo(template.fontSize - 1));
            }
        }

        [UnityTest]
        public IEnumerator ManySituations_AllRemainInTheScrollableContent()
        {
            yield return Show(situationCount: 5, summary: Summary);
            Assert.That(
                VisibleRows(),
                Has.Count.EqualTo(5),
                "스크롤로 모든 표현을 볼 수 있어야 합니다."
            );
            Assert.That(
                Field<RectTransform>("navigationRow").gameObject.activeInHierarchy,
                Is.False,
                "스크롤 화면에 쪽 넘김을 함께 보이지 않습니다."
            );
            AssertSummaryShownWhole();

            ScrollRect scroll = instance.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(RowText(VisibleRows()[4]), Does.Contain("number 5"));
            AssertSummaryShownWhole();
        }

        [UnityTest]
        public IEnumerator LongSummary_HasItsFullHeightInsideTheScrollView()
        {
            string longSummary = string.Join(" ", Enumerable.Repeat(Summary, 12));
            yield return Show(situationCount: 2, summary: longSummary);
            ScrollRect scroll = instance.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(SummaryText().overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            AssertSummaryShownWhole();
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FewSituationRows_KeepTheWholeSummaryAboveThem()
        {
            yield return Show(situationCount: 2, summary: Summary);

            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
            AssertSummaryShownWhole();
        }

        private IEnumerator Show(int situationCount, string summary)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponentInChildren<FeedbackView>(true);
            var situations = new List<SituationExpressionData>();
            for (int i = 0; i < situationCount; i++)
            {
                situations.Add(
                    new SituationExpressionData
                    {
                        Expression = $"It's on your right, number {i + 1}.",
                        MeaningKo = "오른쪽에 있어요.",
                        UsageKo = "목적지가 어느 쪽에 있는지 알려 줄 때 써요.",
                        ExampleEn = "Turn left at the corner. It's on your right.",
                        ExampleKo = "모퉁이에서 왼쪽으로 도세요. 오른쪽에 있어요.",
                    }
                );
            }

            view.Show(
                ConversationFeedback.FromServer(
                    "Ben",
                    3,
                    new ServerFeedback(
                        new FeedbackData
                        {
                            Status = "DONE",
                            Summary = summary,
                            Reviews = new FeedbackReviews
                            {
                                Good = new List<string> { "길을 순서대로 알려 줬어요." },
                            },
                            SituationExpressions = situations,
                        },
                        new List<DetailedFeedbackItem>(),
                        null
                    )
                ),
                new List<DialogueLine>()
            );
            yield return null;
            yield return null;
        }

        // 요약 카드가 요약 글 전체를 담는지. 모자라면 앞부분만 보이거나 아무것도 보이지 않는다.
        private void AssertSummaryShownWhole()
        {
            TMP_Text text = SummaryText();
            Canvas.ForceUpdateCanvases();
            float needed = text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0).y;
            Assert.That(text.text, Does.Contain(Summary));
            Assert.That(text.rectTransform.rect.height + 1f, Is.GreaterThanOrEqualTo(needed));
        }

        private TMP_Text SummaryText() =>
            Field<RectTransform>("summaryCard").Find("Text").GetComponent<TMP_Text>();

        private static string RowText(GameObject row) =>
            string.Join(" ", row.GetComponentsInChildren<TMP_Text>().Select(text => text.text));

        private List<GameObject> VisibleRows() =>
            instance
                .GetComponentsInChildren<Transform>()
                .Where(child => child.name == "SituationRow")
                .Select(child => child.gameObject)
                .ToList();

        private Transform ShownCorrectionCard()
        {
            Transform card = instance
                .GetComponentsInChildren<Transform>()
                .SingleOrDefault(child => child.name == "CorrectionCard");
            Assert.That(card, Is.Not.Null, "문장 교정 탭과 같은 카드로 보여야 합니다.");
            return card;
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;
    }
}
#endif
