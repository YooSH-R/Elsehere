using System;
using System.Collections.Generic;
using System.Linq;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 창의 [장소 연습] 쪽(시안 #place). 왼쪽은 장소 목록(아이콘·이름·앞 두 상황·끝낸 수, 고른 줄은 코랄 테두리)이고,
    /// 장소를 고르면 오른쪽 판만 그 장소(큰 아이콘·이름·직원 칩)와 상황 세 줄로 바뀐다. 끝낸 상황은 초록 체크와 '완료' 칩·[다시 하기],
    /// 아직이면 번호와 [연습하기]를 보인다. 자리는 시안 px × 1.2이고 버튼 연결은 PracticeBoardBuilder가 넣는다.
    /// </summary>
    public sealed class PracticeBoardView : MonoBehaviour
    {
        private const float Px = 1.2f;

        // 고른 줄은 테두리가 1.5 두꺼워 안의 아이콘·글자도 그만큼 안으로 들어간다.
        private const float SelectedInset = 1.5f;

        // 장소 이름과 직원 칩 사이: Jua 44의 띄어쓰기(0.3em) + 칩 왼쪽 여백 12
        private const float ChipGap = 44 * .3f + 12;
        private const float ChipPadding = 14;

        // 상황 줄 글자 폭: 오른쪽 버튼 묶음 왼쪽 20 앞까지(완료 칩과 [다시 하기]가 있으면 더 좁다)
        private const float ScenarioTextWidth = 649.14f;
        private const float DoneScenarioTextWidth = 571.23f;

        // 시안의 .45 잉크를 밝은 줄 바탕에 얹은 색
        private static readonly Color CountInk = new Color32(160, 154, 147, 255);
        private static readonly Color AllDoneInk = new Color32(61, 154, 91, 255);

        [SerializeField]
        private ScrollRect placeList;

        [SerializeField]
        [Tooltip("장소 줄 견본. 꺼 두고 장소마다 복사해 쓴다.")]
        private Button rowTemplate;

        [SerializeField]
        [Tooltip("목록 아래를 흐리게 덮는 그림. 맨 아래까지 넘기면 숨긴다.")]
        private GameObject fade;

        [SerializeField]
        private Sprite rowSprite;

        [SerializeField]
        private Sprite selectedRowSprite;

        [SerializeField]
        [Tooltip(
            "장소 코드별 아이콘(목록의 작은 것, 오른쪽의 큰 것). 없는 장소는 첫 아이콘을 쓴다."
        )]
        private PracticePlaceIcon[] icons;

        [SerializeField]
        [Tooltip("오른쪽 판 안의 내용. 목록을 받기 전에는 숨긴다.")]
        private GameObject paneContent;

        [SerializeField]
        private Image placeIcon;

        [SerializeField]
        private TMP_Text placeName;

        [SerializeField]
        private RectTransform npcChip;

        [SerializeField]
        private TMP_Text npcChipText;

        [SerializeField]
        private Sprite numberSprite;

        [SerializeField]
        private Sprite doneNumberSprite;

        [SerializeField]
        private PracticeScenarioSlot[] scenarios;

        private readonly PracticeBoard board = new PracticeBoard();
        private readonly List<Button> rows = new List<Button>();

        /// <summary>장소 연습 목록을 받아 보인다. 고르던 장소는 그대로 둔다.</summary>
        public void Show(PracticeBoardState state)
        {
            board.Show(state);
            Refresh();
        }

        public void Select(int index)
        {
            board.Select(index);
            Refresh();
        }

        /// <summary>오른쪽 slot번째 상황을 연습하러 갈 계획. 그 칸에 상황이 없으면 null이다.</summary>
        public PracticeVisitPlan PlanFor(int slot) => board.PlanFor(slot);

        private void Awake() => placeList.onValueChanged.AddListener(_ => ShowFade());

        private void Refresh()
        {
            for (int i = 0; i < board.Places.Count; i++)
                FillRow(Row(i), board.Places[i], i == board.SelectedIndex);
            for (int i = board.Places.Count; i < rows.Count; i++)
                rows[i].gameObject.SetActive(false);
            // 줄을 바꾼 직후 목록 높이로 아래 흐림을 정한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(placeList.content);
            ShowFade();

            PracticePlace place = board.Selected;
            paneContent.SetActive(place != null);
            if (place != null)
                FillPane(place);
        }

        private void FillRow(Button row, PracticePlace place, bool selected)
        {
            row.gameObject.SetActive(true);
            row.transform.Find("Background").GetComponent<Image>().sprite = selected
                ? selectedRowSprite
                : rowSprite;
            var content = (RectTransform)row.transform.Find("Content");
            float inset = selected ? SelectedInset * Px : 0;
            content.offsetMin = new Vector2(inset, 0);
            content.offsetMax = new Vector2(-inset, 0);
            content.Find("Icon").GetComponent<Image>().sprite = IconOf(place.PlaceCode).Small;
            Text(content, "Name").text = place.Name;
            Text(content, "Summary").text = place.Summary;
            TMP_Text count = Text(content, "Count");
            count.text = PracticeBoardText.DoneCount(place);
            count.color = PracticeBoardText.IsAllDone(place) ? AllDoneInk : CountInk;
        }

        private void FillPane(PracticePlace place)
        {
            placeIcon.sprite = IconOf(place.PlaceCode).Large;
            placeName.text = place.Name;
            npcChipText.text = PracticeBoardText.NpcChip(place);
            // 직원 칩은 장소 이름 바로 뒤에 두고, 폭은 글자 + 양옆 14다.
            float nameWidth = placeName.GetPreferredValues(place.Name).x;
            npcChip.anchoredPosition = new Vector2(
                placeName.rectTransform.anchoredPosition.x + nameWidth + ChipGap * Px,
                npcChip.anchoredPosition.y
            );
            npcChip.sizeDelta = new Vector2(
                npcChipText.GetPreferredValues(npcChipText.text).x + ChipPadding * 2 * Px,
                npcChip.sizeDelta.y
            );

            for (int i = 0; i < scenarios.Length; i++)
            {
                bool shown = i < place.Scenarios.Count;
                scenarios[i].Root.gameObject.SetActive(shown);
                if (shown)
                    FillScenario(scenarios[i], place.Scenarios[i]);
            }
        }

        private void FillScenario(PracticeScenarioSlot slot, PracticeScenario scenario)
        {
            slot.Number.sprite = scenario.Done ? doneNumberSprite : numberSprite;
            slot.NumberText.gameObject.SetActive(!scenario.Done);
            slot.NumberText.text = scenario.Number.ToString();
            slot.Title.text = scenario.Title;
            slot.Description.text = scenario.Description;
            float width = scenario.Done ? DoneScenarioTextWidth : ScenarioTextWidth;
            foreach (TMP_Text text in new[] { slot.Title, slot.Description })
                text.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    width * Px
                );
            slot.DoneChip.SetActive(scenario.Done);
            slot.Again.gameObject.SetActive(scenario.Done);
            slot.Start.gameObject.SetActive(!scenario.Done);
        }

        // 목록이 넘겨지고 아래에 더 있을 때만 아래를 흐리게 덮는다.
        private void ShowFade()
        {
            RectTransform content = placeList.content;
            float hidden = content.rect.height - placeList.viewport.rect.height;
            fade.SetActive(hidden > 1 && placeList.verticalNormalizedPosition > .001f);
        }

        // index번째 장소 줄. 처음 쓰는 자리면 견본을 복사해 만든다.
        private Button Row(int index)
        {
            while (rows.Count <= index)
            {
                int rowIndex = rows.Count;
                Button row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.name = rowTemplate.name;
                row.onClick.AddListener(() => Select(rowIndex));
                rows.Add(row);
            }
            return rows[index];
        }

        private PracticePlaceIcon IconOf(string placeCode)
        {
            PracticePlaceIcon icon = icons.FirstOrDefault(item => item.PlaceCode == placeCode);
            return icon.Small != null ? icon : icons[0];
        }

        private static TMP_Text Text(Transform parent, string name) =>
            parent.Find(name).GetComponent<TMP_Text>();
    }

    [Serializable]
    public sealed class PracticeScenarioSlot
    {
        public RectTransform Root;
        public Image Number;
        public TMP_Text NumberText;
        public TMP_Text Title;
        public TMP_Text Description;

        /// <summary>끝낸 상황의 '완료' 칩</summary>
        public GameObject DoneChip;

        /// <summary>[연습하기]</summary>
        public Button Start;

        /// <summary>끝낸 상황의 [다시 하기]</summary>
        public Button Again;
    }

    [Serializable]
    public struct PracticePlaceIcon
    {
        public string PlaceCode;
        public Sprite Small;
        public Sprite Large;
    }
}
