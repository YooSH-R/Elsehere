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
    /// <summary>실제 대화창 프리팹에서 [자막]으로 NPC 대사를 가렸다 보였다 하는지 확인한다.</summary>
    public class DialoguePanelSubtitleTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string NpcSentence = "Hi, welcome in. What would you like?";

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;
        private bool savedPreference;

        [SetUp]
        public void StartWithSubtitlesOff()
        {
            savedPreference = NpcSubtitlePreference.ShowsSubtitles;
            NpcSubtitlePreference.ShowsSubtitles = false;
        }

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
            NpcSubtitlePreference.ShowsSubtitles = savedPreference;
        }

        // 글자는 하나도 보이지 않고, 띄어쓰기로 나뉜 단어 7개 자리가 회색 막대로 덮인다.
        [UnityTest]
        public IEnumerator SubtitlesOff_EachNpcWordIsCoveredAndNoLetterIsVisible()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true));

            Assert.That(VisibleNpcLetters(), Is.Zero);
            Assert.That(ActiveCovers(), Has.Count.EqualTo(7));
            Button subtitle = FindButton("SubtitleButton");
            Assert.That(subtitle.transform.parent.name, Is.EqualTo("ToolRow"));
            Assert.That(subtitle.gameObject.activeInHierarchy, Is.True);
            Assert.That(Caption(subtitle), Is.EqualTo("자막 끔"));
        }

        [UnityTest]
        public IEnumerator SubtitleButton_Clicked_ShowsTheNpcLineAndRemembersIt()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true));
            Button subtitle = FindButton("SubtitleButton");

            ClickCenterOf(subtitle);
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(ActiveCovers(), Is.Empty);
            Assert.That(Caption(subtitle), Is.EqualTo("자막 켬"));
            NpcSubtitlePreference.Reload();
            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.True);
        }

        // 켜 둔 채 대화를 열면 레이아웃이 버튼 높이를 정하기 전에 [자막 켬] 알약을 칠한다.
        // 높이가 정해진 뒤에도 알약 위아래 모서리가 버튼 높이를 넘으면 가운데 줄이 테이프처럼 양옆으로 삐져나온다.
        [UnityTest]
        public IEnumerator SubtitlesOn_PillCornersFitInsideTheButtonHeight()
        {
            NpcSubtitlePreference.ShowsSubtitles = true;

            yield return Show(new SpokenContent(speaksNpcLines: true));

            var image = FindButton("SubtitleButton").GetComponent<Image>();
            float corners =
                (image.sprite.border.y + image.sprite.border.w)
                / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
            Assert.That(corners, Is.LessThan(image.rectTransform.rect.height));
        }

        // 음성 없이 글로만 진행하는 대본 대화는 가리면 대사를 알 방법이 없다.
        [UnityTest]
        public IEnumerator SilentConversation_ShowsTheLineWithoutSubtitleButton()
        {
            yield return Show(new SpokenContent(speaksNpcLines: false));

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(ActiveCovers(), Is.Empty);
            Assert.That(FindButton("SubtitleButton").gameObject.activeSelf, Is.False);
        }

        // 음성 생성에 실패한 대사는 들을 수 없으니 자막을 꺼 두었어도 글자로 보인다. [자막]은 다음 대사를 위해 그대로 둔다.
        [UnityTest]
        public IEnumerator SubtitlesOffButSpeechFailed_ShowsTheLine()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true, isNpcSpeechReady: false));

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(ActiveCovers(), Is.Empty);
            Assert.That(FindButton("SubtitleButton").gameObject.activeSelf, Is.True);
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

        private TMP_Text LinesText() => FindChild("Lines").GetComponent<TMP_Text>();

        // NPC 대사 글자 중 화면에 보이는(투명하지 않은) 글자 수. 이름(Alex) 뒤 대사만 센다.
        private int VisibleNpcLetters()
        {
            TMP_Text lines = LinesText();
            lines.ForceMeshUpdate();
            TMP_TextInfo info = lines.textInfo;
            int sentenceStart = lines.GetParsedText().IndexOf("Alex") + "Alex".Length;
            int visible = 0;
            for (int i = sentenceStart; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (character.isVisible && character.color.a > 0)
                    visible++;
            }

            return visible;
        }

        private List<Image> ActiveCovers()
        {
            var covers = new List<Image>();
            foreach (Image image in LinesText().GetComponentsInChildren<Image>())
            {
                if (image.name == NpcLineCovers.CoverName)
                    covers.Add(image);
            }

            return covers;
        }

        private static string Caption(Button button) =>
            button.GetComponentInChildren<TMP_Text>(true).text;

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

        private Transform FindChild(string name)
        {
            foreach (Transform child in panelObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            Assert.Fail($"{name}을 찾지 못했습니다.");
            return null;
        }

        /// <summary>NPC가 첫 대사를 마치고 사용자가 답할 차례인 대화.</summary>
        private class SpokenContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(NpcSentence, null);

            public SpokenContent(bool speaksNpcLines, bool isNpcSpeechReady = true)
            {
                SpeaksNpcLines = speaksNpcLines;
                IsNpcSpeechReady = speaksNpcLines && isNpcSpeechReady;
            }

            public string NpcName => "Alex";
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
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => true;
            public bool SpeaksNpcLines { get; }
            public bool IsNpcSpeechReady { get; }
        }
    }
}
#endif
