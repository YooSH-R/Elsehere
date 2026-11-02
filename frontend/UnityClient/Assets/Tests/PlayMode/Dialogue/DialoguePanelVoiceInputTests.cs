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
    /// 음성 대화에서 [말하기]를 한 번 눌러 녹음을 시작하고 다시 눌러 끝내는지, 녹음 중 목소리가 들리면 초록불이 켜지는지,
    /// 녹음 중·녹음 후에 할 일을 실제 대화창이 알려 주는지 확인한다. 서버와 마이크 없이 대화 내용을 흉내 낸다.
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
        public IEnumerator TalkButton_ClickedTwice_StartsThenStopsRecording()
        {
            yield return Show(new VoiceContent(status: null));
            int starts = 0;
            int stops = 0;
            view.TalkStartRequested += () => starts++;
            view.TalkStopRequested += () => stops++;

            Assert.That(TalkLabel().text, Is.EqualTo("말하기"));
            ClickCenterOf(TalkButton());
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(stops, Is.EqualTo(0));

            view.ShowVoiceInput(isRecording: true, isHearingVoice: false);
            Assert.That(TalkLabel().text, Is.EqualTo("종료"));
            ClickCenterOf(TalkButton());

            Assert.That(starts, Is.EqualTo(1));
            Assert.That(stops, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator VoiceLight_TurnsGreenOnlyWhileVoiceIsHeard()
        {
            yield return Show(new VoiceContent(status: null));
            Image light = TalkButton().transform.Find("VoiceLight").GetComponent<Image>();
            Assert.That(light.gameObject.activeInHierarchy, Is.False);

            view.ShowVoiceInput(isRecording: true, isHearingVoice: false);
            Assert.That(light.gameObject.activeInHierarchy, Is.True);
            Assert.That(light.color, Is.Not.EqualTo(DialoguePanelView.VoiceHeardColor));

            view.ShowVoiceInput(isRecording: true, isHearingVoice: true);
            Assert.That(light.color, Is.EqualTo(DialoguePanelView.VoiceHeardColor));

            // 시간·무음으로 녹음이 저절로 끝나도 버튼은 [말하기]로 돌아오고 불이 꺼진다.
            view.ShowVoiceInput(isRecording: false, isHearingVoice: false);
            Assert.That(TalkLabel().text, Is.EqualTo("말하기"));
            Assert.That(light.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator WhileListening_StatusLineTellsToPressStopWhenDone()
        {
            yield return Show(new VoiceContent(VoiceInputNotice.Listening(15)));

            Assert.That(Status().gameObject.activeInHierarchy, Is.True);
            Assert.That(Status().text, Does.Contain("[종료]"));
            AssertFitsInPanel(Status());
        }

        [UnityTest]
        public IEnumerator RecordingWithoutSpeech_StatusLineFits()
        {
            yield return Show(new VoiceContent(VoiceInputNotice.TooShort));

            Assert.That(Status().text, Does.Contain("[말하기]").And.Contain("[종료]"));
            AssertFitsInPanel(Status());
        }

        // [종료]를 누르기 전에 녹음이 끝났으면 이유가 앞에 붙어 안내가 길어지므로 모든 이유를 확인한다.
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
        }
    }
}
#endif
