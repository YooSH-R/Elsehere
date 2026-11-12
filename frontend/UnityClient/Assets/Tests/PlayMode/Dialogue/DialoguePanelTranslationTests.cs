#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private Transform Card => instance.transform.Find("Panel/UserTranslation");
        private TMP_Text Heading => Card.Find("Heading").GetComponent<TMP_Text>();
        private TMP_Text Expression =>
            Card.Find("Viewport/Body/Expression").GetComponent<TMP_Text>();
        private Button ListenButton => Card.Find("ListenButton").GetComponent<Button>();
        private Button OthersButton => Card.Find("OthersButton").GetComponent<Button>();
        private string OthersLabel => OthersButton.GetComponentInChildren<TMP_Text>(true).text;

        // 펼친 다른 표현 줄(번호 칸). 접혀 있으면 없다.
        private Transform[] OtherRows =>
            Card.Find("Viewport/Body")
                .Cast<Transform>()
                .Where(row => row.name.StartsWith("Other ") && row.gameObject.activeSelf)
                .ToArray();

        [UnityTest]
        public IEnumerator KoreanLine_ShowsItsEnglishBesideTheConversation_WithSmallerText()
        {
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            Assert.That(Card.gameObject.activeInHierarchy, Is.True);
            Assert.That(Heading.text, Is.EqualTo("영어로는 이렇게 말해요"));
            Assert.That(Expression.GetParsedText(), Is.EqualTo("Where is the cafe?"));
            var lines = instance
                .transform.Find("Panel/Conversation/History")
                .GetComponentInChildren<TMP_Text>(true);
            Assert.That(Expression.fontSize, Is.LessThanOrEqualTo(lines.fontSize));
            Assert.That(lines.text, Does.Contain("카페 어디야?"));
            Assert.That(ListenButton.gameObject.activeSelf, Is.True);
            Assert.That(
                ListenButton.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("듣기")
            );
            // 다른 표현이 없으면 [더 보기]도 없다.
            Assert.That(OthersButton.gameObject.activeSelf, Is.False);
            var panel = (RectTransform)Card.parent;
            Rect cardArea = InPanel((RectTransform)Card, panel);
            Rect historyArea = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/History"),
                panel
            );
            Assert.That(cardArea.xMin, Is.GreaterThan(historyArea.xMax));
            Assert.That(cardArea.Overlaps(historyArea), Is.False);
        }

        // 영어로 틀리게 한 말에는 발화 전체를 고친 문장을 보이고, 학습자가 한 말과 달라진 곳에 밑줄을 긋는다.
        // 지금 다시 말하라고 재촉하지 않는 제목을 쓴다 (S15P31A305-862).
        [UnityTest]
        public IEnumerator EnglishCorrection_ShowsTheWholeFixedSentence_WithTheChangedWordsMarked()
        {
            var content = new Content
            {
                UserLine = "i want to speak korea",
                UserTranslation = "I want to speak Korean.",
            };
            view.Show(content);
            yield return null;
            Assert.That(Heading.text, Is.EqualTo("이렇게 말하면 자연스러워요"));
            Assert.That(Expression.GetParsedText(), Is.EqualTo("I want to speak Korean."));
            Assert.That(Expression.text, Does.Contain("<u><noparse>I</noparse></u>"));
            Assert.That(Expression.text, Does.Contain("<u><noparse>Korean</noparse></u>"));
            Assert.That(Expression.text, Does.Not.Contain("<u><noparse>want"));

            content.UserLine = "여기서 먹을게요.";
            content.UserTranslation = "I'll eat here.";
            view.Refresh();
            Assert.That(Heading.text, Is.EqualTo("영어로는 이렇게 말해요"));
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
            ListenButton.onClick.Invoke();
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
            Assert.That(Card.gameObject.activeSelf, Is.False);
            view.Show(new Content());
            yield return null;
            Assert.That(Card.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator NarrowScreen_StacksTheCardAboveTheConversationWithoutOverlap()
        {
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 1400);
            view.Show(new Content { UserTranslation = "Where is the cafe?" });
            yield return null;
            var panel = (RectTransform)Card.parent;
            Rect cardArea = InPanel((RectTransform)Card, panel);
            Rect historyArea = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/History"),
                panel
            );
            Assert.That(cardArea.yMin, Is.GreaterThan(historyArea.yMax));
            Assert.That(cardArea.Overlaps(historyArea), Is.False);
            Assert.That(cardArea.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin));
            Assert.That(cardArea.xMax, Is.LessThanOrEqualTo(panel.rect.xMax));
        }

        // 카드는 내용이 짧으면 짧아지고, 길어지면 대화창이 카드에 맞춰 커진다. 대화창 최대 높이 안이면 문장이 다 보인다.
        [UnityTest]
        public IEnumerator LongExpression_GrowsTheCardAndTheDialogue_WithoutScrolling()
        {
            var content = new Content { UserTranslation = "Where is the cafe?" };
            view.Show(content);
            yield return null;
            var panel = (RectTransform)Card.parent;
            Rect shortCard = InPanel((RectTransform)Card, panel);
            float nameBottom = InPanel((RectTransform)panel.Find("Title"), panel).yMin;

            content.UserTranslation = LongExpression;
            view.Refresh();
            yield return null;
            yield return null;
            Rect longCard = InPanel((RectTransform)Card, panel);
            Rect nameArea = InPanel((RectTransform)panel.Find("Title"), panel);
            Assert.That(longCard.height, Is.GreaterThan(shortCard.height));
            Assert.That(nameArea.yMin, Is.GreaterThan(nameBottom));
            Assert.That(longCard.Overlaps(nameArea), Is.False);
            var scroll = Card.GetComponent<ScrollRect>();
            Assert.That(
                scroll.content.rect.height,
                Is.LessThanOrEqualTo(scroll.viewport.rect.height + .5f)
            );
            Assert.That(panel.rect.height, Is.LessThanOrEqualTo(1080 + .5f));
        }

        // 대화창 최대 높이보다 긴 표현은 그 높이에서 멈추고, 마지막 줄을 반쯤 가려 아래에 더 있다는 걸 알린다.
        // 굵은 막대가 보이고, 글자를 끌어 마지막 줄까지 읽는다.
        [UnityTest]
        public IEnumerator VeryLongExpression_StopsAtTheDialogueMaximum_HalfShowingTheNextLine()
        {
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 720);
            view.Show(new Content { UserTranslation = VeryLongExpression });
            yield return null;
            yield return null;
            var scroll = Card.GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                ((RectTransform)scroll.verticalScrollbar.transform).rect.width,
                Is.GreaterThanOrEqualTo(10)
            );
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.01f));
            Expression.ForceMeshUpdate();
            float line = Expression.preferredHeight / Expression.textInfo.lineCount;
            float shownLines = scroll.viewport.rect.height / line;
            Assert.That(shownLines - Mathf.Floor(shownLines), Is.InRange(.25f, .75f));

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
            var scroll = Card.GetComponent<ScrollRect>();
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False);
        }

        // 맞게 말해도 같은 뜻의 다른 표현을 접어 둔다. 펼치면 문장마다 번호 칸으로 나뉘고 대화창이 카드에 맞춰 커진다.
        // 대화창 최대 높이에 닿으면 다음 문장 칸을 반쯤 가려 아래에 더 있다는 걸 알리고,
        // 아래 [접기]는 스크롤해도 카드 안에 그대로 보인다 (S15P31A305-862).
        [UnityTest]
        public IEnumerator OtherExpressions_StayFolded_AndOpenAsNumberedRowsWithAFixedToggle()
        {
            instance.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 720);
            var content = new Content
            {
                UserLine = "Can I get a latte?",
                UserBetterExpressions = new[]
                {
                    "I'd like a latte, please. I'm in a bit of a hurry, so a small one is fine.",
                    "Could I have a latte? A small one is fine because I'm in a hurry.",
                    "One latte, please. Make it small, I'm running a little late today.",
                },
            };
            view.Show(content);
            yield return null;
            yield return null;
            var panel = (RectTransform)Card.parent;
            Assert.That(Card.gameObject.activeInHierarchy, Is.True);
            Assert.That(Heading.text, Is.EqualTo("잘 말했어요"));
            Assert.That(Expression.gameObject.activeSelf, Is.False);
            Assert.That(ListenButton.gameObject.activeSelf, Is.False);
            Assert.That(OthersButton.gameObject.activeSelf, Is.True);
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 3개 더 보기"));
            Assert.That(OtherRows, Is.Empty);

            OthersButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(OthersLabel, Is.EqualTo("접기"));
            Assert.That(
                Card.Find("Viewport/Body/OthersHeading").GetComponent<TMP_Text>().text,
                Is.EqualTo("이렇게도 말할 수 있어요")
            );
            Transform[] rows = OtherRows;
            Assert.That(rows, Has.Length.EqualTo(3));
            for (int index = 0; index < rows.Length; index++)
            {
                Assert.That(
                    rows[index].Find("Number/Label").GetComponent<TMP_Text>().text,
                    Is.EqualTo((index + 1).ToString())
                );
                Assert.That(
                    rows[index].Find("Text").GetComponent<TMP_Text>().GetParsedText(),
                    Is.EqualTo(content.UserBetterExpressions[index])
                );
            }
            Rect openCard = InPanel((RectTransform)Card, panel);
            Rect nameArea = InPanel((RectTransform)panel.Find("Title"), panel);
            Assert.That(openCard.Overlaps(nameArea), Is.False);
            Assert.That(panel.rect.height, Is.LessThanOrEqualTo(720 + .5f));

            var scroll = Card.GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Rect viewportArea = InPanel((RectTransform)Card.Find("Viewport"), panel);
            Rect cut = rows.Select(row => InPanel((RectTransform)row, panel))
                .First(row => row.yMin < viewportArea.yMin - .5f);
            Assert.That((cut.yMax - viewportArea.yMin) / cut.height, Is.InRange(.25f, .75f));

            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Rect toggle = InPanel((RectTransform)OthersButton.transform, panel);
            Assert.That(OthersButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(toggle.yMin, Is.GreaterThanOrEqualTo(openCard.yMin - .5f));
            Assert.That(toggle.yMax, Is.LessThanOrEqualTo(openCard.yMax + .5f));
            Assert.That(toggle.Overlaps(viewportArea), Is.False);

            OthersButton.onClick.Invoke();
            yield return null;
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 3개 더 보기"));
            Assert.That(OtherRows, Is.Empty);
        }

        // 틀리게 말했을 때도 고친 문장 아래에 다른 표현을 접어 둔다. 다음 말을 하면 다시 접힌다.
        [UnityTest]
        public IEnumerator Correction_AlsoFoldsOtherExpressions_AndANewLineFoldsThemAgain()
        {
            var content = new Content
            {
                UserLine = "i want to speak korea",
                UserTranslation = "I want to speak Korean.",
                UserBetterExpressions = new[] { "I'd like to speak Korean." },
            };
            view.Show(content);
            yield return null;
            Assert.That(Heading.text, Is.EqualTo("이렇게 말하면 자연스러워요"));
            Assert.That(Expression.gameObject.activeSelf, Is.True);
            Assert.That(ListenButton.gameObject.activeSelf, Is.True);
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 1개 더 보기"));

            OthersButton.onClick.Invoke();
            yield return null;
            Assert.That(OtherRows, Has.Length.EqualTo(1));

            content.UserLine = "can we speak korean";
            content.UserTranslation = "Can we speak Korean?";
            content.UserBetterExpressions = new[]
            {
                "Could we talk in Korean?",
                "Let's speak Korean.",
            };
            view.Refresh();
            yield return null;
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 2개 더 보기"));
            Assert.That(OtherRows, Is.Empty);
        }

        private const string LongExpression =
            "You have no conscience, do you? I should've just left you to die. "
            + "What were you even thinking back there?";

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
