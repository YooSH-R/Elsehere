#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Dialogue.PlayModeTests
{
    /// <summary>
    /// 지난 대사는 대사 칸을 위로 스크롤해 다시 본다. 스크롤할 수 있다는 걸 알 수 있게 스크롤바를 보인다. 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelScrollTests
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
        public IEnumerator LongConversation_ShowsAScrollbar()
        {
            yield return Show(Conversation);

            ScrollRect historyScroll = Field<ScrollRect>("historyScroll");
            Scrollbar bar = historyScroll.verticalScrollbar;

            Assert.That(bar, Is.Not.Null, "대사 칸에 세로 스크롤바가 있습니다.");
            Assert.That(bar.gameObject.activeInHierarchy, Is.True, "지난 대사가 있으면 보입니다.");
            Assert.That(
                historyScroll.verticalNormalizedPosition,
                Is.EqualTo(0f).Within(.01f),
                "가장 최근 대사를 볼 수 있도록 스크롤이 맨 아래에 있습니다."
            );
        }

        [UnityTest]
        public IEnumerator FirstLine_HidesTheScrollbar()
        {
            yield return Show(Conversation.Take(1).ToArray());

            Scrollbar bar = Field<ScrollRect>("historyScroll").verticalScrollbar;

            Assert.That(bar, Is.Not.Null);
            Assert.That(
                bar.gameObject.activeInHierarchy,
                Is.False,
                "스크롤할 대사가 없으면 숨깁니다."
            );
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
