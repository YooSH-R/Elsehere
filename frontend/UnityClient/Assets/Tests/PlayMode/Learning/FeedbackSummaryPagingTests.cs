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
    /// 요약 탭은 위 "이 상황에서 자주 써요"와 아래 "대화 요약"으로 나뉘고, 두 칸은 제목 줄 오른쪽의 작은 쪽 넘김으로 따로 넘긴다.
    /// 표현은 한 장에 두 개씩 보이고, 요약 글이 길면 요약 칸 안에서만 넘긴다. 아래쪽 쪽 넘김 줄은 요약 탭에서 숨긴다.
    /// </summary>
    public class FeedbackSummaryPagingTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string Summary = "카페 가는 길을 묻는 Ben에게 곧장 가서 모퉁이에서 왼쪽으로 돌라고 알려 줬어요.";

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.Destroy(instance);

        [UnityTest]
        public IEnumerator ManySituations_PageTwoAtATime_WithTheirOwnArrows()
        {
            yield return Show(situationCount: 5, summary: Summary);
            RectTransform situations = Field<RectTransform>("situationPager");

            Assert.That(VisibleRows(), Has.Count.EqualTo(2), "표현은 한 장에 두 개씩");
            Assert.That(PageText(situations), Is.EqualTo("1 / 3"));
            Assert.That(
                Field<RectTransform>("navigationRow").gameObject.activeInHierarchy,
                Is.False,
                "요약 탭에서는 아래쪽 쪽 넘김 줄을 숨깁니다."
            );
            AssertSummaryShownWhole();

            ClickNext(situations);
            yield return null;

            Assert.That(PageText(situations), Is.EqualTo("2 / 3"));
            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
            Assert.That(RowText(VisibleRows()[0]), Does.Contain("number 3"));
            AssertSummaryShownWhole();
        }

        [UnityTest]
        public IEnumerator LongSummary_PagesInsideItsOwnBox()
        {
            string longSummary = string.Join(" ", Enumerable.Repeat(Summary, 12));
            yield return Show(situationCount: 2, summary: longSummary);
            RectTransform situations = Field<RectTransform>("situationPager");
            RectTransform summary = Field<RectTransform>("summaryPager");
            string[] firstPage = PageText(summary).Split('/');

            Assert.That(PageText(situations), Is.EqualTo("1 / 1"));
            Assert.That(firstPage[0].Trim(), Is.EqualTo("1"));
            Assert.That(int.Parse(firstPage[1].Trim()), Is.GreaterThan(1), "긴 요약은 여러 장");

            ClickNext(summary);
            yield return null;

            Assert.That(PageText(summary), Does.StartWith("2 /"));
            Assert.That(PageText(situations), Is.EqualTo("1 / 1"), "표현 칸은 그대로");
            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FewSituationRows_KeepTheSummaryBelowThem()
        {
            yield return Show(situationCount: 2, summary: Summary);

            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
            Assert.That(PageText(Field<RectTransform>("summaryPager")), Is.EqualTo("1 / 1"));
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

        private static string PageText(RectTransform pager) =>
            pager.Find("PageNumber").GetComponent<TMP_Text>().text;

        private static void ClickNext(RectTransform pager) =>
            pager.Find("NextButton").GetComponent<Button>().onClick.Invoke();

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
