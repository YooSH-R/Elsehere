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
    /// <summary>
    /// NPC 대답과 피드백을 기다리는 동안 버튼 줄 옆 작은 글씨 대신 눈에 띄는 로딩을 보이는지 실제 대화창으로 확인한다.
    /// 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelWaitTests
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
        public IEnumerator WaitingForTheNpcReply_ShowsLoadingInPlaceOfTheLines()
        {
            yield return Show(
                new Content(DialogueWait.NpcReply, "Jake이(가) 대답을 생각하고 있어요...")
            );

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.True);
            Assert.That(
                Field<TMP_Text>("waitText").text,
                Does.Contain("Jake의 대답을 기다리고 있어요")
            );
            Assert.That(Field<TMP_Text>("statusText").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("feedbackWait").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator WaitingForTheOpeningLine_ShowsLoadingInPlaceOfTheLines()
        {
            yield return Show(new Content(DialogueWait.Opening, "대화를 시작하고 있어요..."));

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.True);
            Assert.That(Field<TMP_Text>("waitText").text, Does.Contain("대화를 준비하고 있어요"));
        }

        // 피드백은 오래 걸리므로 화면 전체를 덮는 가운데 로딩 창으로 알린다.
        [UnityTest]
        public IEnumerator WaitingForFeedback_CoversTheScreenWithALoadingWindow()
        {
            yield return Show(
                new Content(
                    DialogueWait.Feedback,
                    "대화를 마치고 피드백을 만들고 있어요. 잠시만 기다려 주세요."
                )
            );

            GameObject window = Field<GameObject>("feedbackWait");
            Assert.That(window.activeInHierarchy, Is.True);
            Assert.That(Texts(window), Does.Contain("피드백을 만들고 있어요"));
            var screen = (RectTransform)window.transform.parent;
            Assert.That(((RectTransform)window.transform).rect.size, Is.EqualTo(screen.rect.size));
            Assert.That(Field<TMP_Text>("statusText").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator NotWaiting_HidesBothLoadingViews()
        {
            yield return Show(new Content(DialogueWait.None, null));

            Assert.That(Field<GameObject>("waitPanel").activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>("feedbackWait").activeInHierarchy, Is.False);
        }

        // 멈춘 화면처럼 보이지 않게 로딩 고리가 계속 돈다.
        [UnityTest]
        public IEnumerator LoadingRing_KeepsTurning()
        {
            yield return Show(new Content(DialogueWait.NpcReply, null));
            Transform ring = Field<GameObject>("waitPanel")
                .GetComponentInChildren<LoadingSpinner>()
                .transform;
            float before = ring.eulerAngles.z;

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(Mathf.DeltaAngle(before, ring.eulerAngles.z), Is.Not.EqualTo(0f));
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
            Canvas.ForceUpdateCanvases();
        }

        private static string Texts(GameObject root)
        {
            var texts = new List<string>();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>())
                texts.Add(text.text);
            return string.Join("\n", texts);
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private class Content : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Welcome to Maple House. How can I help you?",
                null
            );

            public Content(DialogueWait waiting, string status)
            {
                Waiting = waiting;
                StatusMessage = status;
            }

            public DialogueWait Waiting { get; }
            public string NpcName => "Jake";
            public IReadOnlyList<DialogueLine> Lines => new[] { Opening };
            public DialogueLine LastNpcLine => Opening;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;
            public bool IsBusy => Waiting != DialogueWait.None;
            public string StatusMessage { get; }
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => null;
            public bool IsSuggestingWords => false;
            public bool CanSend => !IsBusy;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public bool CanReplaySpeech => false;
        }
    }
}
#endif
