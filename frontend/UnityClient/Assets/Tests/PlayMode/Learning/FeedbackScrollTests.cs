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
                Assert.That(shown.Any(text => text.text == "잘 표현했어요"), Is.True);
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
            Assert.That(
                Field<TMP_Text>("bodyText").overflowMode,
                Is.EqualTo(TextOverflowModes.Overflow)
            );
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            AssertSummaryShownWhole();
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FewSituationRows_KeepTheSummaryBelowThem()
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

        // 요약 칸이 짧은 요약 글 전체를 담는지. 모자라면 앞부분만 보이거나 아무것도 보이지 않는다.
        private void AssertSummaryShownWhole()
        {
            TMP_Text body = Field<TMP_Text>("bodyText");
            Canvas.ForceUpdateCanvases();
            float needed = body.GetPreferredValues(body.text, body.rectTransform.rect.width, 0).y;
            Assert.That(body.text, Does.Contain(Summary));
            Assert.That(body.rectTransform.rect.height + 1f, Is.GreaterThanOrEqualTo(needed));
        }

        private static string RowText(GameObject row) =>
            string.Join(" ", row.GetComponentsInChildren<TMP_Text>().Select(text => text.text));

        private List<GameObject> VisibleRows() =>
            instance
                .GetComponentsInChildren<Transform>()
                .Where(child => child.name == "SituationRow")
                .Select(child => child.gameObject)
                .ToList();

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;
    }
}
#endif
