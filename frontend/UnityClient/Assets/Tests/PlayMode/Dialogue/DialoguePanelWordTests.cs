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
    /// 실제 대화창 프리팹에서 [뜻]을 켠 대사의 풀이 단어를 눌러 단어 근처 호버 카드에서 뜻을 보고, [단어장에 저장]을 누를 수 있는지 확인한다.
    /// 숙어 안의 단어를 누르면 단어와 숙어가 카드에 함께 쌓여 스크롤로 볼 수 있는지도 본다.
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
        public IEnumerator GlossaryWord_ClickedInLines_ShowsAHoverCardWithItsMeaning()
        {
            yield return TurnOnMeaning();

            yield return ClickWordInLines("latte");

            Assert.That(WordCard().gameObject.activeInHierarchy, Is.True);
            Assert.That(CardText("Title").text, Is.EqualTo("latte"));
            Assert.That(CardText("Meaning").text, Is.EqualTo("라떼"));
            Assert.That(WordSaveButton().gameObject.activeInHierarchy, Is.True);
            Assert.That(WordSaveButton().interactable, Is.True);
            Assert.That(
                Child<Scrollbar>(
                    "Panel/Conversation/WordCard/Scrollbar"
                ).gameObject.activeInHierarchy,
                Is.False,
                "풀이가 하나면 스크롤 막대를 두지 않습니다."
            );
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

            Assert.That(CardText("Note").text, Is.EqualTo("단어장에 저장했어요."));
            Assert.That(WordSaveButton().gameObject.activeInHierarchy, Is.False);
        }

        // 호버 카드는 누른 단어 바로 위나 아래에 붙고, 대화창 위쪽(NPC 얼굴)으로 넘어가지 않는다.
        [UnityTest]
        public IEnumerator WordCard_StaysInsideThePanelRightNextToTheWord()
        {
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            Rect card = InPanel(WordCard());
            Rect panel = ((RectTransform)panelObject.transform.Find("Panel")).rect;
            Rect word = WordInPanel("latte");

            Assert.That(card.yMax, Is.LessThanOrEqualTo(panel.yMax + 0.5f));
            Assert.That(card.yMin, Is.GreaterThanOrEqualTo(panel.yMin - 0.5f));
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(panel.xMin - 0.5f));
            Assert.That(card.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.5f));
            bool above = card.yMin >= word.yMax && card.yMin - word.yMax <= 12;
            bool below = card.yMax <= word.yMin && word.yMin - card.yMax <= 12;
            Assert.That(
                above || below,
                Is.True,
                $"카드 {card}가 단어 {word} 바로 위나 아래에 있지 않습니다."
            );
        }

        [UnityTest]
        public IEnumerator CloseButton_ClosesTheWordCard()
        {
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            ClickCenterOf(Child<Button>("Panel/Conversation/WordCard/CloseButton"));
            yield return null;

            Assert.That(WordCard().gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator HidingMeaning_ClosesTheWordCard()
        {
            yield return TurnOnMeaning();
            yield return ClickWordInLines("latte");

            yield return TurnOnMeaning();

            Assert.That(WordCard().gameObject.activeInHierarchy, Is.False);
        }

        // 카드를 띄워도 대사 칸 스크롤은 그대로 두어 가장 최근 대사와 그 아래 단어 풀이가 계속 보인다.
        [UnityTest]
        public IEnumerator OpeningWordCard_KeepsTheLatestLinesInView()
        {
            var earlier = new List<DialogueLine>();
            for (int i = 0; i < 6; i++)
            {
                earlier.Add(DialogueLine.FromNpc($"Earlier line number {i}.", "앞선 대사"));
                earlier.Add(DialogueLine.FromUser($"My answer number {i}."));
            }
            view.Show(new WordContent(earlier));
            yield return null;
            yield return TurnOnMeaning();

            yield return ClickWordInLines("latte");

            Assert.That(WordCard().gameObject.activeInHierarchy, Is.True);
            Assert.That(
                Child<ScrollRect>("Panel/Conversation/History").verticalNormalizedPosition,
                Is.LessThan(0.01f)
            );
        }

        // 고른 단어를 대사 안에서도 칠해 카드의 뜻이 어느 단어의 것인지 바로 보이게 한다.
        [UnityTest]
        public IEnumerator ClickedWord_IsMarkedInTheLine()
        {
            yield return TurnOnMeaning();

            yield return ClickWordInLines("to go");

            Assert.That(
                Child<TMP_Text>("Panel/Conversation/History/Viewport/Lines").text,
                Does.Contain("<mark=#F982564D><u>to go</u></mark>")
            );
            Assert.That(WordSaveButton().gameObject.activeInHierarchy, Is.True);
        }

        // 숙어(grab a seat) 안의 단어(grab)를 누르면 단어와 숙어를 카드에 함께 쌓고, 카드보다 길면 아래로 스크롤해 숙어도 저장한다.
        [UnityTest]
        public IEnumerator WordInsideAnIdiom_ShowsTheWordThenTheIdiom_AndTheIdiomSavesAfterScrolling()
        {
            var requested = new List<GlossaryWord>();
            view.WordSaveRequested += requested.Add;
            view.Show(new WordContent(latest: SeatLine));
            yield return null;
            yield return TurnOnMeaning();

            yield return ClickWordInLines("grab");

            Assert.That(EntryTitles(), Is.EqualTo(new[] { "grab", "grab a seat" }));
            var entries = Child<ScrollRect>("Panel/Conversation/WordCard/Entries");
            Assert.That(
                entries.content.rect.height,
                Is.GreaterThan(entries.viewport.rect.height),
                "풀이가 카드보다 길어 스크롤합니다."
            );
            Assert.That(
                entries.verticalScrollbar.gameObject.activeInHierarchy,
                Is.True,
                "아래에 풀이가 더 있다는 스크롤 막대가 보입니다."
            );

            entries.verticalNormalizedPosition = 0;
            yield return null;
            ClickCenterOf(EntryPart<Button>(1, "SaveButton"));
            yield return null;

            Assert.That(requested, Has.Count.EqualTo(1));
            Assert.That(requested[0].Text, Is.EqualTo("grab a seat"));
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

        private RectTransform WordCard() => Child<RectTransform>("Panel/Conversation/WordCard");

        // 카드의 첫 풀이 줄
        private TMP_Text CardText(string name) => EntryPart<TMP_Text>(0, name);

        private Button WordSaveButton() => EntryPart<Button>(0, "SaveButton");

        // 카드 풀이 목록의 index번째 보이는 줄에서 이름이 name인 부분
        private T EntryPart<T>(int index, string name)
            where T : Component
        {
            var content = Child<RectTransform>("Panel/Conversation/WordCard/Entries/Content");
            var shown = new List<Transform>();
            foreach (Transform entry in content)
            {
                if (entry.gameObject.activeSelf)
                    shown.Add(entry);
            }

            Assert.That(
                shown.Count,
                Is.GreaterThan(index),
                $"카드에 {index + 1}번째 풀이가 없습니다."
            );
            return shown[index].Find(name).GetComponent<T>();
        }

        private List<string> EntryTitles()
        {
            var titles = new List<string>();
            foreach (
                Transform entry in Child<RectTransform>(
                    "Panel/Conversation/WordCard/Entries/Content"
                )
            )
            {
                if (entry.gameObject.activeSelf)
                    titles.Add(entry.Find("Title").GetComponent<TMP_Text>().text);
            }

            return titles;
        }

        // 대사 칸에서 그 단어 글자들이 차지한 영역을 대화창 기준 좌표로 구한다.
        private Rect WordInPanel(string word)
        {
            var lines = Child<TMP_Text>("Panel/Conversation/History/Viewport/Lines");
            lines.ForceMeshUpdate();
            TMP_TextInfo info = lines.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (link.GetLinkText() != word)
                    continue;
                TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
                TMP_CharacterInfo last = info.characterInfo[
                    link.linkTextfirstCharacterIndex + link.linkTextLength - 1
                ];
                var panel = (RectTransform)panelObject.transform.Find("Panel");
                Vector3 min = panel.InverseTransformPoint(
                    lines.transform.TransformPoint(first.bottomLeft)
                );
                Vector3 max = panel.InverseTransformPoint(
                    lines.transform.TransformPoint(last.topRight)
                );
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            Assert.Fail($"대사에 {word} 링크가 없습니다.");
            return default;
        }

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

        // 숙어와 그 안의 단어가 함께 풀이된 대사
        private static readonly DialogueLine SeatLine = DialogueLine.FromNpc(
            "Please grab a seat by the window.",
            "창가 자리에 앉으세요.",
            new[]
            {
                new GlossaryWord(TurnId, "grab a seat", "자리에 앉다"),
                new GlossaryWord(TurnId, "grab", "잡다"),
                new GlossaryWord(TurnId, "seat", "자리"),
                new GlossaryWord(TurnId, "window", "창문"),
            }
        );

        /// <summary>[뜻 보기]로 뜻과 풀이를 받아 둔 Ben의 대사. 앞선 대사를 주면 그 뒤에 둔다.</summary>
        private class WordContent : IDialogueContent
        {
            private static readonly DialogueLine LatteLine = DialogueLine.FromNpc(
                "Would you like an iced latte to go?",
                "아이스 라떼 포장해 드릴까요?",
                new[]
                {
                    new GlossaryWord(TurnId, "latte", "라떼"),
                    new GlossaryWord(TurnId, "to go", "포장"),
                }
            );

            private readonly List<DialogueLine> lines = new List<DialogueLine>();
            private readonly DialogueLine latest;

            public WordContent(IEnumerable<DialogueLine> earlier = null, DialogueLine latest = null)
            {
                this.latest = latest ?? LatteLine;
                if (earlier != null)
                    lines.AddRange(earlier);
                lines.Add(this.latest);
            }

            public string UserTranslation { get; set; }
            public string NpcName => "Ben";
            public IReadOnlyList<DialogueLine> Lines => lines;
            public DialogueLine LastNpcLine => latest;
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
                latest
                    .Glossary.Select(word => new GlossaryData
                    {
                        Text = word.Text,
                        MeaningKo = word.MeaningKo,
                    })
                    .ToList();

            public bool HasHint => true;
            public string HintText => null;
            public bool UsesVoice => true;
            public ConversationStanding Standing { get; set; }
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => false;
            public bool SpeaksNpcLines => false;
            public bool IsNpcSpeechReady => false;
        }
    }
}
#endif
