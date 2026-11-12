#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Dialogue.PlayModeTests
{
    public class DialoguePanelTranslationTests
    {
        private GameObject instance;
        private DialoguePanelView view;

        [SetUp]
        public void SetUp()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Dialogue/DialoguePanel.prefab"
                )
            );
            instance.GetComponent<CanvasScaler>().enabled = false;
            instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            view = instance.GetComponent<DialoguePanelView>();
        }

        [TearDown]
        public void TearDown()
        {
            instance.SetActive(false);
            Object.Destroy(instance);
            if (eventSystem != null)
                Object.Destroy(eventSystem);
        }

        [UnityTest]
        public IEnumerator Translation_ShowsBesideTheConversation_WithTheApprovedCaptionAndSmallText()
        {
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            Transform card = instance.transform.Find("Panel/UserTranslation");
            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                card.Find("Heading").GetComponent<TMP_Text>().text,
                Is.EqualTo("방금 한 말의 영어 표현이에요.")
            );
            var expression = card.Find("Viewport/Expression").GetComponent<TMP_Text>();
            Assert.That(expression.text, Is.EqualTo("Where is the cafe?"));
            var lines = instance
                .transform.Find("Panel/Conversation/History")
                .GetComponentInChildren<TMP_Text>(true);
            Assert.That(expression.fontSize, Is.LessThanOrEqualTo(lines.fontSize));
            Assert.That(lines.text, Does.Contain("카페 어디야?"));
            Assert.That(card.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
            Assert.That(
                card.Find("ListenButton").GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("듣기")
            );
            var panel = (RectTransform)card.parent;
            Rect cardArea = InPanel((RectTransform)card, panel);
            Rect historyArea = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/History"),
                panel
            );
            Assert.That(cardArea.xMin, Is.GreaterThan(historyArea.xMax));
            Assert.That(cardArea.Overlaps(historyArea), Is.False);
        }

        // 영어로 한 말에 온 표현은 교정문이므로, 고친 문장을 다시 말해 보도록 제목을 바꾼다.
        [UnityTest]
        public IEnumerator CorrectionOfEnglish_AsksToSayTheFixedSentenceAgain()
        {
            var content = new Content
            {
                UserLine = "I eat here. No onion please.",
                UserTranslation = "I'll eat here. No onions, please.",
            };
            view.Show(content);
            yield return null;
            var heading = instance
                .transform.Find("Panel/UserTranslation/Heading")
                .GetComponent<TMP_Text>();
            Assert.That(heading.text, Is.EqualTo("이렇게 고쳐 말해 보세요."));

            content.UserLine = "여기서 먹을게요.";
            content.UserTranslation = "I'll eat here.";
            view.Refresh();
            Assert.That(heading.text, Is.EqualTo("방금 한 말의 영어 표현이에요."));
        }

        [UnityTest]
        public IEnumerator Listen_ReadsOnlyTheCurrentExpressionWithoutSendingIt()
        {
            string spoken = null;
            int sends = 0;
            view.UserTranslationListenRequested += text => spoken = text;
            view.SendRequested += text => sends++;
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            instance
                .transform.Find("Panel/UserTranslation/ListenButton")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(spoken, Is.EqualTo("Where is the cafe?"));
            Assert.That(sends, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EmptyTranslationOrAnotherConversation_HidesThePreviousCard()
        {
            var content = new Content { UserTranslation = "Where is the cafe?" };
            view.Show(content);
            yield return null;
            content.UserTranslation = null;
            view.Refresh();
            Assert.That(
                instance.transform.Find("Panel/UserTranslation").gameObject.activeSelf,
                Is.False
            );
            view.Show(new Content());
            yield return null;
            Assert.That(
                instance.transform.Find("Panel/UserTranslation").gameObject.activeSelf,
                Is.False
            );
        }

        [UnityTest]
        public IEnumerator NarrowScreen_StacksTheCardAboveTheConversationWithoutOverlap()
        {
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 1400);
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            Transform card = instance.transform.Find("Panel/UserTranslation");
            var panel = (RectTransform)card.parent;
            Rect cardArea = InPanel((RectTransform)card, panel);
            Rect historyArea = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/History"),
                panel
            );
            Assert.That(cardArea.yMin, Is.GreaterThan(historyArea.yMax));
            Assert.That(cardArea.Overlaps(historyArea), Is.False);
            Assert.That(cardArea.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin));
            Assert.That(cardArea.xMax, Is.LessThanOrEqualTo(panel.rect.xMax));
        }

        [UnityTest]
        [TestCase(1920, 1080, ExpectedResult = null)]
        [TestCase(900, 1400, ExpectedResult = null)]
        public IEnumerator LongExpression_GrowsTheCardSoNoLineIsCut(float width, float height)
        {
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            view.Show(
                new Content
                {
                    UserTranslation =
                        "You have no conscience, do you? I should've just left you to die. "
                        + "What were you even thinking back there?",
                }
            );
            yield return null;
            Transform card = instance.transform.Find("Panel/UserTranslation");
            var viewport = (RectTransform)card.Find("Viewport");
            var expression = viewport.Find("Expression").GetComponent<TMP_Text>();
            float needed = expression.GetPreferredValues(expression.text, viewport.rect.width, 0).y;
            Assert.That(viewport.rect.height, Is.GreaterThanOrEqualTo(needed - .5f));
            var panel = (RectTransform)card.parent;
            Rect cardArea = InPanel((RectTransform)card, panel);
            Rect nameArea = InPanel((RectTransform)panel.Find("Title"), panel);
            Assert.That(cardArea.Overlaps(nameArea), Is.False);
        }

        // 카드 상한보다 긴 표현은 막대로 더 있다는 걸 알리고, 글자를 끌어 마지막 줄까지 읽는다.
        [UnityTest]
        public IEnumerator VeryLongExpression_ShowsAScrollbarAndDragsToTheLastLine()
        {
            view.Show(new Content { UserTranslation = VeryLongExpression });
            yield return null;
            yield return null;
            var scroll = instance
                .transform.Find("Panel/UserTranslation")
                .GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));

            GameObject text = scroll.content.gameObject;
            var drag = new PointerEventData(EventSystem.current ?? NewEventSystem())
            {
                button = PointerEventData.InputButton.Left,
                position = new Vector2(1300, 120),
            };
            ExecuteEvents.ExecuteHierarchy(text, drag, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.ExecuteHierarchy(text, drag, ExecuteEvents.beginDragHandler);
            drag.position += new Vector2(0, 2000);
            ExecuteEvents.ExecuteHierarchy(text, drag, ExecuteEvents.dragHandler);
            ExecuteEvents.ExecuteHierarchy(text, drag, ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
        }

        [UnityTest]
        public IEnumerator ExpressionThatFits_HidesTheScrollbar()
        {
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            yield return null;
            var scroll = instance
                .transform.Find("Panel/UserTranslation")
                .GetComponent<ScrollRect>();
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False);
        }

        // 시안 B: 틀리진 않았지만 어색하거나 짧은 영어는 안내와 [더보기]만 보이고, 누르면 같은 카드 안에 2~3문장 목록이 나온다.
        // 카드는 커지지 않고 넘치는 문장은 카드 안에서 스크롤한다. 다시 누르면 [접기]에서 안내로 돌아간다 (S15P31A305-847)
        [UnityTest]
        public IEnumerator BetterSentences_OpenFromMoreInTheSameCardSize_AndScrollInside()
        {
            view.Show(
                new Content
                {
                    UserBetterExpressions = new[]
                    {
                        "Can I get a latte, please?",
                        "I'd like a latte, please.",
                        "Could I have a latte?",
                    },
                }
            );
            yield return null;
            yield return null;
            var card = (RectTransform)instance.transform.Find("Panel/UserTranslation");
            var scroll = card.GetComponent<ScrollRect>();
            var button = card.Find("ListenButton").GetComponent<Button>();
            TMP_Text Label() => button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                card.Find("Heading").GetComponent<TMP_Text>().text,
                Is.EqualTo("이럴 때 쓰면 좋아요!")
            );
            Assert.That(
                card.Find("Viewport/Guide").GetComponent<TMP_Text>().text,
                Is.EqualTo("방금 한 말도 잘 통해요.\n이렇게 말하면 더 자연스러워요.")
            );
            Assert.That(card.Find("Viewport/BetterList").gameObject.activeSelf, Is.False);
            Assert.That(card.Find("Viewport/Expression").gameObject.activeSelf, Is.False);
            Assert.That(Label().text, Is.EqualTo("더보기"));
            Vector2 closedSize = card.rect.size;

            button.onClick.Invoke();
            yield return null;
            yield return null;

            var list = card.Find("Viewport/BetterList").GetComponent<TMP_Text>();
            Assert.That(list.gameObject.activeSelf, Is.True);
            Assert.That(card.Find("Viewport/Guide").gameObject.activeSelf, Is.False);
            Assert.That(list.GetParsedText(), Does.Contain("Can I get a latte, please?"));
            Assert.That(list.GetParsedText(), Does.Contain("Could I have a latte?"));
            Assert.That(Label().text, Is.EqualTo("접기"));
            Assert.That(card.rect.size, Is.EqualTo(closedSize));
            Assert.That(scroll.content, Is.SameAs(list.rectTransform));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);

            button.onClick.Invoke();
            yield return null;

            Assert.That(card.Find("Viewport/Guide").gameObject.activeSelf, Is.True);
            Assert.That(Label().text, Is.EqualTo("더보기"));
            Assert.That(card.rect.size, Is.EqualTo(closedSize));
        }

        private const string VeryLongExpression =
            "You have no conscience, do you? I should've just left you to die. "
            + "What were you even thinking back there? I waited for you at the bus stop "
            + "for almost an hour in the rain, and you didn't even call me once. "
            + "Next time, please tell me before you change your plans, because I really "
            + "worried about you and I almost called the police.";

        private GameObject eventSystem;

        private EventSystem NewEventSystem()
        {
            eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            return eventSystem.GetComponent<EventSystem>();
        }

        private static Rect InPanel(RectTransform rect, RectTransform panel)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = panel.InverseTransformPoint(corners[0]);
            Vector3 max = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private sealed class Content : IDialogueContent
        {
            private static readonly DialogueLine Npc = DialogueLine.FromNpc(
                "It is on your right.",
                null
            );
            public string NpcName => "Jake";
            public string UserTranslation { get; set; }
            public string UserLine { get; set; } = "카페 어디야?";
            public IReadOnlyList<string> UserBetterExpressions { get; set; }
            public IReadOnlyList<DialogueLine> Lines =>
                new[] { DialogueLine.FromUser(UserLine), Npc };
            public DialogueLine LastNpcLine => Npc;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;
            public bool IsBusy => false;
            public string StatusMessage => null;
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => null;
            public bool IsSuggestingWords => false;
            public bool CanSend => true;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => false;
            public bool SpeaksNpcLines => true;
            public bool IsNpcSpeechReady => true;
        }
    }
}
#endif
