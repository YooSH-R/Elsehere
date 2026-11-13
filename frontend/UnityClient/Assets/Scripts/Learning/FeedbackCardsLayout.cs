using System.Collections.Generic;
using A305.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 피드백 창 요약 탭의 요약 카드·표현 줄과 문장 교정 탭의 카드를 확정 시안(F1·F2)의 자리에 놓는다.
    /// 자리는 시안 px에 1.2를 곱한 판 안의 단위이고, 글자 칸의 위쪽은 시안 글자 상자(글꼴 ascent)의 위쪽에 맞춘다.
    /// 칸·칩·[담기] 그림(Art/UI/Feedback)은 요소 둘레에 시안 px 32씩 투명 여백(그림자 자리)을 두고 2.4배로 렌더링했으므로,
    /// 요소 상자에서 여백만큼 바깥에 그림 크기 그대로(판 안 1단위 = 그림 2px) 깐다.
    /// </summary>
    internal static class FeedbackCardsLayout
    {
        internal const float Px = 1.2f;
        internal const float ArtPad = 32 * Px;

        // 시안의 투명한 글자색(고친 문장 .55, 한국어로 한 말 .75)을 흰 카드 위에 미리 섞은 불투명 색.
        // 얇은 SDF 글자에 투명도를 곱하면 시안보다 옅어 보인다.
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color StruckInk = new Color32(140, 133, 126, 255);
        private static readonly Color KoreanInk = new Color32(98, 91, 82, 255);

        /// <summary>요약 글과 잘했어요·더 해 봐요 메모를 채우고 카드 높이를 돌려준다.</summary>
        internal static float SummaryCard(
            RectTransform card,
            ConversationFeedback feedback,
            float width
        )
        {
            // 카드 안쪽 여백 옆 26·위아래 20에 테두리 1.5. 요약 글은 19px, 줄 높이 1.6(30.4px)이다.
            int lines = PlaceText(Text(card, "Text"), feedback.Summary, 27, 24, width / Px - 54);
            float bottom = 21 + lines * 30.4f;
            float notesTop = lines > 0 ? bottom + 14 : bottom;
            string good = string.Join("\n", feedback.GoodNotes);
            string more = string.Join("\n", feedback.ImproveNotes);
            bool hasGood = !string.IsNullOrWhiteSpace(good);
            bool hasMore = !string.IsNullOrWhiteSpace(more);
            if (hasGood || hasMore)
            {
                // 두 메모는 사이 12로 나란히 두고, 하나뿐이면 카드 폭을 다 쓴다. 높이는 긴 쪽에 맞춘다.
                float notesWidth = width / Px - 54;
                float noteWidth = hasGood && hasMore ? (notesWidth - 12) / 2 : notesWidth;
                float goodHeight = hasGood ? NoteBody(card.Find("Good"), good, noteWidth) : 0;
                float moreHeight = hasMore ? NoteBody(card.Find("More"), more, noteWidth) : 0;
                float noteHeight = Mathf.Max(goodHeight, moreHeight);
                float x = 27;
                if (hasGood)
                {
                    Note(card.Find("Good"), x, notesTop, noteWidth, noteHeight);
                    x += noteWidth + 12;
                }
                if (hasMore)
                    Note(card.Find("More"), x, notesTop, noteWidth, noteHeight);
                bottom = notesTop + noteHeight;
            }
            card.Find("Good").gameObject.SetActive(hasGood);
            card.Find("More").gameObject.SetActive(hasMore);
            float height = bottom + 21;
            Pin(card, 0, 0, width, height * Px);
            Tile(card.Find("Tile"), width, height * Px);
            return height * Px;
        }

        /// <summary>
        /// 표현 줄 한 칸(높이 70): 영어 표현(Jua 24) 뒤 14에 "뜻 · 쓰는 때"(15px), 오른쪽에 [담기]. 뜻이 같은 줄에 들어가지 않으면
        /// 표현 아래로 내리고 칸을 그만큼 키운다. 칸 높이를 돌려준다.
        /// </summary>
        internal static float SituationRow(
            RectTransform row,
            SituationExpressionData situation,
            float width,
            Sprite pickSprite
        )
        {
            Button pick = row.Find("SaveButton").GetComponent<Button>();
            float pickWidth = pick.gameObject.activeSelf ? ArtWidth(pickSprite) / Px + 14 : 0;
            float room = width / Px - 25 - 17 - pickWidth;
            TMP_Text phrase = Text(row, "Phrase");
            TMP_Text meaning = Text(row, "Meaning");
            phrase.text = situation.Expression;
            meaning.text = FeedbackPages.MeaningLine(situation);
            meaning.gameObject.SetActive(!string.IsNullOrEmpty(meaning.text));
            float phraseWidth = Mathf.Min(phrase.GetPreferredValues(phrase.text).x / Px, room);
            float meaningWidth = meaning.GetPreferredValues(meaning.text).x / Px;
            float height = 70;
            bool sameLine = SituationRowLayout.MeaningOnSameLine(
                phraseWidth * Px,
                meaningWidth * Px,
                room * Px
            );
            if (sameLine)
            {
                PlaceText(phrase, phrase.text, 25, 23, phraseWidth + 1);
                PlaceText(meaning, meaning.text, 25 + phraseWidth + 14, 26, meaningWidth + 1);
            }
            else
            {
                float phraseHeight = Height(phrase, room);
                float meaningHeight = Height(meaning, room);
                float block = phraseHeight + 6 + meaningHeight;
                height = Mathf.Max(70, block + 32);
                float top = (height - block) / 2;
                PlaceText(phrase, phrase.text, 25, top, room);
                PlaceText(meaning, meaning.text, 25, top + phraseHeight + 6, room);
            }
            Pin(row, 0, 0, width, height * Px);
            Tile(row.Find("Tile"), width, height * Px);
            // [담기]는 오른쪽 안쪽 17, 세로 가운데
            PlacePick(pick, pickSprite, width - 17 * Px, height * Px / 2);
            return height * Px;
        }

        /// <summary>
        /// 문장 교정 카드 한 장(시안 F2 .fix). 첫 줄은 칩과 내가 한 말, 둘째 줄은 칩과 영어 문장(Jua 27), 그 아래 이유(16px)이고
        /// 오른쪽 150 자리 가운데에 [담기]를 둔다. 잘한 문장은 작은 카드(.small)에 칩과 그 문장(Jua 22)만 둔다. 카드 높이를 돌려준다.
        /// </summary>
        internal static float Correction(
            RectTransform card,
            CorrectionCard model,
            float width,
            Sprite saidChip,
            Sprite betterChip,
            Sprite pickSprite,
            ICollection<(FeedbackExpressionExample Example, Button Pick)> examplePicks = null
        )
        {
            bool small = model.Kind == CorrectionKind.WellSaid;
            float side = small ? 23 : 27;
            float top = small ? 15 : 21;
            float right = model.CanSave ? 151 : 23;
            TMP_Text said = Text(card, "Said");
            TMP_Text better = Text(card, "Better");
            Button pick = card.Find("SaveButton").GetComponent<Button>();
            bool canSave = pick.gameObject.activeSelf;
            Style(said, model, better);
            // 칩과 글자는 글자 아래 기준선을 맞춘다. 한 줄 높이(칩 30·19px 글자 23)에서 시안이 둔 자리다.
            float saidChipTop = small ? 1.5f : 0;
            float saidTextTop = small ? 3 : 1.5f;
            float saidLine = small ? 27.5f : 22.67f;
            float firstLine = small ? 31.5f : 30;
            float chipWidth = Chip(card.Find("SaidChip"), saidChip, side, top + saidChipTop);
            float textX = side + chipWidth + 12;
            float saidWidth = width / Px - right - textX;
            int saidLines = PlaceText(said, model.Said, textX, top + saidTextTop, saidWidth);
            float y = top + firstLine + (saidLines - 1) * saidLine;

            bool hasBetter = model.Better != null;
            card.Find("BetterChip").gameObject.SetActive(hasBetter);
            better.gameObject.SetActive(hasBetter);
            if (hasBetter)
            {
                float lineTop = y + 10;
                Transform chip = card.Find("BetterChip");
                float betterX = side + Chip(chip, betterChip, side, lineTop + 5.5f) + 12;
                float room = width / Px - right - betterX;
                int betterLines = PlaceText(better, model.Better, betterX, lineTop + 3, room);
                y = lineTop + 35.5f + (betterLines - 1) * 33.75f;
                if (!string.IsNullOrWhiteSpace(model.BetterMeaning))
                {
                    TMP_Text meaning = Object.Instantiate(Text(card, "Reason"), card);
                    meaning.name = "BetterMeaning";
                    PlaceText(meaning, model.BetterMeaning, betterX, y + 5, room);
                    y += Height(meaning, room) + 9;
                }
            }

            TMP_Text reason = Text(card, "Reason");
            float reasonWidth = width / Px - side - right;
            int reasonLines = PlaceText(
                reason,
                model.AssessmentReason,
                side,
                y + 12.5f,
                reasonWidth
            );
            if (reasonLines > 0)
                y += 10 + reasonLines * 24;
            float mainBottom = y;
            foreach (FeedbackExpressionExample example in model.Examples)
            {
                y += 24;
                var row = new GameObject(
                    "ExpressionExample",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                row.SetParent(card, false);
                float rowHeight = ExpressionRow(
                    row,
                    example,
                    card,
                    width,
                    side,
                    betterChip,
                    pickSprite,
                    canSave,
                    out Button examplePick
                );
                Pin(row, 0, y * Px, width, rowHeight * Px);
                if (canSave)
                    examplePicks?.Add((example, examplePick));
                y += rowHeight;
            }
            float height = y + top;
            Pin(card, 0, 0, width, height * Px);
            Tile(card.Find("Tile"), width, height * Px);
            pick.gameObject.SetActive(pick.gameObject.activeSelf && model.CanSave);
            if (pick.gameObject.activeSelf)
                PlacePick(pick, pickSprite, width - 23 * Px, (mainBottom + top) * Px / 2);
            return height * Px;
        }

        private static float ExpressionRow(
            RectTransform row,
            FeedbackExpressionExample example,
            RectTransform template,
            float width,
            float side,
            Sprite betterChip,
            Sprite pickSprite,
            bool canSave,
            out Button pick
        )
        {
            float right = canSave ? 151 : 23;
            float room = width / Px - side - right;
            TMP_Text condition = Object.Instantiate(Text(template, "Reason"), row);
            condition.name = "Condition";
            condition.color = KoreanInk;
            PlaceText(condition, example.ConditionKo, side, 0, room);
            float y = string.IsNullOrWhiteSpace(example.ConditionKo)
                ? 0
                : Height(condition, room) + 10;
            Transform chip = Object.Instantiate(template.Find("BetterChip"), row);
            chip.gameObject.SetActive(true);
            float englishX = side + Chip(chip, betterChip, side, y + 5.5f) + 12;
            float englishRoom = width / Px - right - englishX;
            TMP_Text english = Object.Instantiate(Text(template, "Better"), row);
            english.name = "Expression";
            english.color = Ink;
            english.fontStyle = FontStyles.Normal;
            PlaceText(english, example.Expression, englishX, y + 3, englishRoom);
            y += Mathf.Max(36, Height(english, englishRoom) + 8);
            TMP_Text meaning = Object.Instantiate(Text(template, "Reason"), row);
            meaning.name = "Meaning";
            meaning.color = KoreanInk;
            PlaceText(meaning, example.MeaningKo, englishX, y + 4, englishRoom);
            if (!string.IsNullOrWhiteSpace(example.MeaningKo))
                y += Height(meaning, englishRoom) + 8;
            pick = Object.Instantiate(template.Find("SaveButton").GetComponent<Button>(), row);
            pick.name = "SaveButton";
            pick.onClick = new Button.ButtonClickedEvent();
            pick.gameObject.SetActive(canSave);
            if (canSave)
                PlacePick(pick, pickSprite, width - 23 * Px, y * Px / 2);
            return y;
        }

        /// <summary>[담기]를 오른쪽 끝(<paramref name="right"/>)과 세로 가운데(<paramref name="middle"/>)에 맞춰 둔다.</summary>
        internal static void PlacePick(Button pick, Sprite sprite, float right, float middle)
        {
            Image art = pick.transform.Find("Art").GetComponent<Image>();
            art.sprite = sprite;
            float width = ArtWidth(sprite);
            float height = sprite.rect.height / 2 - ArtPad * 2;
            var rect = (RectTransform)pick.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(right - width, -(middle - height / 2));
            rect.sizeDelta = new Vector2(width, height);
            Art(art);
        }

        /// <summary>그림 조각의 요소 폭(판 안 단위). 그림은 둘레에 투명 여백이 있다.</summary>
        internal static float ArtWidth(Sprite sprite) => sprite.rect.width / 2 - ArtPad * 2;

        /// <summary>그림 조각을 부모 요소의 왼쪽 위에서 여백만큼 바깥에 그림 크기 그대로 깐다.</summary>
        internal static void Art(Image art)
        {
            Rect size = art.sprite.rect;
            RectTransform rect = art.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(-ArtPad, ArtPad);
            rect.sizeDelta = new Vector2(size.width / 2, size.height / 2);
        }

        // 글자 칸을 시안 px 자리(x, y)에 폭 width로 두고 줄 수를 돌려준다. 글이 없으면 숨기고 0이다.
        private static int PlaceText(TMP_Text text, string value, float x, float y, float width)
        {
            text.text = value ?? "";
            bool shown = !string.IsNullOrWhiteSpace(text.text);
            text.gameObject.SetActive(shown);
            if (!shown)
                return 0;
            Pin(text.rectTransform, x * Px, y * Px, width * Px, Height(text, width) * Px);
            return Mathf.Max(1, text.GetTextInfo(text.text).lineCount);
        }

        // 폭 width(시안 px)에서 글이 차지하는 높이(시안 px)
        private static float Height(TMP_Text text, float width) =>
            text.GetPreferredValues(text.text, width * Px, 0).y / Px;

        // 메모(안쪽 여백 12·16): 머리말(Jua 16, 줄 24)과 2 아래 본문(16px, 줄 높이 1.5). 메모 높이를 돌려준다.
        private static float NoteBody(Transform note, string body, float width)
        {
            TMP_Text text = Text(note, "Body");
            int lines = PlaceText(text, body, 16, 40.5f, width - 32);
            return 12 + 24 + 2 + lines * 24 + 12;
        }

        private static void Note(Transform note, float x, float y, float width, float height)
        {
            var rect = (RectTransform)note;
            Pin(rect, x * Px, y * Px, width * Px, height * Px);
            Tile(note.Find("Tile"), width * Px, height * Px);
            TMP_Text head = Text(note, "Head");
            Pin(head.rectTransform, 16 * Px, 16 * Px, (width - 32) * Px, 16 * Px);
        }

        // 칩 그림을 (x, y)에 두고 칩 폭(시안 px)을 돌려준다.
        private static float Chip(Transform chip, Sprite sprite, float x, float y)
        {
            var image = chip.GetComponent<Image>();
            image.sprite = sprite;
            Rect size = sprite.rect;
            Pin(
                image.rectTransform,
                x * Px - ArtPad,
                y * Px - ArtPad,
                size.width / 2,
                size.height / 2
            );
            return ArtWidth(sprite) / Px;
        }

        // 고친 영어 문장은 옅은 글자에 줄을 긋고, 한국어로 한 말은 그대로 둔다. 잘한 문장은 Jua 22로 보인다.
        private static void Style(TMP_Text said, CorrectionCard model, TMP_Text better)
        {
            switch (model.Kind)
            {
                case CorrectionKind.Corrected:
                    said.color = StruckInk;
                    said.fontStyle = FontStyles.Strikethrough;
                    break;
                case CorrectionKind.Korean:
                    said.color = KoreanInk;
                    said.fontStyle = FontStyles.Normal;
                    break;
                case CorrectionKind.ExpressionPractice:
                    said.color = Ink;
                    said.fontStyle = FontStyles.Normal;
                    break;
                default:
                    said.font = better.font;
                    said.fontSharedMaterial = better.fontSharedMaterial;
                    said.fontSize = 22 * Px;
                    said.color = Ink;
                    said.fontStyle = FontStyles.Normal;
                    break;
            }
        }

        // 칸 그림(9-slice)을 요소 상자보다 여백만큼 크게 깐다.
        private static void Tile(Transform tile, float width, float height) =>
            Pin((RectTransform)tile, -ArtPad, -ArtPad, width + ArtPad * 2, height + ArtPad * 2);

        private static TMP_Text Text(Transform parent, string name) =>
            parent.Find(name).GetComponent<TMP_Text>();

        // 부모 왼쪽 위 기준 (x, y) 자리에 폭·높이를 둔다(판 안 단위).
        private static void Pin(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
