#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace A305.Dialogue.PlayModeTests
{
    /// <summary>
    /// 지난 대사는 대사 칸을 위로 스크롤해 다시 본다. 스크롤할 수 있다는 걸 알 수 있게 스크롤바를 보이고,
    /// 휴대폰에서는 손가락으로 끌어 스크롤한다. 서버 없이 대화 내용을 흉내 내고 실제 터치 장치로 끈다.
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
        private InputSettings savedSettings;
        private Touchscreen touchscreen;

        [TearDown]
        public void DestroyPanel()
        {
            if (touchscreen != null)
                InputSystem.RemoveDevice(touchscreen);
            if (savedSettings != null)
                InputSystem.settings = savedSettings;
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator LongConversation_ShowsAScrollbar()
        {
            yield return Show(Conversation);

            Scrollbar bar = Field<ScrollRect>("historyScroll").verticalScrollbar;

            Assert.That(bar, Is.Not.Null, "대사 칸에 세로 스크롤바가 있습니다.");
            Assert.That(bar.gameObject.activeInHierarchy, Is.True, "지난 대사가 있으면 보입니다.");
            Assert.That(
                bar.handleRect.anchorMax.y,
                Is.LessThan(.5f),
                "최근 대사를 보는 중이라 손잡이가 아래에 있습니다."
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

        [UnityTest]
        public IEnumerator FingerDrag_ScrollsBackToEarlierLines()
        {
            yield return Show(Conversation);
            var scroll = Field<ScrollRect>("historyScroll");
            var area = (RectTransform)scroll.transform;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(
                null,
                area.TransformPoint(area.rect.center)
            );

            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return Touch(TouchPhase.Began, start);
            // 손가락을 아래로 끌면 대사가 따라 내려가 위의 지난 대사가 보인다.
            for (int step = 1; step <= 8; step++)
                yield return Touch(TouchPhase.Moved, start + Vector2.down * 12f * step);
            yield return Touch(TouchPhase.Ended, start + Vector2.down * 96f);

            Assert.That(scroll.verticalNormalizedPosition, Is.GreaterThan(.05f));
        }

        private IEnumerator Touch(TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(
                touchscreen,
                new TouchState
                {
                    touchId = 1,
                    phase = phase,
                    position = position,
                }
            );
            yield return null;
        }

        private IEnumerator Show(DialogueLine[] shown)
        {
            // 배치 실행처럼 창에 포커스가 없어도 터치 장치 입력을 받는다.
            savedSettings = InputSystem.settings;
            var settings = Object.Instantiate(savedSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
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
        }
    }
}
#endif
