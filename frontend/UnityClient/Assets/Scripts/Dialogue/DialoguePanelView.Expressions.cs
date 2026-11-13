using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    public partial class DialoguePanelView
    {
        [SerializeField]
        private Button expressionExpandButton;
        private GameObject expressionOverlay;

        private void ApplyExpressionHeader(bool hasExpression, bool hasOthers)
        {
            othersDivider.gameObject.SetActive(false);
            float right = 78;
            if (hasOthers)
            {
                float width = othersOpen ? 86 : 150;
                PlaceHeaderButton(othersButton, right, width);
                right += width + 12;
            }
            if (hasExpression)
            {
                PlaceHeaderButton(translationListenButton, right, 72);
                right += 84;
            }
            SetExpressionHeader(translationCard, right + 16);
            if (expressionExpandButton != null)
            {
                PlaceHeaderButton(expressionExpandButton, 20, 44);
                expressionExpandButton.onClick.RemoveListener(OpenExpressionWindow);
                expressionExpandButton.onClick.AddListener(OpenExpressionWindow);
            }
        }

        private static void PlaceHeaderButton(Button button, float right, float width)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -18);
            rect.sizeDelta = new Vector2(width, 42);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Midline;
                label.fontSize = 18;
                label.enableAutoSizing = false;
            }
        }

        private static void SetExpressionHeader(RectTransform card, float right)
        {
            var heading = card.Find("Heading").GetComponent<TMP_Text>();
            heading.rectTransform.anchorMin = new Vector2(0, 1);
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = new Vector2(26, -64);
            heading.rectTransform.offsetMax = new Vector2(-right, -16);
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            var scroll = card.GetComponent<ScrollRect>();
            scroll.viewport.offsetMin = new Vector2(26, CardBottomPadding);
            scroll.viewport.offsetMax = new Vector2(-26, -72);
            if (scroll.verticalScrollbar != null)
            {
                var bar = (RectTransform)scroll.verticalScrollbar.transform;
                bar.offsetMin = new Vector2(-16, CardBottomPadding);
                bar.offsetMax = new Vector2(-6, -72);
            }
        }

        private void AlignDialogueContent()
        {
            linesText.alignment = TextAlignmentOptions.TopLeft;
            var text = linesText.rectTransform;
            text.anchorMin = new Vector2(0, 1);
            text.anchorMax = Vector2.one;
            text.pivot = new Vector2(.5f, 1);
            float height = linesText
                .GetPreferredValues(linesText.text, historyScroll.viewport.rect.width, 0)
                .y;
            text.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (height <= historyScroll.viewport.rect.height + 1)
                text.anchoredPosition = Vector2.zero;
        }

        private void OpenExpressionWindow()
        {
            CloseExpressionWindow();
            if (!HasExpressionCard)
                return;
            var canvas = (RectTransform)GetComponentInParent<Canvas>().transform;
            expressionOverlay = new GameObject(
                "ExpressionOverlay",
                typeof(RectTransform),
                typeof(Image)
            );
            expressionOverlay.transform.SetParent(canvas, false);
            var backdrop = (RectTransform)expressionOverlay.transform;
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            expressionOverlay.GetComponent<Image>().color = new Color(0, 0, 0, .28f);
            var window = Instantiate(translationCard, backdrop);
            window.name = "ExpandedExpressionCard";
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(
                Mathf.Min(1050, canvas.rect.width - 96),
                Mathf.Min(690, canvas.rect.height - 120)
            );
            window.Find("OthersButton").gameObject.SetActive(false);
            window.Find("OthersDivider").gameObject.SetActive(false);
            SetExpressionHeader(window, 170);
            var heading = window.Find("Heading").GetComponent<TMP_Text>();
            heading.enableAutoSizing = false;
            heading.fontSize = 28;
            var listen = window.Find("ListenButton").GetComponent<Button>();
            listen.onClick = new Button.ButtonClickedEvent();
            listen.onClick.AddListener(() =>
                UserTranslationListenRequested?.Invoke(content.UserTranslation)
            );
            PlaceHeaderButton(listen, 78, 72);
            var close = window.Find("ExpandButton").GetComponent<Button>();
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(CloseExpressionWindow);
            close.transform.Find("ExpandGlyph").gameObject.SetActive(false);
            var caption = close.GetComponentInChildren<TMP_Text>(true);
            caption.text = "횞";
            caption.fontSize = 30;
            var body = (RectTransform)window.Find("Viewport/Body");
            foreach (Transform child in body)
                if (child.name.StartsWith("Other "))
                    child.gameObject.SetActive(false);
            body.Find("OthersHeading").gameObject.SetActive(OtherExpressions != null);
            if (OtherExpressions != null)
                for (int i = 0; i < OtherExpressions.Count; i++)
                {
                    var row = Instantiate(otherRowTemplate, body);
                    row.gameObject.SetActive(true);
                    row.Find("Number/Label").GetComponent<TMP_Text>().text = (i + 1).ToString();
                    row.Find("Text").GetComponent<TMP_Text>().text =
                        $"<noparse>{OtherExpressions[i]}</noparse>";
                }
            var scroll = window.GetComponent<ScrollRect>();
            scroll.content = body;
            body.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
            scroll.verticalNormalizedPosition = 1;
        }

        private void CloseExpressionWindow()
        {
            if (expressionOverlay == null)
                return;
            expressionOverlay.SetActive(false);
            if (Application.isPlaying)
                if (Application.isPlaying)
                    Destroy(expressionOverlay);
                else
                    DestroyImmediate(expressionOverlay);
            else
                DestroyImmediate(expressionOverlay);
            expressionOverlay = null;
        }
    }
}
