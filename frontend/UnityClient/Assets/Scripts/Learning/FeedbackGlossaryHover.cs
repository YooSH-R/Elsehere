using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>기존 사물 호버 카드를 재사용해 저장된 풀이만 보여 준다. 네트워크 요청은 하지 않는다.</summary>
    public sealed class FeedbackGlossaryHover
        : MonoBehaviour,
            IPointerMoveHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        private IReadOnlyList<GlossaryWord> glossary;
        private RectTransform template;
        private Sprite backgroundSprite;
        private TMP_Text text;
        private RectTransform tooltip;
        private TMP_Text wordText;
        private TMP_Text meaningText;
        private bool pinned;
        private Button closeButton;
        private Button saveButton;
        private TMP_Text saveLabel;
        private GlossaryWord selectedWord;
        private Func<GlossaryWord, Task<bool>> saveWord;
        private readonly HashSet<string> savedWords = new HashSet<string>();
        private readonly HashSet<string> savingWords = new HashSet<string>();

        public void Configure(
            IReadOnlyList<GlossaryWord> words,
            RectTransform cardTemplate,
            Sprite cardBackground,
            Func<GlossaryWord, Task<bool>> saver = null
        )
        {
            glossary = words;
            template = cardTemplate;
            backgroundSprite = cardBackground;
            saveWord = saver;
            text = GetComponent<TMP_Text>();
        }

        public void OnPointerMove(PointerEventData eventData) => ShowWord(eventData, false);

        public void OnPointerClick(PointerEventData eventData) => ShowWord(eventData, true);

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!pinned)
                Hide();
        }

        private void OnDisable() => Hide();

        private void ShowWord(PointerEventData eventData, bool pin)
        {
            if (text == null || glossary == null || template == null)
                return;
            int hit = TMP_TextUtilities.FindIntersectingLink(
                text,
                eventData.position,
                eventData.enterEventCamera
            );
            if (hit < 0)
            {
                if (pin || !pinned)
                    Hide();
                return;
            }
            string[] parts = text.textInfo.linkInfo[hit].GetLinkID().Split('/');
            if (
                parts.Length != 2
                // 숙어 안의 단어 조각은 "단어,숙어" 번호를 담는다. 피드백 말풍선은 짧은 말(첫 번호) 하나만 보인다.
                || !int.TryParse(parts[1].Split(',')[0], out int index)
                || index < 0
                || index >= glossary.Count
            )
                return;
            if (pin)
                A305.World.UiSoundEvents.Play(A305.World.UiSound.Click);
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Rect bounds = canvasRect.rect;
            ScrollRect scroll = GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll.viewport != null)
            {
                var corners = new Vector3[4];
                scroll.viewport.GetWorldCorners(corners);
                Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
                Vector2 max = canvasRect.InverseTransformPoint(corners[2]);
                bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            bounds.min += Vector2.one * 16;
            bounds.max -= Vector2.one * 16;
            if (tooltip == null)
                CreateTooltip(canvasRect);
            pinned = pinned || pin;
            closeButton.gameObject.SetActive(pinned);
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = pinned;
            GlossaryWord word = glossary[index];
            selectedWord = word;
            saveButton.gameObject.SetActive(pinned && saveWord != null && word.TurnId > 0);
            RefreshSaveButton();
            wordText.text = word.Text;
            meaningText.text = word.MeaningKo;
            wordText.margin = new Vector4(0, 0, pinned ? 34 : 0, 0);
            float naturalWidth = Mathf.Max(
                wordText.GetPreferredValues(word.Text).x,
                meaningText.GetPreferredValues(word.MeaningKo).x
            );
            float width = Mathf.Min(
                Mathf.Min(540, bounds.width),
                Mathf.Max(180, naturalWidth + 64)
            );
            tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            foreach (TMP_Text label in new[] { wordText, meaningText })
            {
                LayoutElement layout = label.GetComponent<LayoutElement>();
                layout.preferredWidth = width - 64;
                layout.preferredHeight = label.GetPreferredValues(label.text, width - 64, 0).y;
            }
            tooltip.gameObject.SetActive(true);
            // 단어장 상세 카드처럼 원래 카드 색을 쓴다. 유리 재질의 배경색 혼합이 글 뒤를 회녹색으로 물들이지 않게 한다.
            Image background = tooltip.GetComponent<Image>();
            background.material = null;
            background.sprite = backgroundSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            SurfaceBleed bleed = tooltip.GetComponent<SurfaceBleed>();
            if (bleed != null)
                bleed.Margin = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            Camera camera =
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                camera,
                out Vector2 point
            );
            tooltip.anchoredPosition = WordCardPlacement.Place(
                new Rect(point, Vector2.one),
                tooltip.rect.size,
                bounds
            );
            tooltip.SetAsLastSibling();
        }

        private void CreateTooltip(RectTransform canvas)
        {
            tooltip = Instantiate(template, canvas, false);
            tooltip.name = "FeedbackWordTooltip";
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(.5f, .5f);
            tooltip.pivot = Vector2.zero;
            // 기존 카드의 테마와 단어·뜻 레이아웃을 쓰고, 이 화면에 없는 조작은 숨긴다.
            foreach (Transform child in tooltip)
                child.gameObject.SetActive(child.name == "Word" || child.name == "Meaning");
            wordText = tooltip.Find("Word").GetComponent<TMP_Text>();
            meaningText = tooltip.Find("Meaning").GetComponent<TMP_Text>();
            wordText.fontSize = 32;
            meaningText.fontSize = 25;
            VerticalLayoutGroup column = tooltip.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 26, 26);
            column.spacing = 12;
            closeButton = tooltip.Find("CloseButton").GetComponent<Button>();
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(Hide);
            wordText.richText = meaningText.richText = false;
            foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1;
                group.blocksRaycasts = false;
            }
            closeButton.targetGraphic.raycastTarget = true;
            saveButton = tooltip.Find("WordActions/SaveButton").GetComponent<Button>();
            saveButton.transform.SetParent(tooltip, false);
            saveButton.onClick = new Button.ButtonClickedEvent();
            saveButton.onClick.AddListener(() => _ = SaveSelectedWordAsync());
            saveLabel = saveButton.GetComponentInChildren<TMP_Text>(true);
            LayoutElement saveLayout = saveButton.GetComponent<LayoutElement>();
            saveLayout.preferredWidth = 110;
            saveLayout.preferredHeight = 52;
            saveButton.targetGraphic.raycastTarget = true;
            foreach (Graphic graphic in saveButton.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic == saveButton.targetGraphic;
        }

        private void RefreshSaveButton()
        {
            if (selectedWord == null)
                return;
            bool saved = savedWords.Contains(selectedWord.Text);
            bool saving = savingWords.Contains(selectedWord.Text);
            saveButton.interactable = !saved && !saving;
            saveLabel.text =
                saved ? "담음"
                : saving ? "저장 중"
                : "담기";
        }

        private async Task SaveSelectedWordAsync()
        {
            GlossaryWord word = selectedWord;
            if (word == null || saveWord == null || !saveButton.interactable)
                return;
            savingWords.Add(word.Text);
            RefreshSaveButton();
            bool saved = await saveWord(word);
            savingWords.Remove(word.Text);
            if (saved)
                savedWords.Add(word.Text);
            if (this == null || tooltip == null)
                return;
            RefreshSaveButton();
        }

        private void Hide()
        {
            pinned = false;
            if (tooltip != null)
                tooltip.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (tooltip == null)
                return;
            if (Application.isPlaying)
                Destroy(tooltip.gameObject);
            else
                DestroyImmediate(tooltip.gameObject);
        }
    }
}
