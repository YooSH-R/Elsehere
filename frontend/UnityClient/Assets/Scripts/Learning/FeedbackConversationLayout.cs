using System;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>기존 피드백 창의 본문 안에만 그리는 대화 말풍선과 문장 카드.</summary>
    internal static class FeedbackConversationLayout
    {
        private const float FontSize = 25;
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Coral = new Color32(242, 99, 63, 255);
        private static readonly Color Muted = new Color32(117, 98, 80, 255);

        internal static GameObject Build(
            RectTransform content,
            TMP_Text fontSource,
            TMP_FontAsset displayFont,
            Button buttonTemplate,
            Sprite tileSprite,
            Sprite avatarSprite,
            ConversationFeedback feedback,
            FeedbackTranscriptDocument transcript,
            int? selectedReview,
            Action<int> selectReview,
            Action back,
            out float height
        )
        {
            var root = new GameObject("ConversationCards", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(content, false);
            Pin(rect, 0, 0, content.rect.width, 0);
            Color peach = UiThemeSelection.Shared.Current switch
            {
                UiThemeId.Sticker => new Color(1f, .97f, .91f, .97f),
                UiThemeId.Cloud => new Color(.96f, .97f, 1f, .97f),
                _ => new Color(1f, .96f, .91f, .97f),
            };
            if (selectedReview.HasValue)
                height = Detail(
                    rect,
                    fontSource,
                    displayFont,
                    tileSprite,
                    feedback,
                    selectedReview.Value,
                    back
                );
            else
                height = Conversation(
                    rect,
                    fontSource,
                    displayFont,
                    buttonTemplate,
                    tileSprite,
                    avatarSprite,
                    feedback,
                    transcript,
                    peach,
                    selectReview
                );
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return root;
        }

        private static float Conversation(
            RectTransform root,
            TMP_Text fontSource,
            TMP_FontAsset displayFont,
            Button buttonTemplate,
            Sprite tileSprite,
            Sprite avatarSprite,
            ConversationFeedback feedback,
            FeedbackTranscriptDocument transcript,
            Color peach,
            Action<int> selectReview
        )
        {
            float width = root.rect.width;
            float y = 18;
            if (transcript.Entries.Count == 0)
            {
                Label(
                    root,
                    fontSource,
                    "EmptyConversation",
                    "주고받은 대화가 없어요.",
                    28,
                    Muted,
                    28,
                    28,
                    width - 56,
                    42
                );
                return 110;
            }
            foreach (FeedbackTranscriptEntry entry in transcript.Entries)
            {
                bool npc = entry.IsNpc;
                float maxBubble = Mathf.Min(width - 84, Mathf.Max(240, width * .72f));
                TMP_Text measure = Label(
                    root,
                    fontSource,
                    "Measure",
                    entry.Text,
                    32,
                    Ink,
                    0,
                    0,
                    maxBubble - 80,
                    1
                );
                measure.font = displayFont;
                Vector2 preferred = measure.GetPreferredValues(entry.Text, maxBubble - 80, 0);
                float bubbleWidth = Mathf.Min(maxBubble, Mathf.Max(220, preferred.x + 80));
                float textHeight = measure.GetPreferredValues(entry.Text, bubbleWidth - 80, 0).y;
                measure.gameObject.SetActive(false);
                DestroyNow(measure.gameObject);
                float bubbleHeight = Mathf.Max(100, Mathf.Ceil(textHeight) + 80);
                float x = npc ? 68 : width - bubbleWidth - 26;
                Label(
                    root,
                    fontSource,
                    npc ? "NpcName" : "LearnerName",
                    npc ? feedback.NpcName : "나",
                    18,
                    Muted,
                    npc ? x + 6 : x + bubbleWidth - 68,
                    y,
                    npc ? 170 : 68,
                    25
                );
                y += 21;
                if (npc)
                {
                    Image avatar = Tile(
                        root,
                        "NpcAvatar",
                        avatarSprite != null ? avatarSprite : tileSprite,
                        Color.white,
                        8,
                        y + 11,
                        59,
                        59
                    );
                    Label(
                        avatar.rectTransform,
                        fontSource,
                        "Initial",
                        Initial(feedback.NpcName),
                        21,
                        Color.white,
                        16,
                        12,
                        27,
                        28
                    ).alignment = TextAlignmentOptions.Center;
                }
                Image bubble = Tile(
                    root,
                    npc ? "NpcBubble" : "LearnerBubble",
                    tileSprite,
                    npc ? new Color(1f, 1f, 1f, .94f) : peach,
                    x,
                    y,
                    bubbleWidth,
                    bubbleHeight
                );
                Label(
                    bubble.rectTransform,
                    fontSource,
                    "Utterance",
                    entry.Text,
                    32,
                    Ink,
                    40,
                    28,
                    bubbleWidth - 80,
                    textHeight + 8,
                    displayFont
                );
                if (!npc && entry.ReviewIndex.HasValue)
                {
                    int reviewIndex = entry.ReviewIndex.Value;
                    Button open = bubble.gameObject.AddComponent<Button>();
                    bubble.raycastTarget = true;
                    open.onClick.AddListener(() => selectReview(reviewIndex));
                    Button chip = UnityEngine.Object.Instantiate(buttonTemplate, root);
                    chip.name = "ReviewChip";
                    chip.onClick = new Button.ButtonClickedEvent();
                    chip.onClick.AddListener(() => selectReview(reviewIndex));
                    chip.interactable = true;
                    var chipRect = (RectTransform)chip.transform;
                    const float Scale = .85f;
                    LayoutElement buttonSize = buttonTemplate.GetComponent<LayoutElement>();
                    float buttonWidth = buttonSize.preferredWidth;
                    float buttonHeight = buttonSize.preferredHeight;
                    Pin(
                        chipRect,
                        x + bubbleWidth - buttonWidth * Scale - 16,
                        y + bubbleHeight - 12,
                        buttonWidth,
                        buttonHeight
                    );
                    chipRect.localScale = Vector3.one * Scale;
                    TMP_Text chipLabel = chip.GetComponentInChildren<TMP_Text>(true);
                    chipLabel.text = "보기";
                    chipLabel.font = displayFont;
                    chipLabel.alignment = TextAlignmentOptions.Midline;
                    chipLabel.enableAutoSizing = false;
                    chipLabel.fontSize = 23;
                    chipLabel.margin = Vector4.zero;
                    chipLabel.rectTransform.anchorMin = Vector2.zero;
                    chipLabel.rectTransform.anchorMax = Vector2.one;
                    chipLabel.rectTransform.pivot = new Vector2(.5f, .5f);
                    chipLabel.rectTransform.offsetMin = chipLabel.rectTransform.offsetMax =
                        new Vector2(0, -3);
                    chip.gameObject.SetActive(true);
                    y += buttonHeight * Scale + 4;
                }
                y += bubbleHeight + 28;
            }
            return y + 18;
        }

        private static float Detail(
            RectTransform root,
            TMP_Text fontSource,
            TMP_FontAsset displayFont,
            Sprite tileSprite,
            ConversationFeedback feedback,
            int reviewIndex,
            Action back
        )
        {
            float width = root.rect.width;
            UtteranceReview review = feedback.Reviews[reviewIndex];
            Image backTile = Tile(
                root,
                "BackToConversation",
                tileSprite,
                new Color(1f, 1f, 1f, .76f),
                22,
                12,
                168,
                64
            );
            backTile.raycastTarget = true;
            backTile.gameObject.AddComponent<Button>().onClick.AddListener(() => back());
            Label(
                backTile.rectTransform,
                fontSource,
                "BackLabel",
                "‹  대화로",
                19,
                Ink,
                12,
                6,
                144,
                42,
                displayFont
            ).alignment = TextAlignmentOptions.Center;
            Label(
                root,
                fontSource,
                "ReviewTitle",
                "문장 다시 보기",
                29,
                Coral,
                48,
                98,
                width - 80,
                48,
                displayFont
            );
            Tile(root, "SectionBar", null, Coral, 28, 105, 5, 28);
            const float Margin = 24;
            const float Gap = 20;
            float cardWidth = width - Margin * 2;
            float columnWidth = (cardWidth - Gap) / 2;
            float y = 148;
            string better = review.Reference?.English ?? review.Utterance;
            if (columnWidth >= 300)
            {
                float pairHeight = Mathf.Max(
                    CardHeight(root, fontSource, review.Utterance, columnWidth, displayFont, 32),
                    CardHeight(root, fontSource, better, columnWidth, displayFont, 32)
                );
                DetailTile(
                    root,
                    fontSource,
                    displayFont,
                    tileSprite,
                    Color.white,
                    "내가 한 말",
                    review.Utterance,
                    Margin,
                    y,
                    columnWidth,
                    pairHeight,
                    displayFont,
                    32
                );
                y =
                    DetailTile(
                        root,
                        fontSource,
                        displayFont,
                        tileSprite,
                        Color.white,
                        "이렇게 말해요",
                        better,
                        Margin + columnWidth + Gap,
                        y,
                        columnWidth,
                        pairHeight,
                        displayFont,
                        32
                    ) + Gap;
            }
            else
            {
                y =
                    DetailTile(
                        root,
                        fontSource,
                        displayFont,
                        tileSprite,
                        Color.white,
                        "내가 한 말",
                        review.Utterance,
                        Margin,
                        y,
                        cardWidth,
                        CardHeight(root, fontSource, review.Utterance, cardWidth, displayFont, 32),
                        displayFont,
                        32
                    ) + Gap;
                y =
                    DetailTile(
                        root,
                        fontSource,
                        displayFont,
                        tileSprite,
                        Color.white,
                        "이렇게 말해요",
                        better,
                        Margin,
                        y,
                        cardWidth,
                        CardHeight(root, fontSource, better, cardWidth, displayFont, 32),
                        displayFont,
                        32
                    ) + Gap;
            }
            string reason = review.Reference?.Reason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = string.IsNullOrWhiteSpace(review.Comment)
                    ? "별도의 교정 없이도 자연스럽게 전달됐어요."
                    : review.Comment;
            y =
                DetailTile(
                    root,
                    fontSource,
                    displayFont,
                    tileSprite,
                    new Color(1f, 1f, 1f, .86f),
                    "이 표현을 쓰는 이유",
                    reason,
                    Margin,
                    y,
                    cardWidth,
                    CardHeight(root, fontSource, reason, cardWidth)
                ) + Gap;
            if (review.Idiom != null)
            {
                string english = review.Idiom.English ?? "";
                string meaning = review.Idiom.Korean ?? "";
                float englishHeight = TextHeight(
                    root,
                    fontSource,
                    english,
                    cardWidth - 112,
                    displayFont,
                    32
                );
                float meaningHeight = TextHeight(
                    root,
                    fontSource,
                    meaning,
                    cardWidth - 112,
                    null,
                    FontSize
                );
                float height = englishHeight + meaningHeight + 182;
                Image tile = Tile(
                    root,
                    "DetailCard",
                    tileSprite,
                    Color.white,
                    Margin,
                    y,
                    cardWidth,
                    height
                );
                Label(
                    tile.rectTransform,
                    fontSource,
                    "Heading",
                    "실제 회화에서는",
                    26,
                    Coral,
                    56,
                    38,
                    cardWidth - 112,
                    40,
                    displayFont
                );
                Label(
                    tile.rectTransform,
                    fontSource,
                    "Body",
                    english,
                    32,
                    Ink,
                    56,
                    94,
                    cardWidth - 112,
                    englishHeight + 8,
                    displayFont
                );
                Label(
                    tile.rectTransform,
                    fontSource,
                    "Meaning",
                    meaning,
                    FontSize,
                    Ink,
                    56,
                    110 + englishHeight,
                    cardWidth - 112,
                    meaningHeight + 8
                );
                y += height + Gap;
            }
            return y + 16;
        }

        private static float TextHeight(
            RectTransform root,
            TMP_Text source,
            string body,
            float width,
            TMP_FontAsset font,
            float size
        )
        {
            TMP_Text measure = Label(
                root,
                source,
                "Measure",
                body,
                size,
                Ink,
                0,
                0,
                width,
                1,
                font
            );
            float height = measure.GetPreferredValues(body ?? "", width, 0).y;
            measure.gameObject.SetActive(false);
            DestroyNow(measure.gameObject);
            return Mathf.Ceil(height);
        }

        private static float CardHeight(
            RectTransform root,
            TMP_Text source,
            string body,
            float width,
            TMP_FontAsset font = null,
            float size = FontSize
        ) => Mathf.Max(190, TextHeight(root, source, body, width - 112, font, size) + 168);

        private static float DetailTile(
            RectTransform root,
            TMP_Text source,
            TMP_FontAsset displayFont,
            Sprite sprite,
            Color tint,
            string heading,
            string body,
            float x,
            float y,
            float width,
            float height,
            TMP_FontAsset bodyFont = null,
            float bodySize = FontSize
        )
        {
            Image tile = Tile(root, "DetailCard", sprite, tint, x, y, width, height);
            const float Inset = 56;
            Label(
                tile.rectTransform,
                source,
                "Heading",
                heading,
                26,
                Coral,
                Inset,
                38,
                width - Inset * 2,
                40,
                displayFont
            );
            Label(
                tile.rectTransform,
                source,
                "Body",
                body ?? "",
                bodySize,
                Ink,
                Inset,
                90,
                width - Inset * 2,
                height - 148,
                bodyFont
            );
            return y + height;
        }

        private static Image Tile(
            RectTransform parent,
            string name,
            Sprite sprite,
            Color color,
            float x,
            float y,
            float width,
            float height
        )
        {
            var image = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image)
            ).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            // FeedbackTile의 투명 여백·그림자와 둥근 모서리는 늘리지 않는다.
            image.type =
                sprite != null && sprite.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = Mathf.Max(1.7f, 280f / Mathf.Max(1, height));
            image.color = color;
            image.raycastTarget = false;
            Pin(image.rectTransform, x, y, width, height);
            return image;
        }

        private static TMP_Text Label(
            RectTransform parent,
            TMP_Text source,
            string name,
            string value,
            float size,
            Color color,
            float x,
            float y,
            float width,
            float height,
            TMP_FontAsset font = null
        )
        {
            var text = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            ).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font != null ? font : source.font;
            text.fontSize = size;
            text.color = color;
            text.text = value ?? "";
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            Pin(text.rectTransform, x, y, width, height);
            return text;
        }

        private static string Initial(string name) =>
            string.IsNullOrEmpty(name) ? "N" : name.Substring(0, 1).ToUpperInvariant();

        private static void Pin(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void DestroyNow(GameObject target)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
