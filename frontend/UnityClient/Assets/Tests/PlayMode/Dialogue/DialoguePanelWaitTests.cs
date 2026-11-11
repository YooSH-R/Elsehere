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
    /// NPC 대답과 피드백을 기다리는 동안 버튼 줄 옆 작은 글씨 대신 눈에 띄는 로딩을 보이는지 실제 대화창으로 확인한다.
    /// 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelWaitTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator PreparingSpeech_KeepsTheNpcSentenceBehindTheWaitCard()
        {
            yield return Show(new Content(DialogueWait.NpcSpeech, null));
            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.True);
            Assert.That(Field<TMP_Text>("linesText").enabled, Is.False);
            Assert.That(Field<TMP_Text>("waitText").text, Does.Contain("음성"));
        }

        [UnityTest]
        public IEnumerator WaitingForTheNpcReply_ShowsLoadingInPlaceOfTheLines()
        {
            yield return Show(
                new Content(DialogueWait.NpcReply, "Jake이(가) 대답을 생각하고 있어요...")
            );

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.True);
            Assert.That(
                Field<TMP_Text>("waitText").text,
                Does.Contain("Jake의 대답을 기다리고 있어요")
            );
            Assert.That(Field<TMP_Text>("statusText").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("feedbackWait").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator WaitingForTheOpeningLine_ShowsLoadingInPlaceOfTheLines()
        {
            yield return Show(new Content(DialogueWait.Opening, "대화를 시작하고 있어요..."));

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.True);
            Assert.That(Field<TMP_Text>("waitText").text, Does.Contain("대화를 준비하고 있어요"));
        }

        // 피드백은 오래 걸리므로 화면 전체를 덮는 가운데 로딩 창으로 알린다.
        [UnityTest]
        public IEnumerator WaitingForFeedback_CoversTheScreenWithALoadingWindow()
        {
            yield return Show(
                new Content(
                    DialogueWait.Feedback,
                    "대화를 마치고 피드백을 만들고 있어요. 잠시만 기다려 주세요."
                )
            );

            GameObject window = Field<GameObject>("feedbackWait");
            Assert.That(window.activeInHierarchy, Is.True);
            Assert.That(Texts(window), Does.Contain("피드백을 만들고 있어요"));
            var screen = (RectTransform)window.transform.parent;
            Assert.That(((RectTransform)window.transform).rect.size, Is.EqualTo(screen.rect.size));
            Assert.That(Field<TMP_Text>("statusText").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator TaxiArrivalStaysAboveFeedbackAndHidesAfterAlighting()
        {
            yield return Show(new Content(DialogueWait.Feedback, null));
            GameObject feedback = Field<GameObject>("feedbackWait");
            view.ShowTaxiArrival();
            var arrival = feedback.transform.parent.Find("TaxiArrivalStatus") as RectTransform;

            Assert.That(arrival, Is.Not.Null);
            Assert.That(arrival.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                arrival.GetSiblingIndex(),
                Is.GreaterThan(feedback.transform.GetSiblingIndex())
            );
            Assert.That(arrival.anchorMax.y, Is.EqualTo(1));
            Assert.That(arrival.GetComponent<Image>().raycastTarget, Is.False);

            view.SetVehicleLayout(false);
            Assert.That(arrival.gameObject.activeSelf, Is.False);
            Assert.That(feedback.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator NotWaiting_HidesBothLoadingViews()
        {
            yield return Show(new Content(DialogueWait.None, null));

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("feedbackWait").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator EndNotice_HighlightsOnlyTheEndButtonUntilFeedbackStarts()
        {
            var conversation = new ServerConversation("CAFE_STAFF", "Mia", null, canTalk: true);
            yield return Show(conversation);
            Graphic button = view.EndButtonRect.GetComponent<Button>().targetGraphic;
            Assert.That(button.GetComponent<Outline>(), Is.Null);

            // 서버 호출 없이 종료된 턴과 실제 안내 문구를 화면에 넘긴다.
            typeof(ServerConversation)
                .GetProperty(nameof(ServerConversation.IsEnded))
                .SetValue(conversation, true);
            conversation.ShowMessage(
                "Mia이(가) 대화를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요."
            );
            view.Refresh();

            var highlight = button.GetComponent<Outline>();
            Assert.That(highlight, Is.Not.Null, "종료 안내가 보이면 버튼을 강조합니다.");
            Assert.That(highlight.enabled, Is.True);
            Assert.That(view.EndButtonRect.GetComponent<Button>().IsInteractable(), Is.True);

            typeof(ServerConversation)
                .GetProperty(nameof(ServerConversation.Waiting))
                .SetValue(conversation, DialogueWait.Feedback);
            view.Refresh();
            Assert.That(highlight.enabled, Is.False);
            Assert.That(Field<GameObject>("feedbackWait").activeInHierarchy, Is.True);
        }

        // 멈춘 화면처럼 보이지 않게 로딩 고리가 계속 돈다.
        [UnityTest]
        public IEnumerator LoadingRing_KeepsTurning()
        {
            yield return Show(new Content(DialogueWait.NpcReply, null));
            Transform ring = Field<GameObject>("waitPanel")
                .GetComponentInChildren<LoadingSpinner>()
                .transform;
            float before = ring.eulerAngles.z;

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(Mathf.DeltaAngle(before, ring.eulerAngles.z), Is.Not.EqualTo(0f));
        }

        private IEnumerator Show(IDialogueContent content)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(content);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static string Texts(GameObject root)
        {
            var texts = new List<string>();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>())
                texts.Add(text.text);
            return string.Join("\n", texts);
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private class Content : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Welcome to Maple House. How can I help you?",
                null
            );

            public Content(DialogueWait waiting, string status)
            {
                Waiting = waiting;
                StatusMessage = status;
            }

            public DialogueWait Waiting { get; }
            public string UserTranslation { get; set; }
            public string NpcName => "Jake";
            public IReadOnlyList<DialogueLine> Lines => new[] { Opening };
            public DialogueLine LastNpcLine => Opening;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;
            public bool IsBusy => Waiting != DialogueWait.None;
            public string StatusMessage { get; }
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => null;
            public bool IsSuggestingWords => false;
            public bool CanSend => !IsBusy;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public bool CanReplaySpeech => false;
            public bool SpeaksNpcLines => false;
            public bool IsNpcSpeechReady => false;
        }
    }
}
#endif
