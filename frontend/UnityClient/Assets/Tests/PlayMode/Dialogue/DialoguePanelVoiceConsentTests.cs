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
    /// <summary>
    /// 음성 처리에 동의하지 않은 대화에서 [말하기]를 회색으로 보이고, 누르면 녹음 대신 동의 창이 떠 그 자리에서
    /// 동의할 수 있는지 실제 대화창 프리팹으로 확인한다. 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelVoiceConsentTests
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
        public IEnumerator WithoutVoiceConsent_ShowsGrayTalkButtonAndTellsToPressIt()
        {
            yield return Show(new FreeTalkContent(canTalk: false));

            Assert.That(TalkButton().gameObject.activeInHierarchy, Is.True);
            Assert.That(TalkButtonAlpha(), Is.LessThan(1f), "동의 전에는 회색으로 보여야 합니다.");
            TMP_Text status = Status();
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Assert.That(status.text, Does.Contain("[말하기]").And.Contain("음성 처리"));
            // 안내가 대화창 밖으로 넘치지 않고 한 줄에 들어간다.
            status.ForceMeshUpdate();
            Assert.That(status.isTextOverflowing, Is.False);
            Assert.That(RightEdge(status.rectTransform), Is.LessThanOrEqualTo(RightEdge(Panel())));
        }

        [UnityTest]
        public IEnumerator WithoutVoiceConsent_PressingTalk_OpensConsentDialogInsteadOfRecording()
        {
            yield return Show(new FreeTalkContent(canTalk: false));
            int starts = 0;
            view.TalkStartRequested += () => starts++;

            ClickCenterOf(TalkButton());

            Assert.That(starts, Is.EqualTo(0), "동의 전에는 녹음하지 않습니다.");
            Assert.That(view.VoiceConsentDialog.IsVisible, Is.True);
            Assert.That(DialogButton("DocumentButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(DialogButton("CloseButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(DialogButton("AgreeButton").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ConsentDialog_Close_HidesItWithoutAgreeing()
        {
            yield return Show(new FreeTalkContent(canTalk: false));
            int agrees = 0;
            view.VoiceConsentDialog.AgreeRequested += () => agrees++;
            yield return OpenConsentDialog();

            ClickCenterOf(DialogButton("CloseButton"));

            Assert.That(view.VoiceConsentDialog.IsVisible, Is.False);
            Assert.That(agrees, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ConsentDialog_AgreeAndDocumentButtons_AskConversationToAct()
        {
            yield return Show(new FreeTalkContent(canTalk: false));
            int agrees = 0;
            int documents = 0;
            view.VoiceConsentDialog.AgreeRequested += () => agrees++;
            view.VoiceConsentDialog.DocumentRequested += () => documents++;
            yield return OpenConsentDialog();

            ClickCenterOf(DialogButton("DocumentButton"));
            ClickCenterOf(DialogButton("AgreeButton"));

            Assert.That(documents, Is.EqualTo(1));
            Assert.That(agrees, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ConsentDialog_WhileSaving_BlocksAgreeAndShowsFailureReason()
        {
            yield return Show(new FreeTalkContent(canTalk: false));
            ClickCenterOf(TalkButton());

            view.VoiceConsentDialog.ShowSaving();
            Assert.That(DialogButton("AgreeButton").interactable, Is.False);

            view.VoiceConsentDialog.ShowFailure(VoiceConsentAgreement.SaveFailedMessage);
            Assert.That(DialogButton("AgreeButton").interactable, Is.True);
            TMP_Text status = DialogText("Status");
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Assert.That(status.text, Is.EqualTo(VoiceConsentAgreement.SaveFailedMessage));
        }

        [UnityTest]
        public IEnumerator HidingPanel_ClosesConsentDialog()
        {
            yield return Show(new FreeTalkContent(canTalk: false));
            ClickCenterOf(TalkButton());

            view.Hide();

            Assert.That(view.VoiceConsentDialog.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator WithVoiceConsent_ShowsTalkButtonWithoutTheConsentGuide()
        {
            yield return Show(new FreeTalkContent(canTalk: true));

            Assert.That(Status().gameObject.activeInHierarchy, Is.False);
            Assert.That(TalkButton().gameObject.activeInHierarchy, Is.True);
            Assert.That(TalkButtonAlpha(), Is.EqualTo(1f));
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
        }

        // 회색 [말하기]로 동의 창을 연다. 창 안 버튼은 다음 프레임에 배치가 끝난 뒤 누른다.
        private IEnumerator OpenConsentDialog()
        {
            ClickCenterOf(TalkButton());
            yield return null;
        }

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

        private RectTransform Panel() =>
            (RectTransform)
                (
                    (GameObject)
                        new SerializedObject(view).FindProperty("panel").objectReferenceValue
                ).transform;

        private static float RightEdge(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[2].x;
        }

        // 입력칸 위의 상태 줄. 녹음 중·오류 안내와 같은 자리다.
        private TMP_Text Status() =>
            (TMP_Text)new SerializedObject(view).FindProperty("statusText").objectReferenceValue;

        private Button TalkButton() =>
            panelObject
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "TalkButton");

        private float TalkButtonAlpha() =>
            TalkButton().TryGetComponent(out CanvasGroup group) ? group.alpha : 1f;

        private Transform Dialog() => panelObject.transform.Find("VoiceConsent/Dialog");

        private Button DialogButton(string name) =>
            Dialog().Find("Buttons/" + name).GetComponent<Button>();

        private TMP_Text DialogText(string name) => Dialog().Find(name).GetComponent<TMP_Text>();

        private class FreeTalkContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Welcome to Maple House. How can I help you?",
                null
            );

            public FreeTalkContent(bool canTalk)
            {
                CanTalk = canTalk;
            }

            public string NpcName => "Jake";
            public IReadOnlyList<DialogueLine> Lines => new[] { Opening };
            public DialogueLine LastNpcLine => Opening;
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
            public bool CanTalk { get; }
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => false;
        }
    }
}
#endif
