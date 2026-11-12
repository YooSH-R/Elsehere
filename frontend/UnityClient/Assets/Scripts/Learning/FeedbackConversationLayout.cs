using System;
using System.Threading.Tasks;
using A305.Dialogue;
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
        private const string NoCorrectionReason = "별도의 교정 없이도 자연스럽게 전달됐어요.";
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
            RectTransform wordCardTemplate,
            Sprite wordCardBackground,
            ConversationFeedback feedback,
            FeedbackTranscriptDocument transcript,
            int? selectedReview,
            Action<int> selectReview,
            Action back,
            bool loadingMeanings,
            Action retryMeanings,
            Func<GlossaryWord, Task<bool>> saveWord,
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
                    buttonTemplate,
                    feedback,
                    selectedReview.Value,
                    back
                );
            else
                height = Conversation(
                    rect,
                    fontSource,
                    displayFont,
                    tileSprite,
                    avatarSprite,
                    wordCardTemplate,
                    wordCardBackground,
                    feedback,
                    transcript,
                    peach,
                    selectReview,
                    loadingMeanings,
                    retryMeanings,
                    saveWord
                );
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return root;
        }

        private static float Conversation(
            RectTransform root,
            TMP_Text fontSource,
            TMP_FontAsset displayFont,
            Sprite tileSprite,
            Sprite avatarSprite,
            RectTransform wordCardTemplate,
            Sprite wordCardBackground,
            ConversationFeedback feedback,
            FeedbackTranscriptDocument transcript,
            Color peach,
            Action<int> selectReview,
            bool loadingMeanings,
            Action retryMeanings,
            Func<GlossaryWord, Task<bool>> saveWord
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
                string meaning = npc
                    ? entry.Korean
                        ?? (
                            entry.TurnId > 0
                                ? loadingMeanings
                                    ? "뜻을 불러오는 중이에요…"
                                    : "뜻 다시 불러오기"
                                : "저장된 뜻이 없어요."
                        )
                    : null;
                float meaningHeight = 0;
                if (npc)
                {
                    measure.font = fontSource.font;
                    measure.fontSize = 23;
                    meaningHeight = measure.GetPreferredValues(meaning, maxBubble - 24, 0).y;
                }
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
                TMP_Text utterance = Label(
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
                utterance.richText = npc;
                if (npc)
                {
                    string escaped = entry
                        .Text.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;");
                    utterance.text = DialogueLinesText.LinkGlossary(
                        escaped,
                        entry.Glossary,
                        0,
                        null
                    );
                    utterance.raycastTarget = true;
                    utterance
                        .gameObject.AddComponent<FeedbackGlossaryHover>()
                        .Configure(entry.Glossary, wordCardTemplate, wordCardBackground, saveWord);
                    TMP_Text meaningLabel = Label(
                        root,
                        fontSource,
                        "Meaning",
                        meaning,
                        23,
                        Muted,
                        x + 12,
                        y + bubbleHeight - 12,
                        maxBubble - 24,
                        meaningHeight + 8
                    );
                    meaningLabel.richText = false;
                    if (entry.Korean == null && entry.TurnId > 0 && !loadingMeanings)
                    {
                        meaningLabel.color = Coral;
                        meaningLabel.raycastTarget = true;
                        Button retry = meaningLabel.gameObject.AddComponent<Button>();
                        retry.onClick.AddListener(() => retryMeanings());
                    }
                }
                if (!npc && entry.ReviewIndex.HasValue)
                {
                    int reviewIndex = entry.ReviewIndex.Value;
                    Button open = bubble.gameObject.AddComponent<Button>();
                    bubble.raycastTarget = true;
                    open.onClick.AddListener(() => selectReview(reviewIndex));
                }
                y += bubbleHeight + 28 + (npc ? meaningHeight + 12 : 0);
            }
            return y + 18;
        }

        private static float Detail(
            RectTransform root,
            TMP_Text fontSource,
            TMP_FontAsset displayFont,
            Sprite tileSprite,
            Button buttonTemplate,
            ConversationFeedback feedback,
            int reviewIndex,
            Action back
        )
        {
            float width = root.rect.width;
            UtteranceReview review = feedback.Reviews[reviewIndex];
            Button backButton = UnityEngine.Object.Instantiate(buttonTemplate, root);
            backButton.name = "BackToConversation";
            backButton.onClick = new Button.ButtonClickedEvent();
            backButton.onClick.AddListener(() => back());
            backButton.interactable = true;
            backButton.gameObject.SetActive(true);
            RectTransform backRect = (RectTransform)backButton.transform;
            Pin(backRect, 42, 18, 176, 44);
            backRect.localScale = Vector3.one;
            ColorBlock backColors = backButton.colors;
            backColors.normalColor = Color.white;
            backColors.highlightedColor = Color.white;
            backColors.selectedColor = Color.white;
            backColors.pressedColor = new Color(.9f, .9f, .9f, 1f);
            backButton.colors = backColors;
            backButton.targetGraphic.color = new Color32(255, 157, 119, 255);
            TMP_Text backLabel = backButton.GetComponentInChildren<TMP_Text>(true);
            backLabel.text = "전체 대화";
            backLabel.font = displayFont;
            backLabel.fontSize = 22;
            backLabel.color = Ink;
            backLabel.enableAutoSizing = false;
            backLabel.alignment = TextAlignmentOptions.Midline;
            backLabel.margin = Vector4.zero;
            backLabel.rectTransform.anchorMin = Vector2.zero;
            backLabel.rectTransform.anchorMax = Vector2.one;
            backLabel.rectTransform.offsetMin = new Vector2(16, 0);
            backLabel.rectTransform.offsetMax = new Vector2(-16, 0);
            Label(
                backRect,
                fontSource,
                "BackArrow",
                "‹",
                28,
                Ink,
                10,
                2,
                26,
                40,
                displayFont
            ).alignment = TextAlignmentOptions.Midline;
            Label(
                root,
                fontSource,
                "ReviewTitle",
                "문장 피드백",
                25,
                Coral,
                280,
                18,
                width - 304,
                44,
                displayFont
            ).alignment = TextAlignmentOptions.MidlineLeft;
            const float Margin = 24;
            const float Gap = 20;
            float cardWidth = width - Margin * 2;
            float columnWidth = (cardWidth - Gap) / 2;
            float y = 108;
            // 문장 교정 카드와 같은 이름표를 쓴다. 한국어로 한 말은 "내가 한 말 · 한국어"와 "영어로는"이다.
            CorrectionCard card = CorrectionCard.From(review);
            string saidLabel = card.SaidLabel;
            if (card.Kind == CorrectionKind.WellSaid)
                saidLabel = "잘 표현했어요";
            string betterLabel = card.BetterLabel;
            string better = card.Better;
            if (better == null)
            {
                y =
                    DetailTile(
                        root,
                        fontSource,
                        displayFont,
                        tileSprite,
                        Color.white,
                        saidLabel,
                        review.Utterance,
                        Margin,
                        y,
                        cardWidth,
                        CardHeight(root, fontSource, review.Utterance, cardWidth, displayFont, 32),
                        displayFont,
                        32
                    ) + Gap;
            }
            else if (columnWidth >= 300)
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
                    saidLabel,
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
                        betterLabel,
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
                        saidLabel,
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
                        betterLabel,
                        better,
                        Margin,
                        y,
                        cardWidth,
                        CardHeight(root, fontSource, better, cardWidth, displayFont, 32),
                        displayFont,
                        32
                    ) + Gap;
            }
            (string reasonLabel, string reason) = ReasonOf(card);
            if (reason != null)
                y =
                    DetailTile(
                        root,
                        fontSource,
                        displayFont,
                        tileSprite,
                        new Color(1f, 1f, 1f, .86f),
                        reasonLabel,
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

        // 이유 칸의 제목과 글. 한국어로 한 말에는 "자연스럽게 전달됐다"는 칭찬을 붙이지 않고, 이유가 없으면 칸을 두지 않는다.
        private static (string Label, string Text) ReasonOf(CorrectionCard card)
        {
            bool hasReason = !string.IsNullOrWhiteSpace(card.Reason);
            if (card.Better != null)
                return ("이 표현을 쓰는 이유", hasReason ? card.Reason : NoCorrectionReason);
            if (
                card.Kind == CorrectionKind.Korean
                || card.Kind == CorrectionKind.ExpressionPractice
            )
                return ("설명", hasReason ? card.Reason : null);
            return ("자연스러웠던 점", hasReason ? card.Reason : NoCorrectionReason);
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
