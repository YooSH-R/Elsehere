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

namespace A305.Learning.PlayModeTests
{
    /// <summary>
    /// 요약 탭은 "이 상황에서 자주 써요" 표현 칸 아래에 대화 요약을 둔다. 표현이 많아 그 아래 요약이 다 들어가지 않으면
    /// 요약을 잘린 채 끼워 두지 않고, 표현 칸은 첫 장에 두고 요약은 다음 장에 온전히 보인다.
    /// </summary>
    public class FeedbackSummaryPagingTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string Summary =
            "카페 가는 길을 묻는 Ben에게 곧장 가서 모퉁이에서 왼쪽으로 돌라고 알려 줬어요.";

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.Destroy(instance);

        [UnityTest]
        public IEnumerator ManySituationRows_MoveTheSummaryToTheNextCard()
        {
            yield return Show(situationCount: 5);
            Assert.That(VisibleRows(), Has.Count.EqualTo(5), "준비: 첫 장의 표현 칸");
            TMP_Text body = Field<TMP_Text>("bodyText");
            Assert.That(
                !body.gameObject.activeInHierarchy
                    || !body.text.Contains(Summary)
                    || FitsWhole(body),
                Is.True,
                "표현 칸 아래에 요약이 잘린 채 끼어 있습니다."
            );

            view.NextPage();
            yield return null;

            Assert.That(VisibleRows(), Is.Empty, "요약 장에는 표현 칸을 두지 않습니다.");
            Assert.That(body.text, Does.Contain(Summary));
            Assert.That(FitsWhole(body), Is.True, "요약이 다 들어갑니다.");
        }

        [UnityTest]
        public IEnumerator FewSituationRows_KeepTheSummaryBelowThem()
        {
            yield return Show(situationCount: 2);

            TMP_Text body = Field<TMP_Text>("bodyText");
            Assert.That(VisibleRows(), Has.Count.EqualTo(2));
            Assert.That(body.text, Does.Contain(Summary));
            Assert.That(FitsWhole(body), Is.True);
        }

        private IEnumerator Show(int situationCount)
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
                            Summary = Summary,
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

        // 본문 칸이 글 전체를 담을 높이를 가졌는지. 모자라면 앞부분만 보이거나 아무것도 보이지 않는다.
        private static bool FitsWhole(TMP_Text body)
        {
            Canvas.ForceUpdateCanvases();
            float needed = body.GetPreferredValues(body.text, body.rectTransform.rect.width, 0).y;
            return body.rectTransform.rect.height + 1f >= needed;
        }

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
