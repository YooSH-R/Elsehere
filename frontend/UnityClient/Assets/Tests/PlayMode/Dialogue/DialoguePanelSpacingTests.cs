#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    /// 대사·뜻·상태 문구가 버튼 줄과 겹치거나 잘리지 않는지 실제 대화창으로 확인한다.
    /// 서버 없이 대화 내용을 흉내 낸다.
    /// </summary>
    public class DialoguePanelSpacingTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const float CanvasWidth = 1600f;

        // NPC가 한 번에 하는 긴 대답(두세 문장)과 그 뜻
        private const string LongLine =
            "Of course. I can keep your large suitcase here until your room is ready this afternoon. Here is your claim tag, so please keep it with you.";
        private const string LongMeaning =
            "물론이죠. 오후에 방이 준비될 때까지 큰 캐리어를 여기 맡아 드릴게요. 보관표 여기 있으니 잘 가지고 계세요.";

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        // 대사 칸 아래 끝이 도구 줄 안까지 내려와 마지막 줄이 버튼에 걸려 잘려 보였다.
        [UnityTest]
        public IEnumerator ConversationLines_StayAboveTheToolRowWithRoomToBreathe()
        {
            yield return Show(new Content(null));

            Rect lines = InPanel((RectTransform)Field<ScrollRect>("historyScroll").transform);
            Rect tools = InPanel(ToolRow());
            Assert.That(lines.yMin - tools.yMax, Is.GreaterThanOrEqualTo(12f));
        }

        // 긴 NPC 대사와 뜻을 펼쳐도 스크롤하지 않고 한 번에 다 보여야 한다.
        [UnityTest]
        public IEnumerator LongLineWithItsMeaning_IsFullyVisible()
        {
            yield return Show(new Content(null));

            Field<Button>("meaningButton").onClick.Invoke();
            yield return null;

            TMP_Text lines = Field<TMP_Text>("linesText");
            Rect viewport = InPanel((RectTransform)Field<ScrollRect>("historyScroll").transform);
            Assert.That(lines.text, Does.Contain(LongMeaning));
            Assert.That(lines.preferredHeight, Is.LessThanOrEqualTo(viewport.height + 0.5f));
        }

        // [다시 듣기]까지 도구 버튼이 모두 보일 때 상태 줄이 [담기] 위로 겹쳐 안내가 가려졌다.
        [UnityTest]
        public IEnumerator StatusLine_DoesNotOverlapAnyToolButton(
            [Values] RecordingEndReason endReason
        )
        {
            yield return Show(new Content(VoiceInputNotice.Transcribed(endReason)));

            TMP_Text status = Field<TMP_Text>("statusText");
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Rect statusRect = InPanel(status.rectTransform);
            foreach (RectTransform tool in ToolRow())
            {
                if (!tool.gameObject.activeInHierarchy)
                    continue;
                Assert.That(
                    statusRect.Overlaps(InPanel(tool)),
                    Is.False,
                    $"상태 줄이 {tool.name}와 겹칩니다."
                );
            }
            status.ForceMeshUpdate();
            Assert.That(status.isTextOverflowing, Is.False, "안내가 한 줄에 들어가야 합니다.");
        }

        private IEnumerator Show(IDialogueContent content)
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            var scaler = panelObject.GetComponentInChildren<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Screen.width / CanvasWidth;
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(content);
            yield return null;
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform ToolRow() =>
            (RectTransform)Field<Button>("saveButton").transform.parent;

        private Rect InPanel(RectTransform rect)
        {
            var panel = (RectTransform)Field<GameObject>("panel").transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = panel.InverseTransformPoint(corners[0]);
            Vector3 max = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;

        private class Content : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                LongLine,
                LongMeaning
            );

            public Content(string status)
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
            public bool IsBusy => false;
            public string StatusMessage { get; }
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => null;
            public bool CanSend => true;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;

            // NPC 음성을 다시 들을 수 있으면 도구 버튼이 가장 많다.
            public bool CanReplaySpeech => true;
        }
    }
}
#endif
