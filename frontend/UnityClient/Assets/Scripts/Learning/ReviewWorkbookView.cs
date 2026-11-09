using System;
using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>복습장 목록. 데이터 요청과 장소 이동은 학습 처리가 맡는다.</summary>
    public sealed class ReviewWorkbookView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private ScrollRect scroll;

        [SerializeField]
        private ReviewWorkbookRow rowTemplate;

        [SerializeField]
        private TMP_Text status;

        [SerializeField]
        private Button more;

        [SerializeField]
        private Button close;

        [SerializeField]
        private Button[] filters;
        private readonly List<ReviewWorkbookRow> rows = new List<ReviewWorkbookRow>();
        private readonly List<TMP_Text> buildingHeaders = new List<TMP_Text>();
        private ReviewBook book;
        private float lastWidth;
        private bool isResult;
        private ReviewCard resultCard;
        private DialogueRecord resultRecord;
        private Rect lastFreeArea;
        private bool lastResultLayout;

        public event Action CloseRequested;
        public event Action MoreRequested;
        public event Action<string> FilterRequested;
        public event Action<ReviewCard> PracticeRequested;
        public event Action<ReviewCard> DeleteRequested;
        public event Action<ReviewCard> ResultRequested;
        public bool IsVisible => panel.activeSelf;

        public void Show(ReviewBook source)
        {
            book = source;
            isResult = false;
            panel.transform.Find("Title").GetComponent<TMP_Text>().text = "복습장";
            panel.SetActive(true);
            UiLayer.Open(this);
            Fit();
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }

        public void Hide()
        {
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        public void Refresh()
        {
            if (book == null)
                return;
            if (isResult)
                return;
            ClearRows();
            float width = scroll.content.rect.width;
            float top = 14;
            foreach (
                var building in book
                    .Items.Where(card => card != null)
                    .GroupBy(ReviewScenarioPresentation.BuildingName)
            )
            {
                TMP_Text header = Instantiate(status, scroll.content);
                header.name = "BuildingHeader";
                header.text = ReviewWorkbookRow.Escape(building.Key);
                header.fontSize = 30;
                header.fontStyle = FontStyles.Bold;
                header.raycastTarget = false;
                header.gameObject.SetActive(true);
                buildingHeaders.Add(header);
                ReviewWorkbookRow.Pin(header.rectTransform, 58, top + 6, width - 116, 46);
                top += 66;
                foreach (ReviewCard card in building)
                {
                    ReviewWorkbookRow row = Instantiate(rowTemplate, scroll.content);
                    rows.Add(row);
                    float height = row.Show(
                        card,
                        width - 48,
                        book.IsBusy,
                        () => PracticeRequested?.Invoke(card),
                        () => DeleteRequested?.Invoke(card),
                        () => ResultRequested?.Invoke(card)
                    );
                    ReviewWorkbookRow.Pin(
                        (RectTransform)row.transform,
                        24,
                        top,
                        width - 48,
                        height
                    );
                    top += height + 28;
                }
                top += 16;
            }
            status.text = book.IsBusy
                ? "불러오는 중…"
                : book.Message
                    ?? (
                        book.Items.Count == 0
                            ? "대화 피드백에서 상황을 담으면 여기서 다시 연습할 수 있어요."
                            : ""
                    );
            ReviewWorkbookRow.Pin(status.rectTransform, 58, top, width - 116, 90);
            top += 102;
            more.gameObject.SetActive(book.HasMore);
            more.GetComponentInChildren<TMP_Text>().text = "더 보기";
            more.interactable = !book.IsBusy;
            ReviewWorkbookRow.Pin((RectTransform)more.transform, (width - 178) / 2, top, 178, 54);
            scroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, top + (book.HasMore ? 90 : 0))
            );
            string[] codes = { null, "CAFE", "HOTEL", "TOWN" };
            for (int i = 0; i < filters.Length; i++)
            {
                bool selected = book.SceneCode == codes[i];
                filters[i].interactable = !book.IsBusy && !selected;
                filters[i].targetGraphic.color = selected ? Color.white : Color.clear;
            }
            lastWidth = width;
        }

        private void Awake()
        {
            close.onClick.AddListener(() => CloseRequested?.Invoke());
            more.onClick.AddListener(() =>
            {
                if (isResult && resultCard != null)
                    PracticeRequested?.Invoke(resultCard);
                else
                    MoreRequested?.Invoke();
            });
            string[] codes = { null, "CAFE", "HOTEL", "TOWN" };
            for (int i = 0; i < filters.Length; i++)
            {
                string code = codes[i];
                filters[i].onClick.AddListener(() => FilterRequested?.Invoke(code));
            }
            rowTemplate.gameObject.SetActive(false);
            Hide();
        }

        public void ShowResult(ReviewCard card, DialogueRecord record)
        {
            resultCard = card;
            resultRecord = record;
            isResult = true;
            panel.SetActive(true);
            panel.transform.Find("Title").GetComponent<TMP_Text>().text = "연습 돌아보기";
            UiLayer.Open(this);
            Fit();
            ClearRows();
            more.gameObject.SetActive(card != null);
            more.interactable = true;
            more.GetComponentInChildren<TMP_Text>().text = "다시 연습";
            float width = scroll.content.rect.width;
            float top = 24;
            status.text =
                card == null
                    ? ""
                    : "<b>"
                        + ReviewWorkbookRow.Escape(ReviewScenarioPresentation.Title(card))
                        + "</b>\n"
                        + ReviewWorkbookRow.Escape(ReviewScenarioPresentation.BuildingName(card))
                        + " · "
                        + ReviewWorkbookRow.Escape(card.Npc?.Name)
                        + "\n\n";
            status.text += ReviewPracticeFeedback.Format(card, record.ReviewResult);
            float bodyWidth = Mathf.Max(160, width - 116);
            float bodyHeight = status.GetPreferredValues(status.text, bodyWidth, 0).y + 36;
            ReviewWorkbookRow.Pin(status.rectTransform, 58, top, bodyWidth, bodyHeight);
            ReviewWorkbookRow.Pin(
                (RectTransform)more.transform,
                (width - 178) / 2,
                top + bodyHeight + 24,
                178,
                54
            );
            scroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, top + bodyHeight + (card == null ? 40 : 114))
            );
            scroll.verticalNormalizedPosition = 1;
            lastWidth = width;
        }

        private void ClearRows()
        {
            foreach (var header in buildingHeaders)
            {
                header.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(header.gameObject);
                else
                    DestroyImmediate(header.gameObject);
            }
            buildingHeaders.Clear();
            foreach (ReviewWorkbookRow row in rows)
            {
                row.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(row.gameObject);
                else
                    DestroyImmediate(row.gameObject);
            }
            rows.Clear();
        }

        private void LateUpdate()
        {
            if (!IsVisible)
                return;
            Fit();
            if (Mathf.Abs(scroll.content.rect.width - lastWidth) > 1)
            {
                if (isResult)
                    ShowResult(resultCard, resultRecord);
                else
                    Refresh();
            }
        }

        private void Fit()
        {
            var frame = (RectTransform)panel.transform;
            Rect area = CenterWindow.Shared.FreeArea((RectTransform)frame.parent);
            if (lastFreeArea == area && lastResultLayout == isResult)
                return;
            lastFreeArea = area;
            lastResultLayout = isResult;
            const float scale = 1 / 1.2f;
            frame.sizeDelta = new Vector2(
                Mathf.Min(1296, area.width / scale),
                Mathf.Min(924, area.height / scale)
            );
            CenterWindow.Shared.Place(frame, scale);
            bool narrow = frame.rect.width < 860;
            var sections = (RectTransform)panel.transform.Find("Sections");
            sections.gameObject.SetActive(!isResult);
            sections.anchorMin = sections.anchorMax = new Vector2(narrow ? 0 : 1, 1);
            sections.pivot = new Vector2(narrow ? 0 : 1, .5f);
            sections.anchoredPosition = narrow ? new Vector2(42, -132) : new Vector2(-120, -64);
            foreach (Button filter in filters)
            {
                var size = filter.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = narrow
                    ? Mathf.Clamp((frame.rect.width - 104) / 4, 64, 92)
                    : 92;
            }
            scroll.viewport.offsetMax = new Vector2(-43.2f, narrow ? -188 : -125);
            scroll.viewport.offsetMin = new Vector2(43.2f, 32);
            Canvas.ForceUpdateCanvases();
        }
    }
}
