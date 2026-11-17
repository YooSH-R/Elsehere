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
    /// 대사 칸은 줄바꿈과 뜻을 포함해 최대 다섯 줄만 보이고, 지난 내용은 스크롤해 다시 본다.
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
        public IEnumerator LongConversation_ShowsAtMostFiveRenderedLinesAndKeepsHistory()
        {
            yield return Show(Conversation.Concat(Conversation).ToArray());

            TMP_Text lines = Field<TMP_Text>("linesText");
            var scroll = Field<ScrollRect>("historyScroll");
            float viewport = ((RectTransform)scroll.transform).rect.height;
            AssertFiveLineLimit(lines, scroll);
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
            scroll.verticalNormalizedPosition = 1;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f));
            Assert.That(lines.text, Does.Contain(Conversation[0].English));
        }

        [UnityTest]
        public IEnumerator OneLongUtterance_DoesNotGrowPastFiveRenderedLines()
        {
            string longSentence = string.Join(
                " ",
                Enumerable.Repeat("Could I have a cheeseburger without onions and some fries?", 18)
            );
            yield return Show(new[] { DialogueLine.FromNpc(longSentence, null) });

            TMP_Text lines = Field<TMP_Text>("linesText");
            var scroll = Field<ScrollRect>("historyScroll");
            Assert.That(lines.textInfo.lineCount, Is.GreaterThan(5));
            AssertFiveLineLimit(lines, scroll);
            Assert.That(lines.text, Does.Contain(longSentence), "원문을 잘라 버리지 않습니다.");
        }

        [UnityTest]
        public IEnumerator MeaningAndNarrowWidth_StillUseFiveRenderedLines()
        {
            yield return Show(Conversation, canvasWidth: 900);
            Field<Button>("meaningButton").onClick.Invoke();
            yield return null;
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
            yield return null;

            TMP_Text lines = Field<TMP_Text>("linesText");
            var scroll = Field<ScrollRect>("historyScroll");
            Assert.That(lines.text, Does.Contain(Conversation.Last().Korean));
            AssertFiveLineLimit(lines, scroll);
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

        private static void AssertFiveLineLimit(TMP_Text lines, ScrollRect scroll)
        {
            lines.ForceMeshUpdate();
            TMP_TextInfo info = lines.textInfo;
            int first = Mathf.Max(0, info.lineCount - 5);
            float fiveLines =
                info.lineInfo[first].ascender - info.lineInfo[info.lineCount - 1].descender;
            Assert.That(
                scroll.viewport.rect.height,
                Is.LessThanOrEqualTo(fiveLines + .5f),
                "발화 개수가 아니라 실제로 줄바꿈된 다섯 줄을 상한으로 삼습니다."
            );
            int visible = 0;
            for (int i = 0; i < info.lineCount; i++)
            {
                TMP_LineInfo line = info.lineInfo[i];
                float top = scroll
                    .viewport.InverseTransformPoint(
                        lines.transform.TransformPoint(new Vector3(0, line.ascender))
                    )
                    .y;
                float bottom = scroll
                    .viewport.InverseTransformPoint(
                        lines.transform.TransformPoint(new Vector3(0, line.descender))
                    )
                    .y;
                if (
                    top > scroll.viewport.rect.yMin + .5f
                    && bottom < scroll.viewport.rect.yMax - .5f
                )
                    visible++;
            }
            Assert.That(visible, Is.InRange(1, 5), "실제 화면에서도 다섯 줄을 넘지 않습니다.");
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f));
        }

        private IEnumerator Show(DialogueLine[] shown, float canvasWidth = CanvasWidth)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            var scaler = panelObject.GetComponentInChildren<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Screen.width / canvasWidth;
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
