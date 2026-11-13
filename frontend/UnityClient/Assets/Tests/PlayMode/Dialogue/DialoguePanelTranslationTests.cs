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
            Card.Find("Viewport/Body/ExpressionRow/Expression").GetComponent<TMP_Text>();

        // 고친 문장·영어 번역 옆 스피커. 머리줄 [듣기] 대신 문장마다 붙는다(S15P31A305-895).
        private Button ExpressionSpeaker =>
            Card.Find("Viewport/Body/ExpressionRow/SpeakButton").GetComponent<Button>();
        private Button OthersButton => Card.Find("OthersButton").GetComponent<Button>();
        private string OthersLabel => OthersButton.GetComponentInChildren<TMP_Text>(true).text;

        // 다른 표현 버튼의 꺾쇠. 접혀 있으면 오른쪽(›), 펼치면 아래(⌄)를 가리킨다.
        private float ChevronAngle => OthersButton.transform.Find("Chevron").localEulerAngles.z;

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
            Assert.That(Card.Find("ListenButton"), Is.Null);
            Assert.That(ExpressionSpeaker.gameObject.activeInHierarchy, Is.True);
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

        // 카드의 영어 문장마다 옆 스피커로 그 문장만 기기 음성으로 듣는다. 보내지는 않는다(S15P31A305-895).
        [UnityTest]
        public IEnumerator Speakers_ReadTheirOwnSentenceWithoutSendingIt()
        {
            var spoken = new List<string>();
            int sends = 0;
            view.ExpressionListenRequested += spoken.Add;
            view.SendRequested += text => sends++;
            view.Show(
                new Content
                {
                    UserTranslation = "Where is the cafe?",
                    UserBetterExpressions = new[]
                    {
                        "How do I get to the cafe?",
                        "Could you tell me where the cafe is?",
                    },
                }
            );
            yield return null;
            ExpressionSpeaker.onClick.Invoke();
            OthersButton.onClick.Invoke();
            yield return null;
            Transform[] rows = OtherRows;
            Assert.That(rows.All(row => row.Find("SpeakButton").gameObject.activeSelf), Is.True);
            rows[1].Find("SpeakButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(
                spoken,
                Is.EqualTo(new[] { "Where is the cafe?", "Could you tell me where the cafe is?" })
            );
            Assert.That(sends, Is.Zero);
        }

        // '잘 말했어요'는 머리줄 높이만큼만 차지해 빈 공간이 없고, 대사 칸 위쪽에 붙는다.
        // 펼쳐도 대화창과 카드 위쪽은 그대로이고 아래로만 늘어난다(S15P31A305-895).
        [UnityTest]
        public IEnumerator WellSaidCard_FitsItsHeader_AndOpensDownwardWithoutMovingTheDialogue()
        {
            view.Show(
                new Content
                {
                    UserLine = "Sorry.",
                    UserBetterExpressions = new[] { "I apologize.", "Sorry about that." },
                }
            );
            yield return null;
            yield return null;
            var panel = (RectTransform)Card.parent;
            Rect folded = InPanel((RectTransform)Card, panel);
            Rect history = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/History"),
                panel
            );
            float nameBottom = InPanel((RectTransform)panel.Find("Title"), panel).yMin;
            Assert.That(folded.height, Is.LessThanOrEqualTo(110));
            Assert.That(folded.yMax, Is.EqualTo(history.yMax).Within(1));
            Assert.That(ChevronAngle, Is.EqualTo(0).Within(.5f));
            // 다른 표현 버튼은 옅은 강조색 바탕과 강조색 꺾쇠로 다른 알약보다 조금 눈에 띈다.
            Color othersFace = OthersButton.GetComponent<Image>().color;
            Color plainFace = Card.Find("ExpandButton").GetComponent<Image>().color;
            Assert.That(othersFace, Is.Not.EqualTo(plainFace));
            Assert.That(
                OthersButton.transform.Find("Chevron").GetComponent<Image>().color,
                Is.EqualTo((Color)new Color32(210, 79, 47, 255))
            );

            OthersButton.onClick.Invoke();
            yield return null;
            yield return null;
            Rect open = InPanel((RectTransform)Card, panel);
            Assert.That(open.yMax, Is.EqualTo(folded.yMax).Within(1));
            Assert.That(open.yMin, Is.LessThan(folded.yMin - 40));
            Assert.That(
                InPanel((RectTransform)panel.Find("Title"), panel).yMin,
                Is.EqualTo(nameBottom).Within(1)
            );
            Assert.That(ChevronAngle, Is.EqualTo(270).Within(.5f));
            var scroll = Card.GetComponent<ScrollRect>();
            Assert.That(
                scroll.content.rect.height,
                Is.LessThanOrEqualTo(scroll.viewport.rect.height + .5f)
            );
        }

        // 머리줄 버튼은 도구 줄 [다시 듣기]·[뜻]과 같은 유리 알약이다. 밝은 카드 위라 테두리와 글자는 테마 글자색으로 칠한다.
        // 알약 그림의 위아래 모서리가 버튼 높이 안에 들어가야 Unity가 세로로 눌러 그리지 않아 찌그러져 보이지 않는다.
        [UnityTest]
        public IEnumerator HeaderButtons_KeepTheSmoothPillShape()
        {
            view.Show(
                new Content
                {
                    UserTranslation = "Where is the cafe?",
                    UserBetterExpressions = new[] { "How do I get to the cafe?" },
                }
            );
            yield return null;
            Sprite toolFace = instance
                .GetComponentsInChildren<Button>(true)
                .First(button => button.name == "ReplayButton")
                .GetComponent<Image>()
                .sprite;
            foreach (string name in new[] { "OthersButton", "ExpandButton" })
            {
                var face = Card.Find(name).GetComponent<Image>();
                Assert.That(
                    face.sprite,
                    Is.EqualTo(toolFace),
                    $"{name}이 도구 줄 버튼과 모양이 다릅니다."
                );
                Vector4 border = face.sprite.border;
                float corners =
                    (border.y + border.w) / (face.pixelsPerUnit * face.pixelsPerUnitMultiplier);
                Assert.That(
                    corners,
                    Is.LessThanOrEqualTo(face.rectTransform.rect.height + .5f),
                    $"{name}의 알약 모서리가 버튼 높이보다 큽니다."
                );
            }
            // 글자 없는 확대 버튼은 옆으로 긴 타원이 아니라 정확한 원이다.
            Rect expand = ((RectTransform)Card.Find("ExpandButton")).rect;
            Assert.That(expand.width, Is.EqualTo(expand.height).Within(.5f));
        }

        // 무례한 말에는 같은 기분을 공손하게 말하는 다른 표현을 '무례하지 않게 말하면' 아래 보인다.
        [UnityTest]
        public IEnumerator RudeLine_ShowsPoliteWaysUnderTheirOwnHeading()
        {
            view.Show(
                new Content
                {
                    UserLine = "뭘봐 못생긴게",
                    UserTranslation = "What are you looking at, you ugly thing?",
                    UserBetterExpressions = new[] { "Why are you staring at me?" },
                    UserTurnRude = true,
                }
            );
            yield return null;
            OthersButton.onClick.Invoke();
            yield return null;
            Assert.That(
                Card.Find("Viewport/Body/OthersHeading").GetComponent<TMP_Text>().text,
                Is.EqualTo("무례하지 않게 말하면")
            );
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
        public IEnumerator NarrowScreen_StacksTheConversationAboveTheCardWithoutOverlap()
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
            Assert.That(historyArea.yMin, Is.GreaterThan(cardArea.yMax));
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
        public IEnumerator VeryLongExpression_StopsAtTheDialogueMaximum_AndScrollsToTheEnd()
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
            Assert.That(Expression.transform.parent.gameObject.activeSelf, Is.False);
            Assert.That(OthersButton.gameObject.activeSelf, Is.True);
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 3개"));
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
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Rect toggle = InPanel((RectTransform)OthersButton.transform, panel);
            Assert.That(OthersButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(toggle.yMin, Is.GreaterThanOrEqualTo(openCard.yMin - .5f));
            Assert.That(toggle.yMax, Is.LessThanOrEqualTo(openCard.yMax + .5f));
            Assert.That(toggle.Overlaps(viewportArea), Is.False);

            OthersButton.onClick.Invoke();
            yield return null;
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 3개"));
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
            Assert.That(Expression.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(ExpressionSpeaker.gameObject.activeSelf, Is.True);
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 1개"));

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
            Assert.That(OthersLabel, Is.EqualTo("다른 표현 2개"));
            Assert.That(OtherRows, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Expand_OpensACenteredWindow_AndCloseKeepsTheConversation()
        {
            view.Show(
                new Content
                {
                    UserTranslation = "Where is the cafe?",
                    UserBetterExpressions = new[] { "How can I get to the cafe?" },
                }
            );
            yield return null;
            Card.Find("ExpandButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var overlay = instance.transform.Find("ExpressionOverlay");
            Assert.That(overlay, Is.Not.Null);
            var window = (RectTransform)overlay.Find("ExpandedExpressionCard");
            Assert.That(window.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(window.rect.width, Is.GreaterThan(((RectTransform)Card).rect.width));
            window.Find("ExpandButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(instance.transform.Find("ExpressionOverlay"), Is.Null);
            Assert.That(Card.gameObject.activeInHierarchy, Is.True);
        }

        // 확대창은 내용 높이만큼만 차지해 문장 아래에 빈 공간이 남지 않는다. 화면 가운데에 둔다(S15P31A305-903).
        // 확대창의 문장도 옆 스피커로 듣는다.
        [UnityTest]
        public IEnumerator ExpandedWindow_FitsItsContent_AndKeepsTheSpeakers()
        {
            var spoken = new List<string>();
            view.ExpressionListenRequested += spoken.Add;
            view.Show(
                new Content
                {
                    UserLine = "i want coffee",
                    UserTranslation = "I want coffee.",
                    UserBetterExpressions = new[] { "I'd like a coffee, please." },
                }
            );
            yield return null;
            Card.Find("ExpandButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var window = (RectTransform)
                instance.transform.Find("ExpressionOverlay/ExpandedExpressionCard");
            var scroll = window.GetComponent<ScrollRect>();
            float content = scroll.content.rect.height;
            float room = scroll.viewport.rect.height;
            Assert.That(
                content,
                Is.LessThanOrEqualTo(room + .5f),
                "짧은 내용인데 확대창 안에서 스크롤합니다."
            );
            Assert.That(room - content, Is.LessThan(24), "확대창 문장 아래에 빈 공간이 남습니다.");
            Assert.That(window.anchoredPosition, Is.EqualTo(Vector2.zero));

            window
                .Find("Viewport/Body/ExpressionRow/SpeakButton")
                .GetComponent<Button>()
                .onClick.Invoke();
            Transform row = window
                .Find("Viewport/Body")
                .Cast<Transform>()
                .First(child => child.name.StartsWith("Other ") && child.gameObject.activeSelf);
            row.Find("SpeakButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(
                spoken,
                Is.EqualTo(new[] { "I want coffee.", "I'd like a coffee, please." })
            );
        }

        [UnityTest]
        public IEnumerator OpenCard_KeepsHeaderControlsAndDialogueAtTheTop()
        {
            view.Show(
                new Content
                {
                    UserLine = "A latte, please.",
                    UserBetterExpressions = new[] { "I'd like a latte, please." },
                }
            );
            yield return null;
            OthersButton.onClick.Invoke();
            yield return null;
            var panel = (RectTransform)Card.parent;
            Rect toggle = InPanel((RectTransform)OthersButton.transform, panel);
            Rect title = InPanel(Heading.rectTransform, panel);
            Assert.That(toggle.center.y, Is.EqualTo(title.center.y).Within(5));
            var history = instance
                .transform.Find("Panel/Conversation/History")
                .GetComponent<ScrollRect>();
            Assert.That(history.content.pivot.y, Is.EqualTo(1));
            Assert.That(history.content.anchoredPosition.y, Is.EqualTo(0).Within(1));
            foreach (var tool in view.ToolParts)
                if (tool.gameObject.activeInHierarchy)
                    Assert.That(
                        InPanel((RectTransform)Card, panel).yMax,
                        Is.LessThanOrEqualTo(InPanel(tool, panel).yMin)
                    );
        }

        private const string LongExpression =
            "You have no conscience, do you? I should've just left you to die. "
            + "What were you even thinking back there?";

        // 한국어로 말하면 대사 칸이 좁아져 대사가 여러 줄로 늘어난다. 그래도 추천 문장 카드가 눌려 [힌트]가 카드 밖으로 나가지 않는다.
        [UnityTest]
        public IEnumerator LongConversationInKorean_KeepsTheHintButtonInsideItsCard()
        {
            string longLine = string.Join(
                " ",
                System.Linq.Enumerable.Repeat(VeryLongExpression, 2)
            );
            view.Show(
                new Content
                {
                    UserTranslation =
                        "I'm the police commissioner. I'm the one who can approve it.",
                    Conversation = new[]
                    {
                        DialogueLine.FromUser(longLine),
                        DialogueLine.FromNpc(longLine, null),
                        DialogueLine.FromUser(longLine),
                        DialogueLine.FromNpc(longLine, null),
                    },
                }
            );
            yield return null;
            var root = (RectTransform)instance.transform;
            Transform hintCard = instance.transform.Find("Suggestions/HintCard");
            Rect cardArea = InPanel((RectTransform)hintCard, root);
            Rect buttonArea = InPanel((RectTransform)hintCard.Find("HintCover/HintButton"), root);
            Rect titleArea = InPanel((RectTransform)hintCard.Find("Title"), root);
            Assert.That(buttonArea.yMin, Is.GreaterThanOrEqualTo(cardArea.yMin));
            Assert.That(buttonArea.yMax, Is.LessThanOrEqualTo(titleArea.yMin));
            Rect toolsArea = InPanel(
                (RectTransform)instance.transform.Find("Panel/Conversation/ToolRow"),
                root
            );
            Assert.That(cardArea.yMin, Is.GreaterThanOrEqualTo(toolsArea.yMax));

            // [힌트]를 눌러 펼친 추천 문장과 그 뜻도 카드 안에서 잘리지 않는다.
            hintCard.Find("HintCover/HintButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var sentence = hintCard.Find("Viewport/HintSentence").GetComponent<TMP_Text>();
            Assert.That(sentence.text, Does.Contain("Over there, please."));
            Assert.That(sentence.text, Does.Contain("저쪽으로 부탁해요."));
            var sentenceViewport = (RectTransform)sentence.rectTransform.parent;
            Assert.That(
                sentenceViewport.rect.height,
                Is.GreaterThanOrEqualTo(sentence.preferredHeight - .5f)
            );
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
            public bool UserTurnRude { get; set; }
            public IReadOnlyList<DialogueLine> Conversation { get; set; }
            public IReadOnlyList<DialogueLine> Lines =>
                Conversation ?? new[] { DialogueLine.FromUser(UserLine), Npc };
            public DialogueLine LastNpcLine => Npc;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => "저쪽으로 부탁해요.";
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
            public string HintText => "Over there, please.";
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
