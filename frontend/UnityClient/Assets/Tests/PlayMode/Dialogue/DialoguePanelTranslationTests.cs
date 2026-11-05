#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
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
            public IReadOnlyList<DialogueLine> Lines =>
                new[] { DialogueLine.FromUser("카페 어디야?"), Npc };
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
