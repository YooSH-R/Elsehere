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
    /// <summary>서버 없이 실제 대화창 프리팹에서 [힌트]를 메뉴를 거치지 않고 바로 누를 수 있는지 확인한다.</summary>
    public class DialoguePanelHintTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string HintSentence = "Can I get an iced latte, please?";

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;
        private HintContent content;

        [UnitySetUp]
        public IEnumerator ShowServerConversation()
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            content = new HintContent();
            view.Show(content);
            yield return null;
        }

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator HintButton_ClickedOnScreen_RequestsHintWithoutMoreMenu()
        {
            int requests = 0;
            view.HintRequested += () => requests++;
            Button hint = FindButton("HintButton");

            Assert.That(hint.transform.parent.name, Is.EqualTo("ToolRow"));
            Assert.That(hint.gameObject.activeInHierarchy, Is.True);
            Assert.That(hint.interactable, Is.True);
            Assert.That(FindChild("MoreButton"), Is.Null);

            ClickCenterOf(hint);
            yield return null;

            Assert.That(requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HintText_AfterServerAnswers_ShowsInSentenceCard()
        {
            content.HintText = HintSentence;
            view.Refresh();
            yield return null;

            var body = FindChild("HelpPanel/Viewport/Content/HintCard/Body")
                .GetComponent<TMP_Text>();
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(body.text, Is.EqualTo(HintSentence));
            Assert.That(FindButton("HintButton").interactable, Is.False);
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

        private Transform FindChild(string path)
        {
            if (path.Contains("/"))
                return panelObject.transform.Find(path);

            foreach (Transform child in panelObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == path)
                    return child;
            }

            return null;
        }

        /// <summary>NPC가 첫 대사를 마치고 사용자가 답할 차례인 서버 대화.</summary>
        private class HintContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Hi, welcome in. What would you like?",
                null
            );

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
            public string HintText { get; set; }
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => false;
            public bool SpeaksNpcLines => false;
            public bool IsNpcSpeechReady => false;
        }
    }
}
#endif
