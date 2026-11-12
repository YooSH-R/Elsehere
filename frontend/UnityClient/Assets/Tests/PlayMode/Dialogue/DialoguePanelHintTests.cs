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
    /// <summary>
    /// 서버 없이 실제 대화창 프리팹에서 [힌트]를 메뉴를 거치지 않고 바로 누를 수 있는지,
    /// 고정 카드에서 추천 문장을 펼치고 가리며, 문장을 직접 눌렀을 때만 입력창에 들어가는지 확인한다.
    /// </summary>
    public class DialoguePanelHintTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string HintSentence = "Can I get an iced latte, please?";

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;
        private HintContent content;

        [UnitySetUp]
        public IEnumerator ShowServerConversation()
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            content = new HintContent();
            view.Show(content);
            yield return null;
        }

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator HintButton_ClickedOnScreen_RequestsHintWithoutMoreMenu()
        {
            int requests = 0;
            view.HintRequested += () => requests++;
            Button hint = FindButton("HintButton");

            Assert.That(hint.transform.IsChildOf(FindChild("HintCard")), Is.True);
            Assert.That(FindChild("Panel/Conversation/ToolRow/HintButton"), Is.Null);
            Assert.That(hint.gameObject.activeInHierarchy, Is.True);
            Assert.That(hint.interactable, Is.True);
            Assert.That(FindChild("MoreButton"), Is.Null);

            ClickCenterOf(hint);
            yield return null;

            Assert.That(requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HintText_AfterServerAnswers_StaysHiddenWithoutChangingTheDraft()
        {
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;

            Assert.That(Input.text, Is.Empty);
            Assert.That(FindChild("HintCard"), Is.Not.Null);
            Assert.That(FindChild("HintCover").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindChild("HintSentence").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator PrefetchedHint_RevealsImmediatelyAndOnlySignalsOpening()
        {
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;
            int requests = 0;
            int opened = 0;
            view.HintRequested += () => requests++;
            view.HintOpened += () => opened++;
            Input.text = "My own answer";
            ClickCenterOf(FindButton("HintButton"));
            yield return null;
            Assert.That(FindChild("HintSentence").gameObject.activeInHierarchy, Is.True);
            Assert.That(
                FindChild("HintSentence").GetComponent<TMP_Text>().text,
                Does.Contain(HintSentence)
            );
            Assert.That(requests, Is.Zero);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(Input.text, Is.EqualTo("My own answer"));
        }

        [UnityTest]
        public IEnumerator HintText_AfterUserEditsTheInput_IsNotPutBackOnRefresh()
        {
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;

            Input.text = "Can I get a hot latte?";
            view.Refresh();
            yield return null;

            Assert.That(Input.text, Is.EqualTo("Can I get a hot latte?"));
        }

        [UnityTest]
        public IEnumerator RevealedSentence_WhenClicked_FillsTheInputWithoutSendingOrAskingAgain()
        {
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;
            int requests = 0;
            view.HintRequested += () => requests++;
            Input.text = "";

            Button hint = FindButton("HintButton");
            Assert.That(hint.interactable, Is.True);
            ClickCenterOf(hint);
            yield return null;

            Assert.That(Input.text, Is.Empty);
            Assert.That(
                FindChild("HintSentence").GetComponent<TMP_Text>().text,
                Does.Contain("아이스라테 한 잔 주시겠어요?")
            );
            ClickCenterOf(FindButton("UseHintButton"));
            yield return null;
            Assert.That(Input.text, Is.EqualTo(HintSentence));
            Assert.That(requests, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TutorialHint_LikeTheService_ShowsTheSentenceAndFillsOnlyWhenItIsClicked()
        {
            // 튜토리얼이 [힌트]를 누르면 바로 입력칸이 채워지는 기능처럼 가르치지 않도록 서비스와 같게 동작한다.
            content.IsGuided = true;
            view.Show(content);
            view.HintRequested += () => content.HintText = HintSentence;
            yield return null;

            ClickCenterOf(FindButton("HintButton"));
            yield return null;

            Assert.That(Input.text, Is.Empty, "[힌트]는 추천 문장을 보여 주기만 합니다.");
            Assert.That(FindChild("HintSentence").gameObject.activeInHierarchy, Is.True);
            ClickCenterOf(FindButton("UseHintButton"));
            yield return null;
            Assert.That(Input.text, Is.EqualTo(HintSentence));
        }

        [UnityTest]
        public IEnumerator TutorialDraft_LateHintDoesNotReplaceTheSentenceBeingTyped()
        {
            content.IsGuided = true;
            view.Show(content);
            Input.text = "Turn left at the corner.";
            content.HintText = "Go straight ahead.";
            view.Refresh();
            yield return null;
            Assert.That(Input.text, Is.EqualTo("Turn left at the corner."));
        }

        [UnityTest]
        public IEnumerator HintRequest_DraftEditedWhileWaitingIsKept()
        {
            Input.text = "My first draft";
            ClickCenterOf(FindButton("HintButton"));
            Input.text = "My new draft";
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;
            Assert.That(Input.text, Is.EqualTo("My new draft"));
        }

        [UnityTest]
        public IEnumerator HintRequest_UnchangedDraftIsKeptUntilTheSentenceIsClicked()
        {
            Input.text = "My first draft";
            ClickCenterOf(FindButton("HintButton"));
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;
            Assert.That(Input.text, Is.EqualTo("My first draft"));
        }

        [UnityTest]
        public IEnumerator HintToggle_ReusesReceivedTextAndKeepsTheCardSize()
        {
            int requests = 0;
            view.HintRequested += () =>
            {
                requests++;
                content.HintText = HintSentence;
            };
            RectTransform card = (RectTransform)FindChild("HintCard");
            Vector2 before = card.rect.size;
            ClickCenterOf(FindButton("HintButton"));
            yield return null;
            Assert.That(FindChild("HintSentence").gameObject.activeInHierarchy, Is.True);
            Assert.That(card.rect.size, Is.EqualTo(before));
            ClickCenterOf(FindButton("HideHintButton"));
            yield return null;
            Assert.That(FindChild("HintCover").gameObject.activeInHierarchy, Is.True);
            ClickCenterOf(FindButton("HintButton"));
            yield return null;
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(card.rect.size, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator LongHint_ScrollsInsideTheFixedCardAndResetsForTheNextNpcLine()
        {
            content.HintText = string.Join("\n", new string[20])
                .Replace("\n", "A longer practice sentence.\n");
            view.Refresh();
            ClickCenterOf(FindButton("HintButton"));
            yield return null;
            ScrollRect scroll = FindChild("HintSentence").GetComponentInParent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            RectTransform card = (RectTransform)FindChild("HintCard");
            Vector2 size = card.rect.size;
            content.CurrentLine = DialogueLine.FromNpc("Anything else?", null);
            content.HintText = null;
            view.Refresh();
            yield return null;
            Assert.That(FindChild("HintCover").gameObject.activeInHierarchy, Is.True);
            Assert.That(card.rect.size, Is.EqualTo(size));
        }

        [UnityTest]
        public IEnumerator ReviewReference_KeepsExistingTargetsWithoutGeneralSuggestionCards()
        {
            view.SetReviewReference("분실한 파란 가방을 찾고 물건을 설명해 보세요.");
            view.Refresh();
            yield return null;
            Assert.That(FindButton("HintButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindChild("Suggestions").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindChild("HelpPanel").gameObject.activeInHierarchy, Is.True);
        }

        // 추천 단어는 빈 줄 대신 얇은 구분선으로 나눠, 빈 줄 높이만큼 공간을 버리지 않는다.
        [UnityTest]
        public IEnumerator SuggestedWords_AreSplitByThinDividers_InsteadOfBlankLines()
        {
            content.SuggestWords = new[]
            {
                "for here — 매장에서 먹을게요",
                "to go — 포장해 주세요",
                "the usual — 늘 먹던 걸로",
            };
            view.Refresh();
            yield return null;

            Transform card = FindChild("Suggestions/WordsCard");
            Assert.That(card.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("추천 단어"));
            List<TMP_Text> rows = ActiveChildren<TMP_Text>(card, "Word");
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].text, Does.Contain("for here").And.Contain("매장에서 먹을게요"));
            Assert.That(rows[2].text, Does.Contain("the usual").And.Contain("늘 먹던 걸로"));
            Assert.That(rows.TrueForAll(row => !row.text.Contains("\n\n")), Is.True);
            Assert.That(FindChild("SuggestedWordsBody").gameObject.activeInHierarchy, Is.False);

            List<RectTransform> dividers = ActiveChildren<RectTransform>(card, "Divider");
            Assert.That(dividers.Count, Is.EqualTo(2), "구분선은 단어 사이에만 있어야 합니다.");
            var list = (RectTransform)rows[0].transform.parent;
            for (int i = 0; i < dividers.Count; i++)
            {
                Rect above = RectIn(list, rows[i].rectTransform);
                Rect line = RectIn(list, dividers[i]);
                Rect below = RectIn(list, rows[i + 1].rectTransform);
                Assert.That(line.center.y, Is.LessThan(above.yMin).And.GreaterThan(below.yMax));
                Assert.That(line.height, Is.LessThanOrEqualTo(2));
                Assert.That(line.width, Is.EqualTo(list.rect.width).Within(1));
                Assert.That(
                    above.yMin - below.yMax,
                    Is.LessThan(rows[i].fontSize * .8f),
                    "단어 사이 간격이 빈 줄 한 줄보다 좁아야 합니다."
                );
            }
        }

        [UnityTest]
        public IEnumerator SuggestedWords_WhileLoading_ShowOnlyTheMessage_AndKeepNoStaleRows()
        {
            content.SuggestWords = new[]
            {
                "for here — 매장에서 먹을게요",
                "to go — 포장해 주세요",
            };
            view.Refresh();
            yield return null;

            content.SuggestWords = null;
            content.IsSuggestingWords = true;
            view.Refresh();
            yield return null;

            Transform card = FindChild("Suggestions/WordsCard");
            TMP_Text message = FindChild("SuggestedWordsBody").GetComponent<TMP_Text>();
            Assert.That(message.gameObject.activeInHierarchy, Is.True);
            Assert.That(message.text, Is.EqualTo("추천 단어를 고르고 있어요..."));
            Assert.That(ActiveChildren<TMP_Text>(card, "Word"), Is.Empty);
            Assert.That(ActiveChildren<RectTransform>(card, "Divider"), Is.Empty);

            content.SuggestWords = new[] { "the usual — 늘 먹던 걸로" };
            content.IsSuggestingWords = false;
            view.Refresh();
            yield return null;

            Assert.That(message.gameObject.activeInHierarchy, Is.False);
            Assert.That(ActiveChildren<TMP_Text>(card, "Word").Count, Is.EqualTo(1));
            Assert.That(ActiveChildren<RectTransform>(card, "Divider"), Is.Empty);
        }

        private static List<T> ActiveChildren<T>(Transform root, string name)
            where T : Component
        {
            var found = new List<T>();
            foreach (T child in root.GetComponentsInChildren<T>(false))
            {
                if (child.name == name)
                    found.Add(child);
            }
            return found;
        }

        // 대상 사각형을 기준 사각형 좌표로 옮긴다.
        private static Rect RectIn(RectTransform space, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 min = space.InverseTransformPoint(corners[0]);
            Vector3 max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private TMP_InputField Input => panelObject.GetComponentInChildren<TMP_InputField>(true);

        // 버튼 가운데를 실제 화면 좌표로 눌러, 다른 UI에 가려지지 않고 버튼이 클릭을 받는지 본다.
        private static void ClickCenterOf(Button button)
        {
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center)
                ),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "버튼 위치에 클릭을 받는 UI가 없습니다.");

            GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                hits[0].gameObject
            );
            Assert.That(clicked, Is.EqualTo(button.gameObject));
            ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in panelObject.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                    return button;
            }

            Assert.Fail($"{name}을 찾지 못했습니다.");
            return null;
        }

        private Transform FindChild(string path)
        {
            if (path.Contains("/"))
                return panelObject.transform.Find(path);

            foreach (Transform child in panelObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == path)
                    return child;
            }

            return null;
        }

        /// <summary>NPC가 첫 대사를 마치고 사용자가 답할 차례인 서버 대화.</summary>
        private class HintContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Hi, welcome in. What would you like?",
                null
            );

            public string UserTranslation { get; set; }
            public string NpcName => "Alex";
            public DialogueLine CurrentLine { get; set; } = Opening;
            public IReadOnlyList<DialogueLine> Lines => new[] { CurrentLine };
            public DialogueLine LastNpcLine => CurrentLine;
            public bool IsGuided { get; set; }
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => "아이스라테 한 잔 주시겠어요?";
            public string ExampleSentence => null;
            public bool IsBusy => false;
            public string StatusMessage => null;
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords { get; set; }
            public bool IsSuggestingWords { get; set; }
            public bool CanSend => true;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText { get; set; }
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => false;
            public bool SpeaksNpcLines => false;
            public bool IsNpcSpeechReady => false;
        }
    }
}
#endif
