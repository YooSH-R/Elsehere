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
    /// 대사 칸에는 가장 최근 두 줄(바로 앞 대사와 그다음 대사)만 보이고, 그 전 대사는 위로 스크롤해 다시 본다.
    /// 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelLatestLinesTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const float CanvasWidth = 1600f;

        private static readonly DialogueLine[] Conversation =
        {
            DialogueLine.FromNpc(
                "Hello again. What can I help you with?",
                "다시 오셨네요. 무엇을 도와드릴까요?"
            ),
            DialogueLine.FromUser("i lost my bag. i think i don't take my bag in cafeteria."),
            DialogueLine.FromNpc(
                "You left your small bag at the café, not here. When will you return to check in?",
                "작은 가방은 여기가 아니라 카페에 두고 오셨어요. 체크인하러 언제 돌아오세요?"
            ),
            DialogueLine.FromUser("i return in an hour"),
            DialogueLine.FromNpc(
                "All right. Your large suitcase is stored here. We'll see you in an hour.",
                "알겠습니다. 큰 캐리어는 여기 보관되어 있어요. 한 시간 뒤에 봬요."
            ),
        };

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
        public IEnumerator LongConversation_ShowsOnlyTheLatestTwoLines()
        {
            yield return Show(Conversation);

            TMP_Text lines = Field<TMP_Text>("linesText");
            var scroll = Field<ScrollRect>("historyScroll");
            float viewport = ((RectTransform)scroll.transform).rect.height;
            string latestTwo = DialogueLinesText.Format(
                Conversation.Skip(Conversation.Length - 2).ToArray(),
                "Jake",
                false,
                null
            );
            float latestHeight = lines
                .GetPreferredValues(latestTwo, lines.rectTransform.rect.width, 0)
                .y;

            Assert.That(
                viewport,
                Is.EqualTo(latestHeight).Within(2f),
                "최근 두 줄 높이만큼 보입니다."
            );
            Assert.That(
                lines.preferredHeight,
                Is.GreaterThan(viewport + 1f),
                "그 전 대사는 칸 밖(위)에 있습니다."
            );
            Assert.That(
                scroll.verticalNormalizedPosition,
                Is.EqualTo(0f).Within(.01f),
                "맨 아래(최근 대사)가 보입니다."
            );
            Assert.That(scroll.vertical, Is.True, "위로 스크롤해 지난 대사를 봅니다.");
        }

        // "나"와 "Jake"처럼 이름 길이가 달라도 두 줄의 문장 첫 글자가 같은 자리에서 시작한다.
        [UnityTest]
        public IEnumerator Sentences_StartAtTheSameColumnWhateverTheSpeaker()
        {
            yield return Show(Conversation);

            TMP_Text lines = Field<TMP_Text>("linesText");
            lines.ForceMeshUpdate();
            TMP_TextInfo info = lines.textInfo;
            float mine = FirstLetterX(info, "i return");
            float jake = FirstLetterX(info, "All right");
            Assert.That(mine, Is.EqualTo(jake).Within(1f));
        }

        private static float FirstLetterX(TMP_TextInfo info, string sentence)
        {
            string shown = new string(
                System.Linq.Enumerable.ToArray(
                    System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.Take(info.characterInfo, info.characterCount),
                        character => character.character
                    )
                )
            );
            int index = shown.IndexOf(sentence, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), sentence);
            return info.characterInfo[index].origin;
        }

        [UnityTest]
        public IEnumerator FirstLine_IsShownAlone()
        {
            yield return Show(Conversation.Take(1).ToArray());

            TMP_Text lines = Field<TMP_Text>("linesText");
            float viewport = ((RectTransform)Field<ScrollRect>("historyScroll").transform)
                .rect
                .height;

            Assert.That(lines.preferredHeight, Is.LessThanOrEqualTo(viewport + .5f));
        }

        private IEnumerator Show(DialogueLine[] shown)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            var scaler = panelObject.GetComponentInChildren<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Screen.width / CanvasWidth;
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(new Content(shown));
            yield return null;
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private class Content : IDialogueContent
        {
            private readonly DialogueLine[] lines;

            public Content(DialogueLine[] lines)
            {
                this.lines = lines;
            }

            public string UserTranslation { get; set; }
            public string NpcName => "Jake";
            public IReadOnlyList<DialogueLine> Lines => lines;
            public DialogueLine LastNpcLine => lines.LastOrDefault(line => line.IsNpc);
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
            public bool SpeaksNpcLines => false;
            public bool IsNpcSpeechReady => false;
        }
    }
}
#endif
