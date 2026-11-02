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
    /// 음성 처리에 동의하지 않아 [말하기]가 없는 대화에서, 동의를 켜는 곳(오른쪽 위 메뉴 › 내 정보 › 선택 동의)을
    /// 실제 대화창의 상태 줄에 알려 주는지 확인한다. 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelVoiceConsentTests
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
        public IEnumerator WithoutVoiceConsent_ShowsWhereToTurnItOnInsteadOfTalkButton()
        {
            yield return Show(new FreeTalkContent(canTalk: false));

            TMP_Text status = Status();
            Assert.That(
                status.gameObject.activeInHierarchy,
                Is.True,
                "동의를 켜는 곳을 보여야 합니다."
            );
            Assert.That(
                status.text,
                Does.Contain("내 정보").And.Contain("선택 동의").And.Contain("음성 처리")
            );
            // 긴 안내도 대화창 밖으로 넘치지 않고 한 줄에 들어간다.
            status.ForceMeshUpdate();
            Assert.That(status.isTextOverflowing, Is.False);
            Assert.That(RightEdge(status.rectTransform), Is.LessThanOrEqualTo(RightEdge(Panel())));
            Assert.That(TalkButton().gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator WithVoiceConsent_ShowsTalkButtonWithoutTheConsentGuide()
        {
            yield return Show(new FreeTalkContent(canTalk: true));

            Assert.That(Status().gameObject.activeInHierarchy, Is.False);
            Assert.That(TalkButton().gameObject.activeInHierarchy, Is.True);
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

        // 입력칸 위의 상태 줄. 녹음 중·오류 안내와 같은 자리다.
        private TMP_Text Status() =>
            (TMP_Text)new SerializedObject(view).FindProperty("statusText").objectReferenceValue;

        private Button TalkButton() =>
            panelObject
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "TalkButton");

        private class FreeTalkContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Welcome to Maple House. How can I help you?",
                null
            );

            public FreeTalkContent(bool canTalk)
            {
                CanTalk = canTalk;
            }

            public string NpcName => "Jake";
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
            public bool CanSend => true;
            public bool CanTalk { get; }
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
