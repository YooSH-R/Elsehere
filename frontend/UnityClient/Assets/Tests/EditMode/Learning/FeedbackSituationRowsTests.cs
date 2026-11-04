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
    /// 추천 표현은 회화 표현 탭에서만 보여 주고 각자 담으며, 요약 탭에는 대화 요약만 남는지 확인한다.
    /// 회화 탭을 눌러 한 장씩 넘겨야 보이면 잘 와닿지 않는다.
    /// </summary>
    public class FeedbackSituationRowsTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";

        private static readonly List<SituationExpressionData> Expressions =
            new List<SituationExpressionData>
            {
                Situation(
                    "Could you hold my luggage?",
                    "짐 좀 맡아 주시겠어요?",
                    "체크인 전에 짐을 맡길 때"
                ),
                Situation("Is early check-in possible?", "얼리 체크인 되나요?", "일찍 도착했을 때"),
                Situation(
                    "Is there a cafe nearby?",
                    "근처에 카페가 있나요?",
                    "기다릴 곳을 찾을 때"
                ),
            };

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.DestroyImmediate(instance);

        [Test]
        public void ConversationTab_ShowsEverySituationExpressionAtOnce()
        {
            Show(Expressions);
            view.SelectSection(FeedbackSection.Conversation);

            List<GameObject> rows = Rows();
            Assert.That(rows, Has.Count.EqualTo(3));
            for (int i = 0; i < rows.Count; i++)
            {
                string text = Texts(rows[i]);
                Assert.That(text, Does.Contain(Expressions[i].Expression));
                Assert.That(text, Does.Contain(Expressions[i].MeaningKo));
                Assert.That(text, Does.Contain(Expressions[i].UsageKo));
            }
        }

        // 표현마다 [+ 담기]를 눌러 그 표현과 뜻을 단어장에 담는다.
        [Test]
        public void RowSaveButton_SavesThatExpressionWithItsMeaning()
        {
            Show(Expressions);
            view.SelectSection(FeedbackSection.Conversation);
            var saved = new List<(string English, string Korean)>();
            view.SaveRequested += (english, korean) => saved.Add((english, korean));

            Rows()[1].GetComponentInChildren<Button>().onClick.Invoke();

            Assert.That(
                saved,
                Is.EqualTo(new[] { ("Is early check-in possible?", "얼리 체크인 되나요?") })
            );
        }

        // 추천 표현은 회화 표현 탭에만 둔다.
        [TestCase(FeedbackSection.Summary)]
        [TestCase(FeedbackSection.Corrections)]
        [TestCase(FeedbackSection.Expressions)]
        public void OtherTabs_HideTheSituationRows(FeedbackSection section)
        {
            Show(Expressions);

            view.SelectSection(section);

            Assert.That(Rows(), Is.Empty);
        }

        [Test]
        public void WithoutSituationExpressions_SummaryShowsNoRows()
        {
            Show(new List<SituationExpressionData>());

            Assert.That(Rows(), Is.Empty);
        }

        // 추천 표현 때문에 대화 요약이 아래로 밀리거나 페이지가 추가되지 않는다.
        [Test]
        public void SummaryTab_ShowsTheConversationSummaryWithoutExpressionRows()
        {
            Show(Expressions);
            var body = (TMP_Text)
                new SerializedObject(view).FindProperty("bodyText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();

            Assert.That(Rows(), Is.Empty);
            Assert.That(body.text, Does.Contain("예약 확인부터 캐리어 보관까지"));
            var heading = (TMP_Text)
                new SerializedObject(view).FindProperty("summaryTitle").objectReferenceValue;
            Assert.That(heading.gameObject.activeInHierarchy, Is.True);
        }

        // 다른 탭에서 긴 추천 표현을 본 뒤 돌아와도 요약에는 추천 표현을 반복하지 않는다.
        [Test]
        public void ReturningFromConversationTab_KeepsOnlyTheSummary()
        {
            Show(
                new List<SituationExpressionData>
                {
                    Situation(
                        "Do you know where I can buy an umbrella?",
                        "우산을 어디서 살 수 있는지 아세요?",
                        "비가 와서 우산을 살 곳을 행인에게 정중히 물을 때 씁니다."
                    ),
                    Situation(
                        "Is there a drugstore nearby?",
                        "근처에 약국이 있나요?",
                        "약국의 위치를 간단히 물을 때 쓸 수 있습니다."
                    ),
                }
            );
            view.SelectSection(FeedbackSection.Conversation);
            Assert.That(Rows(), Has.Count.EqualTo(2));
            view.SelectSection(FeedbackSection.Summary);
            var body = (TMP_Text)
                new SerializedObject(view).FindProperty("bodyText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();

            Assert.That(Rows(), Is.Empty);
            Assert.That(body.text, Does.Contain("예약 확인부터 캐리어 보관까지"));
        }

        [Test]
        public void SingleSituationExpression_HasOnlyItsOwnSaveButton()
        {
            Show(new List<SituationExpressionData> { Expressions[0] });
            view.SelectSection(FeedbackSection.Conversation);

            var pageSave = (Button)
                new SerializedObject(view).FindProperty("saveButton").objectReferenceValue;
            Assert.That(pageSave.gameObject.activeInHierarchy, Is.False);
            Assert.That(Rows().Single().GetComponentInChildren<Button>(), Is.Not.Null);
        }

        private void Show(List<SituationExpressionData> expressions)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponentInChildren<FeedbackView>(true);
            view.Show(
                ConversationFeedback.FromServer(
                    "Jake",
                    3,
                    new ServerFeedback(
                        new FeedbackData
                        {
                            Status = "DONE",
                            Summary = "예약 확인부터 캐리어 보관까지 차례대로 잘 요청했어요.",
                            SituationExpressions = expressions,
                        },
                        new List<DetailedFeedbackItem>(),
                        null
                    )
                ),
                new List<DialogueLine>()
            );
        }

        // 화면에 보이는 표현 줄. 복사해 쓰는 원본 줄은 숨겨 두므로 빠진다.
        private List<GameObject> Rows() =>
            instance
                .GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == "SituationRow" && child.gameObject.activeInHierarchy)
                .Select(child => child.gameObject)
                .ToList();

        private static string Texts(GameObject row) =>
            string.Join("\n", row.GetComponentsInChildren<TMP_Text>().Select(text => text.text));

        private static SituationExpressionData Situation(
            string english,
            string korean,
            string usage
        ) =>
            new SituationExpressionData
            {
                Expression = english,
                MeaningKo = korean,
                UsageKo = usage,
                ExampleEn = english,
                ExampleKo = korean,
            };
    }
}
