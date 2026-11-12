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
            Assert.That(Caption(subtitle), Is.EqualTo("자막 켜기"));
        }

        [UnityTest]
        public IEnumerator NewConversation_HidesSubtitlesEvenWhenPreviouslySavedOn()
        {
            NpcSubtitlePreference.ShowsSubtitles = true;
            NpcSubtitlePreference.Reload();

            yield return Show(new SpokenContent(speaksNpcLines: true));

            Assert.That(VisibleNpcLetters(), Is.Zero);
            Assert.That(ActiveCovers(), Has.Count.EqualTo(7));
            Assert.That(Caption(FindButton("SubtitleButton")), Is.EqualTo("자막 켜기"));
        }

        [UnityTest]
        public IEnumerator SubtitlesEnabledDuringConversation_StayOnUntilTheNextConversation()
        {
            var content = new SpokenContent(speaksNpcLines: true);
            yield return Show(content);
            Button subtitle = FindButton("SubtitleButton");
            ClickCenterOf(subtitle);
            yield return null;

            content.Waiting = DialogueWait.NpcReply;
            view.Refresh();
            content.Waiting = DialogueWait.None;
            view.Refresh();
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(Caption(subtitle), Is.EqualTo("자막 끄기"));

            view.Hide();
            view.Show(new SpokenContent(speaksNpcLines: true));
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.Zero);
            Assert.That(ActiveCovers(), Has.Count.EqualTo(7));
            Assert.That(Caption(subtitle), Is.EqualTo("자막 켜기"));
        }

        [UnityTest]
        public IEnumerator SubtitleButton_Clicked_TogglesCaptionAndRemembersPreference()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true));
            Button subtitle = FindButton("SubtitleButton");

            ClickCenterOf(subtitle);
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(ActiveCovers(), Is.Empty);
            Assert.That(Caption(subtitle), Is.EqualTo("자막 끄기"));
            NpcSubtitlePreference.Reload();
            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.True);

            ClickCenterOf(subtitle);
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.Zero);
            Assert.That(Caption(subtitle), Is.EqualTo("자막 켜기"));
            NpcSubtitlePreference.Reload();
            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.False);
        }

        // 자막을 켜면 채운 강조색으로 바뀌지만, 둥근 모서리가 버튼 높이에 맞아야 한다.
        [UnityTest]
        public IEnumerator SubtitleButton_KeepsRoundedCornersAndHighlightsWhenEnabled()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true));

            Button subtitle = FindButton("SubtitleButton");
            Image image = subtitle.GetComponent<Image>();
            Sprite originalSprite = image.sprite;
            Color originalColor = image.color;
            Material originalMaterial = image.material;
            float originalPixelsPerUnit = image.pixelsPerUnitMultiplier;
            ClickCenterOf(subtitle);
            yield return null;

            float buttonHeight = image.rectTransform.rect.height;
            float roundedCorners =
                (image.sprite.border.y + image.sprite.border.w)
                / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
            Assert.That(Caption(subtitle), Is.EqualTo("자막 끄기"));
            Assert.That(roundedCorners, Is.InRange(buttonHeight * .9f, buttonHeight));
            Assert.That(image.material, Is.Not.SameAs(originalMaterial));
            Assert.That(image.color, Is.Not.EqualTo(originalColor));

            ClickCenterOf(subtitle);
            yield return null;

            Assert.That(Caption(subtitle), Is.EqualTo("자막 켜기"));
            Assert.That(image.sprite, Is.SameAs(originalSprite));
            Assert.That(image.color, Is.EqualTo(originalColor));
            Assert.That(image.material, Is.SameAs(originalMaterial));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(originalPixelsPerUnit));
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

        [UnityTest]
        public IEnumerator SubtitlesOff_ReceivingFirstLine_HidesItUntilSpeechFails()
        {
            var content = new SpokenContent(speaksNpcLines: true, isNpcSpeechReady: false)
            {
                IsReceivingNpcReply = true,
            };
            yield return Show(content);

            Assert.That(VisibleNpcLetters(), Is.Zero, "음성이 오기 전에 받은 첫 대사도 가립니다.");
            Assert.That(ActiveCovers(), Is.Not.Empty);

            content.IsReceivingNpcReply = false;
            view.Refresh();
            yield return null;

            Assert.That(VisibleNpcLetters(), Is.EqualTo(NpcSentence.Replace(" ", "").Length));
            Assert.That(ActiveCovers(), Is.Empty);
        }

        // 대답 로딩 카드가 대사 칸을 덮는 동안 지난 대사를 가린 막대가 로딩 위로 비치지 않고, 로딩이 끝나면 다시 덮는다.
        [UnityTest]
        public IEnumerator WaitingForReply_HidesWordCoversUntilTheReplyArrives()
        {
            var content = new SpokenContent(speaksNpcLines: true);
            yield return Show(content);

            content.Waiting = DialogueWait.NpcReply;
            view.Refresh();
            yield return null;

            Assert.That(ActiveCovers(), Is.Empty);

            content.Waiting = DialogueWait.None;
            view.Refresh();
            yield return null;

            Assert.That(ActiveCovers(), Has.Count.EqualTo(7));
        }

        // [자막]과 짝인 재생 버튼은 이미 나온 NPC 대사를 다시 들려준다는 뜻이 드러나게 [다시 듣기]로 적고 한 줄에 담는다.
        [UnityTest]
        public IEnumerator ReplayButton_ReadsListenAgainOnOneLine()
        {
            yield return Show(new SpokenContent(speaksNpcLines: true));
            TMP_Text caption = FindButton("ReplayButton").GetComponentInChildren<TMP_Text>(true);
            caption.ForceMeshUpdate();

            Assert.That(caption.text, Is.EqualTo("다시 듣기"));
            Assert.That(caption.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(caption.isTextOverflowing, Is.False);
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

        /// <summary>NPC가 첫 대사를 마치고 사용자가 답할 차례인 대화. 대답을 기다리는 상태로 바꿔 볼 수 있다.</summary>
        private class SpokenContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(NpcSentence, null);

            public SpokenContent(bool speaksNpcLines, bool isNpcSpeechReady = true)
            {
                SpeaksNpcLines = speaksNpcLines;
                IsNpcSpeechReady = speaksNpcLines && isNpcSpeechReady;
            }

            public string UserTranslation { get; set; }
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
            public DialogueWait Waiting { get; set; }
            public bool CanReplaySpeech => true;
            public bool SpeaksNpcLines { get; }
            public bool IsReceivingNpcReply { get; set; }
            public bool IsNpcSpeechReady { get; }
        }
    }
}
#endif
