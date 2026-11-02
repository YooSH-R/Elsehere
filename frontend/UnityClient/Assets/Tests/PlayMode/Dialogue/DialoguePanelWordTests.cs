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
    /// 실제 대화창 프리팹에서 [뜻]을 켠 대사의 풀이 단어를 눌러 대사 칸 아래 줄에 뜻을 보고, [단어장에 저장]을 누를 수 있는지 확인한다.
    /// </summary>
    public class DialoguePanelWordTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const long TurnId = 105;

        private GameObject panelObject;
        private GameObject eventSystemObject;
        private DialoguePanelView view;

        [UnitySetUp]
        public IEnumerator ShowServerConversation()
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            panelObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)
            );
            view = panelObject.GetComponentInChildren<DialoguePanelView>(true);
            view.Show(new WordContent());
            yield return null;
        }

        [TearDown]
        public void DestroyPanel()
        {
            Object.Destroy(panelObject);
            Object.Destroy(eventSystemObject);
        }

        [UnityTest]
        public IEnumerator GlossaryWord_ClickedInLines_ShowsItsMeaningBelowTheLines()
        {
            yield return TurnOnMeaning();

            yield return ClickWordInLines("latte");

            Assert.That(WordRow().gameObject.activeInHierarchy, Is.True);
            Assert.That(WordRowText().text, Is.EqualTo("<b>latte</b>  라떼"));
            Assert.That(WordSaveButton().gameObject.activeInHierarchy, Is.True);
            Assert.That(WordSaveButton().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator WordSaveButton_Clicked_RequestsThatWordAndShowsTheResult()
        {
            var requested = new List<GlossaryWord>();
            view.WordSaveRequested += requested.Add;
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            ClickCenterOf(WordSaveButton());
            yield return null;

            Assert.That(requested, Has.Count.EqualTo(1));
            Assert.That(requested[0].TurnId, Is.EqualTo(TurnId));
            Assert.That(requested[0].Text, Is.EqualTo("latte"));
            Assert.That(WordSaveButton().interactable, Is.False);

            view.ShowWordSaveResult(requested[0], true, "단어장에 저장했어요.");
            yield return null;

            Assert.That(WordRowText().text, Does.EndWith("단어장에 저장했어요."));
            Assert.That(WordSaveButton().gameObject.activeInHierarchy, Is.False);
        }

        // 단어 줄은 대사 칸 바로 아래, 상태 줄 위에 두어 마지막 대사와 안내를 가리지 않는다.
        [UnityTest]
        public IEnumerator WordRow_SitsBetweenLinesAndStatusWithoutOverlapping()
        {
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            Rect row = InPanel(WordRow());
            Rect history = InPanel(Child<RectTransform>("Panel/Conversation/History"));
            Rect status = InPanel(Child<RectTransform>("Panel/Conversation/Status"));

            Assert.That(history.yMin, Is.GreaterThanOrEqualTo(row.yMax));
            Assert.That(row.yMin, Is.GreaterThanOrEqualTo(status.yMax));
        }

        [UnityTest]
        public IEnumerator HidingMeaning_ClosesTheWordRow()
        {
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            yield return TurnOnMeaning();

            Assert.That(WordRow().gameObject.activeInHierarchy, Is.False);
        }

        // 서버는 문장을 저장하지 않으므로 대사 전체를 담던 [담기]는 두지 않는다.
        [UnityTest]
        public IEnumerator ToolRow_HasNoSentenceSaveButton()
        {
            Assert.That(
                panelObject.transform.Find("Panel/Conversation/ToolRow/SaveButton"),
                Is.Null
            );
            yield break;
        }

        private IEnumerator TurnOnMeaning()
        {
            ClickCenterOf(Child<Button>("Panel/Conversation/ToolRow/MeaningButton"));
            yield return null;
        }

        // 대사 칸에서 그 단어 글자의 가운데를 실제 화면 좌표로 눌러, 다른 UI에 가려지지 않고 링크가 클릭을 받는지 본다.
        private IEnumerator ClickWordInLines(string word)
        {
            var lines = Child<TMP_Text>("Panel/Conversation/History/Viewport/Lines");
            lines.ForceMeshUpdate();
            TMP_TextInfo info = lines.textInfo;
            int linkIndex = -1;
            for (int i = 0; i < info.linkCount; i++)
            {
                if (info.linkInfo[i].GetLinkText() == word)
                    linkIndex = i;
            }

            Assert.That(linkIndex, Is.GreaterThanOrEqualTo(0), $"대사에 {word} 링크가 없습니다.");
            TMP_CharacterInfo first = info.characterInfo[
                info.linkInfo[linkIndex].linkTextfirstCharacterIndex
            ];
            Vector3 middle = (first.bottomLeft + first.topRight) / 2;
            Click(
                RectTransformUtility.WorldToScreenPoint(
                    null,
                    lines.transform.TransformPoint(middle)
                ),
                lines.gameObject
            );
            yield return null;
        }

        private static void ClickCenterOf(Button button)
        {
            var rect = (RectTransform)button.transform;
            Click(
                RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center)
                ),
                button.gameObject
            );
        }

        private static void Click(Vector2 position, GameObject expected)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "누른 위치에 클릭을 받는 UI가 없습니다.");

            GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                hits[0].gameObject
            );
            Assert.That(clicked, Is.EqualTo(expected));
            pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
        }

        private RectTransform WordRow() => Child<RectTransform>("Panel/Conversation/WordRow");

        private TMP_Text WordRowText() => Child<TMP_Text>("Panel/Conversation/WordRow/Text");

        private Button WordSaveButton() =>
            Child<Button>("Panel/Conversation/WordRow/WordSaveButton");

        private T Child<T>(string path)
            where T : Component
        {
            Transform child = panelObject.transform.Find(path);
            Assert.That(child, Is.Not.Null, $"{path}를 찾지 못했습니다.");
            return child.GetComponent<T>();
        }

        private Rect InPanel(RectTransform rect)
        {
            var panel = (RectTransform)panelObject.transform.Find("Panel");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = panel.InverseTransformPoint(corners[0]);
            Vector3 max = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>[뜻 보기]로 뜻과 풀이를 받아 둔 Ben의 대사 하나.</summary>
        private class WordContent : IDialogueContent
        {
            private static readonly DialogueLine Opening = DialogueLine.FromNpc(
                "Would you like an iced latte to go?",
                "아이스 라떼 포장해 드릴까요?",
                new[]
                {
                    new GlossaryWord(TurnId, "latte", "라떼"),
                    new GlossaryWord(TurnId, "to go", "포장"),
                }
            );

            public string NpcName => "Ben";
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

            public IReadOnlyList<GlossaryData> LastNpcGlossary =>
                new[]
                {
                    new GlossaryData { Text = "latte", MeaningKo = "라떼" },
                    new GlossaryData { Text = "to go", MeaningKo = "포장" },
                };

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
