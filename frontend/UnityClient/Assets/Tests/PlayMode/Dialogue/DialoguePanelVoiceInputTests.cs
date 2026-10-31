#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using A305.Voice;
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
    /// 음성 대화에서 버튼을 누르고 있어야 녹음된다는 것, 녹음 중·녹음 후에 할 일을 실제 대화창이 알려 주는지 확인한다.
    /// 서버와 마이크 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelVoiceInputTests
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
        public IEnumerator TalkButton_TellsToHoldWhileSpeaking()
        {
            yield return Show(new VoiceContent(status: null));

            Assert.That(TalkLabel().text, Is.EqualTo("꾹 눌러 말하기"));
        }

        [UnityTest]
        public IEnumerator HoldingTalkButton_ShowsListening_UntilReleased()
        {
            yield return Show(new VoiceContent(status: null));
            var hold = TalkButton().GetComponent<HoldButton>();
            var pointer = new PointerEventData(EventSystem.current);

            hold.OnPointerDown(pointer);
            Assert.That(TalkLabel().text, Is.EqualTo("듣는 중…"));

            hold.OnPointerUp(pointer);
            Assert.That(TalkLabel().text, Is.EqualTo("꾹 눌러 말하기"));
        }

        [UnityTest]
        public IEnumerator WhileListening_StatusLineTellsToReleaseWhenDone()
        {
            yield return Show(new VoiceContent(VoiceInputNotice.Listening(15)));

            Assert.That(Status().gameObject.activeInHierarchy, Is.True);
            Assert.That(Status().text, Does.Contain("손을 떼"));
            AssertFitsInPanel(Status());
        }

        // 버튼을 떼기 전에 녹음이 끝났으면 이유가 앞에 붙어 안내가 길어지므로 모든 이유를 확인한다.
        [UnityTest]
        public IEnumerator AfterTranscription_StatusLineAsksToCheckAndSend(
            [Values] RecordingEndReason endReason
        )
        {
            yield return Show(new VoiceContent(VoiceInputNotice.Transcribed(endReason)));

            Assert.That(Status().gameObject.activeInHierarchy, Is.True);
            Assert.That(Status().text, Does.Contain("[보내기]"));
            AssertFitsInPanel(Status());
        }

        [UnityTest]
        public IEnumerator SentenceToSend_HighlightsSendButton()
        {
            yield return Show(new VoiceContent(status: null));
            Color idle = SendButton().image.color;

            view.SetInputText("Hi, I have a reservation.");
            Assert.That(SendButton().image.color, Is.Not.EqualTo(idle));

            view.SetInputText("");
            Assert.That(SendButton().image.color, Is.EqualTo(idle));
        }

        private IEnumerator Show(IDialogueContent content)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            // 4:3에 가까운 창처럼 캔버스 폭이 1600이면 상태 줄이 가장 좁은 넓은 화면 배치가 된다.
            var scaler = panelObject.GetComponentInChildren<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Screen.width / NarrowCanvasWidth;
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(content);
            yield return null;
            view.UpdateLayout();
        }

        private const float NarrowCanvasWidth = 1600f;

        private void AssertFitsInPanel(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing, Is.False, "안내가 한 줄에 들어가야 합니다.");
            Assert.That(RightEdge(text.rectTransform), Is.LessThanOrEqualTo(RightEdge(Panel())));
            float textRight = text.rectTransform.TransformPoint(text.textBounds.max).x;
            Assert.That(
                textRight,
                Is.LessThanOrEqualTo(RightEdge(text.rectTransform) + 0.5f),
                "안내 글자가 상태 줄 밖으로 넘칩니다."
            );
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

        private TMP_Text Status() =>
            (TMP_Text)new SerializedObject(view).FindProperty("statusText").objectReferenceValue;

        private Button TalkButton() => ButtonNamed("TalkButton");

        private TMP_Text TalkLabel() =>
            TalkButton().transform.Find("Label").GetComponent<TMP_Text>();

        private Button SendButton() => ButtonNamed("SendButton");

        private Button ButtonNamed(string name) =>
            panelObject.GetComponentsInChildren<Button>(true).Single(button => button.name == name);

        private class VoiceContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Welcome to Maple House. How can I help you?",
                null
            );

            public VoiceContent(string status)
            {
                StatusMessage = status;
            }

            public string NpcName => "Jake";
            public IReadOnlyList<DialogueLine> Lines => new[] { Opening };
            public DialogueLine LastNpcLine => Opening;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;

            // 녹음 중과 전사한 뒤에는 서버 요청이 없어 바쁘지 않다.
            public bool IsBusy => false;
            public string StatusMessage { get; }
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => null;
            public bool CanSend => true;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public bool CanReplaySpeech => false;
        }
    }
}
#endif
