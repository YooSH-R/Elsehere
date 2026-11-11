using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// [연습 돌아보기] 창. 대화 노트의 [다시 연습]을 마치면 쓴 표현과 다음에 써 볼 표현을 보이고, [다시 연습]으로 같은 상황을 한 번 더 연습한다.
    /// 데이터 요청과 장소 이동은 학습 처리가 맡는다.
    /// </summary>
    public sealed class ReviewWorkbookView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private ScrollRect scroll;

        [SerializeField]
        private TMP_Text status;

        [SerializeField]
        [Tooltip("결과 아래의 [다시 연습]")]
        private Button more;

        [SerializeField]
        private Button close;

        [SerializeField]
        private Sprite resultUsedMark;

        [SerializeField]
        private Sprite resultUnusedMark;
        private readonly List<RectTransform> resultRows = new List<RectTransform>();
        private readonly List<TMP_Text> headings = new List<TMP_Text>();
        private float lastWidth;
        private ReviewCard resultCard;
        private DialogueRecord resultRecord;
        private Rect lastFreeArea;

        public event Action CloseRequested;
        public event Action<ReviewCard> PracticeRequested;
        public bool IsVisible => panel.activeSelf;

        public void Hide()
        {
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        private void Awake()
        {
            close.onClick.AddListener(() => CloseRequested?.Invoke());
            more.onClick.AddListener(() =>
            {
                if (resultCard != null)
                    PracticeRequested?.Invoke(resultCard);
            });
            Hide();
        }

        public void ShowResult(ReviewCard card, DialogueRecord record)
        {
            resultCard = card;
            resultRecord = record;
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
            status.gameObject.SetActive(record?.ReviewResult == null);
            if (record?.ReviewResult == null)
            {
                status.text = ReviewPracticeFeedback.Format(card, null);
                float bodyHeight = status.GetPreferredValues(status.text, width - 116, 0).y + 36;
                ReviewResultRow.Pin(status.rectTransform, 58, top, width - 116, bodyHeight);
                top += bodyHeight;
            }
            else
            {
                UiThemeCatalog theme = panel.GetComponent<ThemedElement>().Catalog;
                top +=
                    ResultHeading(
                        ReviewScenarioPresentation.Title(card)
                            + " · "
                            + ReviewScenarioPresentation.BuildingName(card),
                        30,
                        UiThemeRole.Title,
                        top,
                        width,
                        theme
                    ) + 20;
                var expressions = ReviewPracticeFeedback.Expressions(card, record.ReviewResult);
                top +=
                    ResultHeading(
                        ReviewPracticeFeedback.Summary(expressions),
                        25,
                        UiThemeRole.Accent,
                        top,
                        width,
                        theme
                    ) + 34;
                foreach (ReviewExpressionFeedback expression in expressions)
                {
                    RectTransform row = ReviewResultRow.Create(
                        scroll.content,
                        status,
                        theme,
                        expression,
                        expression.Used ? resultUsedMark : resultUnusedMark,
                        width - 48
                    );
                    resultRows.Add(row);
                    float height = row.sizeDelta.y;
                    ReviewResultRow.Pin(row, 24, top, width - 48, height);
                    top += height + 28;
                }
            }
            ReviewResultRow.Pin(
                (RectTransform)more.transform,
                (width - 178) / 2,
                top + 24,
                178,
                54
            );
            scroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(scroll.viewport.rect.height, top + (card == null ? 40 : 114))
            );
            scroll.verticalNormalizedPosition = 1;
            lastWidth = width;
        }

        private float ResultHeading(
            string text,
            float fontSize,
            UiThemeRole role,
            float top,
            float width,
            UiThemeCatalog theme
        )
        {
            TMP_Text label = ReviewResultRow.Text(
                scroll.content,
                status,
                text,
                fontSize,
                role,
                theme
            );
            headings.Add(label);
            float height = Mathf.Max(38, label.GetPreferredValues(label.text, width - 116, 0).y);
            ReviewResultRow.Pin(label.rectTransform, 58, top, width - 116, height);
            return height;
        }

        private void ClearRows()
        {
            foreach (RectTransform row in resultRows)
                Discard(row.gameObject);
            resultRows.Clear();
            foreach (TMP_Text heading in headings)
                Discard(heading.gameObject);
            headings.Clear();
        }

        private static void Discard(GameObject item)
        {
            item.SetActive(false);
            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }

        private void LateUpdate()
        {
            if (!IsVisible)
                return;
            Fit();
            if (Mathf.Abs(scroll.content.rect.width - lastWidth) > 1)
                ShowResult(resultCard, resultRecord);
        }

        private void Fit()
        {
            var frame = (RectTransform)panel.transform;
            Rect area = CenterWindow.Shared.FreeArea((RectTransform)frame.parent);
            if (lastFreeArea == area)
                return;
            lastFreeArea = area;
            const float scale = 1 / 1.2f;
            frame.sizeDelta = new Vector2(
                Mathf.Min(1296, area.width / scale),
                Mathf.Min(924, area.height / scale)
            );
            CenterWindow.Shared.Place(frame, scale);
            scroll.viewport.offsetMax = new Vector2(-43.2f, -125);
            scroll.viewport.offsetMin = new Vector2(43.2f, 32);
            Canvas.ForceUpdateCanvases();
        }
    }
}
