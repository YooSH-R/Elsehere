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
    /// 피드백을 열자마자 보이는 요약 탭에 이 상황에서 자주 쓰는 표현을 한 번에 보여 주고, 표현마다 바로 담을 수 있는지 확인한다.
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
        public void SummaryTab_ShowsEverySituationExpressionAtOnce()
        {
            Show(Expressions);

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
            var saved = new List<(string English, string Korean)>();
            view.SaveRequested += (english, korean) => saved.Add((english, korean));

            Rows()[1].GetComponentInChildren<Button>().onClick.Invoke();

            Assert.That(
                saved,
                Is.EqualTo(new[] { ("Is early check-in possible?", "얼리 체크인 되나요?") })
            );
        }

        // 회화 표현 탭은 같은 표현 칸을 보인다(FeedbackScreenLayoutTests). 문장 교정·저장할 표현 탭에는 없다.
        [TestCase(FeedbackSection.Corrections)]
        [TestCase(FeedbackSection.Expressions)]
        public void CorrectionsAndExpressionsTabs_HideTheSituationRows(FeedbackSection section)
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

        // 요약 글은 표현 줄 아래에서 시작해 표현 줄과 겹치지 않는다.
        [Test]
        public void SummaryText_StartsBelowTheSituationRows()
        {
            Show(Expressions);
            var body = (TMP_Text)
                new SerializedObject(view).FindProperty("bodyText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();

            float rowsBottom = Rows().Min(row => Corners((RectTransform)row.transform)[0].y);
            Assert.That(Corners(body.rectTransform)[1].y, Is.LessThanOrEqualTo(rowsBottom + 0.01f));
        }

        // 표현이 길어 두 줄로 넘어가도 처음 열 때부터 요약 글이 표현 줄 아래에서 시작한다.
        // 다른 탭에 다녀와야 제자리로 내려가면 처음 보는 요약 글 첫 줄이 표현 줄에 가린다.
        [Test]
        public void SummaryText_StartsBelowWrappedSituationRows_OnFirstShow()
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
            var body = (TMP_Text)
                new SerializedObject(view).FindProperty("bodyText").objectReferenceValue;
            Canvas.ForceUpdateCanvases();

            float rowsBottom = Rows().Min(row => Corners((RectTransform)row.transform)[0].y);
            Assert.That(Corners(body.rectTransform)[1].y, Is.LessThanOrEqualTo(rowsBottom + 0.01f));
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

        private static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

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
