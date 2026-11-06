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
        private const float FontSize = 23;
        private static readonly Color Ink = new Color32(54, 43, 34, 255);
        private static readonly Color Muted = new Color32(117, 98, 80, 255);

        internal static GameObject Build(
            RectTransform content,
            TMP_Text fontSource,
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
                UiThemeId.Sticker => new Color(1f, .87f, .73f, .87f),
                UiThemeId.Cloud => new Color(.89f, .91f, 1f, .87f),
                _ => new Color(1f, .87f, .74f, .94f),
            };
            Color mint = UiThemeSelection.Shared.Current switch
            {
                UiThemeId.Sticker => new Color(.87f, .94f, .78f, .88f),
                UiThemeId.Cloud => new Color(.83f, .94f, .95f, .88f),
                _ => new Color(.83f, .94f, .83f, .94f),
            };
            if (selectedReview.HasValue)
                height = Detail(
                    rect,
                    fontSource,
                    tileSprite,
                    feedback,
                    selectedReview.Value,
                    peach,
                    mint,
                    back
                );
            else
                height = Conversation(
                    rect,
                    fontSource,
                    tileSprite,
                    avatarSprite,
                    feedback,
                    transcript,
                    peach,
                    mint,
                    selectReview
                );
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return root;
        }

        private static float Conversation(
            RectTransform root,
            TMP_Text fontSource,
            Sprite tileSprite,
            Sprite avatarSprite,
            ConversationFeedback feedback,
            FeedbackTranscriptDocument transcript,
            Color peach,
            Color mint,
            Action<int> selectReview
        )
        {
            float width = root.rect.width;
            float y = 8;
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
                float maxBubble = Mathf.Max(260, width * .69f);
                TMP_Text measure = Label(
                    root,
                    fontSource,
                    "Measure",
                    entry.Text,
                    FontSize,
                    Ink,
                    0,
                    0,
                    maxBubble - 64,
                    1
                );
                Vector2 preferred = measure.GetPreferredValues(entry.Text, maxBubble - 64, 0);
                float bubbleWidth = Mathf.Min(maxBubble, Mathf.Max(216, preferred.x + 112));
                float textHeight = measure.GetPreferredValues(entry.Text, bubbleWidth - 104, 0).y;
                measure.gameObject.SetActive(false);
                DestroyNow(measure.gameObject);
                float bubbleHeight = Mathf.Max(100, textHeight + 66);
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
                    FontSize,
                    Ink,
                    52,
                    33,
                    bubbleWidth - 104,
                    bubbleHeight - 60
                );
                if (!npc && entry.ReviewIndex.HasValue)
                {
                    int reviewIndex = entry.ReviewIndex.Value;
                    Button open = bubble.gameObject.AddComponent<Button>();
                    bubble.raycastTarget = true;
                    open.onClick.AddListener(() => selectReview(reviewIndex));
                    Image chip = Tile(
                        root,
                        "ReviewChip",
                        tileSprite,
                        mint,
                        x + bubbleWidth - 135,
                        y + bubbleHeight + 1,
                        135,
                        49
                    );
                    chip.raycastTarget = true;
                    Button chipButton = chip.gameObject.AddComponent<Button>();
                    chipButton.onClick.AddListener(() => selectReview(reviewIndex));
                    Label(
                        chip.rectTransform,
                        fontSource,
                        "ReviewChipLabel",
                        "✦ 피드백",
                        19,
                        new Color32(64, 108, 72, 255),
                        20,
                        11,
                        98,
                        26
                    ).alignment = TextAlignmentOptions.Center;
                    y += 38;
                }
                y += bubbleHeight + 28;
            }
            return y + 18;
        }

        private static float Detail(
            RectTransform root,
            TMP_Text fontSource,
            Sprite tileSprite,
            ConversationFeedback feedback,
            int reviewIndex,
            Color peach,
            Color mint,
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
                48
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
                24,
                9,
                130,
                28
            );
            Label(root, fontSource, "ReviewTitle", "선택한 문장", 31, Ink, 28, 84, width - 56, 44);
            float columnGap = 18;
            float columnWidth = (width - 56 - columnGap) / 2;
            float top = 134;
            string better = review.Reference?.English ?? review.Utterance;
            float originalHeight = Math.Max(
                158,
                Measure(fontSource, review.Utterance, columnWidth - 120, FontSize) + 116
            );
            float betterHeight = Math.Max(
                158,
                Measure(fontSource, better, columnWidth - 120, FontSize) + 116
            );
            float pairHeight = Mathf.Max(originalHeight, betterHeight);
            DetailTile(
                root,
                fontSource,
                tileSprite,
                peach,
                "내가 한 말",
                review.Utterance,
                20,
                top,
                columnWidth,
                pairHeight
            );
            DetailTile(
                root,
                fontSource,
                tileSprite,
                mint,
                "이렇게 말해요",
                better,
                20 + columnWidth + columnGap,
                top,
                columnWidth,
                pairHeight
            );
            float y = top + pairHeight + 22;
            string reason = review.Reference?.Reason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = string.IsNullOrWhiteSpace(review.Comment)
                    ? "별도의 교정 없이도 자연스럽게 전달됐어요."
                    : review.Comment;
            y =
                DetailTile(
                    root,
                    fontSource,
                    tileSprite,
                    new Color(1f, 1f, 1f, .82f),
                    "왜 이렇게 말할까요?",
                    reason,
                    20,
                    y,
                    width - 40,
                    Mathf.Max(154, Measure(fontSource, reason, width - 120, FontSize) + 118)
                ) + 18;
            if (review.Idiom != null)
            {
                string idiom = review.Idiom.English;
                if (!string.IsNullOrWhiteSpace(review.Idiom.Korean))
                    idiom += "\n" + review.Idiom.Korean;
                y =
                    DetailTile(
                        root,
                        fontSource,
                        tileSprite,
                        peach,
                        "실제 회화에서는",
                        idiom,
                        20,
                        y,
                        width - 40,
                        Mathf.Max(184, Measure(fontSource, idiom, width - 120, FontSize) + 118)
                    ) + 18;
            }
            return y + 16;
        }

        private static float DetailTile(
            RectTransform root,
            TMP_Text source,
            Sprite sprite,
            Color tint,
            string heading,
            string body,
            float x,
            float y,
            float width,
            float height
        )
        {
            Image tile = Tile(root, "DetailCard", sprite, tint, x, y, width, height);
            float inset = Mathf.Max(60, width * .09f);
            Label(
                tile.rectTransform,
                source,
                "Heading",
                heading,
                20,
                Muted,
                inset,
                27,
                width - inset * 2,
                30
            );
            Label(
                tile.rectTransform,
                source,
                "Body",
                body ?? "",
                FontSize,
                Ink,
                inset,
                68,
                width - inset * 2,
                height - 82
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
            image.type = Image.Type.Simple;
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
            float height
        )
        {
            var text = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            ).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = source.font;
            text.fontSize = size;
            text.color = color;
            text.text = value ?? "";
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            Pin(text.rectTransform, x, y, width, height);
            return text;
        }

        private static float Measure(TMP_Text source, string value, float width, float size)
        {
            float old = source.fontSize;
            source.fontSize = size;
            float result = source.GetPreferredValues(value ?? "", width, 0).y;
            source.fontSize = old;
            return result;
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
