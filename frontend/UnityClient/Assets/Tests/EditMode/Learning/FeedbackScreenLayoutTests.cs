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
    /// 대화 피드백 화면은 확정 시안(g10·g11을 읽기 쉽게 다듬은 것)이다. 판은 뒤가 비치는 유리이고,
    /// 글자가 놓이는 칸만 진한 흰 유리(요약은 따뜻한 색)로 띄운다. "이 상황에서 자주 써요"는 요약·회화 표현 탭 모두 같은 코랄 제목과 같은 칸이고,
    /// 표현마다 글자만 있는 [담기]를 둔다.
    /// </summary>
    public class FeedbackScreenLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private static readonly Color Coral = new Color32(242, 99, 63, 255);

        private GameObject instance;
        private FeedbackView view;

        [TearDown]
        public void DestroyScreen() => Object.DestroyImmediate(instance);

        [Test]
        public void Header_ShowsTheMockupTitleAndTabNames()
        {
            Show();

            Assert.That(Field<TMP_Text>("titleText").text, Is.EqualTo("대화 피드백"));
            Assert.That(
                Field<RectTransform>("sectionRow")
                    .GetComponentsInChildren<Button>(true)
                    .Select(tab => tab.GetComponentInChildren<TMP_Text>(true).text),
                Is.EqualTo(new[] { "요약", "문장 교정", "회화 표현", "저장할 표현" })
            );
            Assert.That(
                Panel().Find("AppIcon").GetComponent<Image>().sprite.name,
                Is.EqualTo("FeedbackIcon")
            );
        }

        [Test]
        public void SituationTitle_IsCoralOnBothTabs()
        {
            Show();
            TMP_Text summaryTitle = SituationTitle();
            view.SelectSection(FeedbackSection.Conversation);
            TMP_Text conversationTitle = SituationTitle();

            Assert.That(summaryTitle, Is.SameAs(conversationTitle), "같은 제목을 씁니다.");
            Assert.That(conversationTitle.gameObject.activeInHierarchy, Is.True);
            Assert.That(conversationTitle.color, Is.EqualTo(Coral));
            Assert.That(conversationTitle.font.name, Does.Contain("Jua"));
        }

        [Test]
        public void SituationRows_AreRaisedTilesWithATextOnlySaveButton()
        {
            Show();
            GameObject row = Rows().First();

            Assert.That(
                row.transform.Find("Tile").GetComponent<Image>().sprite.name,
                Is.EqualTo("FeedbackTile")
            );
            Button save = row.GetComponentInChildren<Button>();
            Assert.That(save.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("담기"));
            Image icon = save.transform.Find("Icon")?.GetComponent<Image>();
            Assert.That(
                icon == null || !icon.enabled || icon.sprite == null,
                Is.True,
                "아이콘 없이 글자만"
            );
        }

        // 회화 표현 탭도 요약 탭과 같은 표현 칸을 보이고, 예문을 한 줄 더 둔다. 굵은 글자 본문으로 따로 쓰지 않는다.
        [Test]
        public void ConversationTab_ShowsTheSameRowsWithExamples()
        {
            Show();
            view.SelectSection(FeedbackSection.Conversation);

            List<GameObject> rows = Rows();
            Assert.That(rows, Has.Count.EqualTo(2));
            string texts = string.Join(
                "\n",
                rows[0].GetComponentsInChildren<TMP_Text>().Select(text => text.text)
            );
            Assert.That(
                texts,
                Does.Contain("Could you tell me how to get to the hotel?").And.Contain("길을 알려")
            );
            Assert.That(texts, Does.Contain("I'm looking for the hotel."));
            Assert.That(
                Field<TMP_Text>("bodyText").text,
                Does.Not.Contain("이 상황에서 자주 써요")
            );
        }

        [Test]
        public void SummaryTab_PutsTheSummaryInAWarmTile()
        {
            Show();
            Canvas.ForceUpdateCanvases();
            Transform tile = Panel().Find("PageCard/BodyTile");

            Assert.That(tile.gameObject.activeInHierarchy, Is.True);
            Assert.That(tile.GetComponent<Image>().sprite.name, Is.EqualTo("FeedbackSummaryTile"));
            view.SelectSection(FeedbackSection.Corrections);
            Assert.That(
                tile.GetComponent<Image>().sprite.name,
                Is.EqualTo("FeedbackTile"),
                "다른 탭 글은 흰 칸"
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

        private void Show()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponentInChildren<FeedbackView>(true);
            view.Show(
                ConversationFeedback.FromServer(
                    "Ben",
                    2,
                    new ServerFeedback(
                        new FeedbackData
                        {
                            Status = "DONE",
                            Summary = "호텔 위치를 물었고, 행인 Ben은 위치를 모른다고 답했어요.",
                            Reviews = new FeedbackReviews
                            {
                                Good = new List<string> { "위치 질문 전달" },
                            },
                            SituationExpressions = new List<SituationExpressionData>
                            {
                                new SituationExpressionData
                                {
                                    Expression = "Could you tell me how to get to the hotel?",
                                    MeaningKo = "호텔까지 가는 길을 알려 주실 수 있나요?",
                                    UsageKo = "가는 방법을 물을 때 써요.",
                                    ExampleEn = "I'm looking for the hotel.",
                                    ExampleKo = "호텔을 찾고 있어요.",
                                },
                                new SituationExpressionData
                                {
                                    Expression = "Is the hotel nearby?",
                                    MeaningKo = "호텔이 근처에 있나요?",
                                    UsageKo = "가까운지 확인할 때 써요.",
                                },
                            },
                        },
                        new List<DetailedFeedbackItem>(),
                        null
                    )
                ),
                new List<DialogueLine>()
            );
        }

        private Transform Panel() => Field<GameObject>("panel").transform;

        private TMP_Text SituationTitle() =>
            Field<RectTransform>("situationRows").Find("Header").GetComponent<TMP_Text>();

        private List<GameObject> Rows() =>
            instance
                .GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == "SituationRow" && child.gameObject.activeInHierarchy)
                .Select(child => child.gameObject)
                .ToList();

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;
    }
}
