using System.Collections.Generic;
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

        // 머리줄 오른쪽에 확대 버튼과 [다른 표현 N개 ›]·[접기 ⌄]를 둔다. 듣기는 문장마다 옆 스피커로 한다.
        private void ApplyExpressionHeader(bool hasOthers)
        {
            othersDivider.gameObject.SetActive(false);
            float right = 78;
            if (hasOthers)
            {
                float width = othersOpen ? 110 : 172;
                PlaceHeaderButton(othersButton, right, width);
                PlaceChevron(othersButton);
                right += width + 12;
            }
            SetExpressionHeader(translationCard, right + 16);
            if (expressionExpandButton != null)
            {
                // 글자 없는 확대 버튼은 폭과 높이를 같게 둬 타원이 아니라 원으로 그린다.
                PlaceHeaderButton(expressionExpandButton, 20, HeaderButtonHeight);
                expressionExpandButton.onClick.RemoveListener(OpenExpressionWindow);
                expressionExpandButton.onClick.AddListener(OpenExpressionWindow);
            }
        }

        // 머리줄 버튼 높이. Builder가 알약 그림 배율을 이 높이에 맞춘다(DialogueTranslationBuilder.FitPill).
        private const float HeaderButtonHeight = 42;

        private static void PlaceHeaderButton(Button button, float right, float width)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -18);
            rect.sizeDelta = new Vector2(width, HeaderButtonHeight);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Midline;
                label.fontSize = 18;
                label.enableAutoSizing = false;
            }
        }

        // 글자는 왼쪽에, 꺾쇠는 버튼 오른쪽 끝에 둬 글자와 겹치지 않게 한다.
        private static void PlaceChevron(Button button)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true).rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(14, 0);
            label.offsetMax = new Vector2(-34, 0);
            var chevron = (RectTransform)button.transform.Find("Chevron");
            chevron.anchorMin = chevron.anchorMax = new Vector2(1, .5f);
            chevron.pivot = new Vector2(.5f, .5f);
            chevron.anchoredPosition = new Vector2(-24, 0);
            chevron.sizeDelta = new Vector2(16, 16);
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
            // 복제한 버튼에는 화면이 코드로 단 연결이 따라오지 않으므로 스피커를 다시 잇는다.
            var speak = window
                .Find("Viewport/Body/ExpressionRow/SpeakButton")
                .GetComponent<Button>();
            speak.onClick = new Button.ButtonClickedEvent();
            speak.onClick.AddListener(() => ListenTo(content?.UserTranslation));
            var close = window.Find("ExpandButton").GetComponent<Button>();
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(CloseExpressionWindow);
            close.transform.Find("ExpandGlyph").gameObject.SetActive(false);
            var caption = close.GetComponentInChildren<TMP_Text>(true);
            caption.text = string.Empty;
            for (int index = 0; index < 2; index++)
            {
                var stroke = new GameObject("CloseStroke", typeof(RectTransform), typeof(Image));
                stroke.transform.SetParent(close.transform, false);
                var rect = (RectTransform)stroke.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(18, 3);
                rect.localRotation = Quaternion.Euler(0, 0, index == 0 ? 45 : -45);
                stroke.GetComponent<Image>().color = new Color32(57, 51, 44, 255);
                stroke.GetComponent<Image>().raycastTarget = false;
            }
            var body = (RectTransform)window.Find("Viewport/Body");
            foreach (Transform child in body)
                if (child.name.StartsWith("Other "))
                    child.gameObject.SetActive(false);
            body.Find("OthersHeading").gameObject.SetActive(OtherExpressions != null);
            ShowOtherRows(
                body,
                new List<RectTransform>(),
                OtherExpressions ?? (IReadOnlyList<string>)System.Array.Empty<string>()
            );
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
