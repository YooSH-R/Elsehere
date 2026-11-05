using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Mission;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 마을 HUD를 확정 시안 g1과 똑같이 만든다. 크기·여백·글꼴은 시안 HTML의 값(px)에 1.2를 곱한 캔버스 단위이고,
    /// 단계 칩·진행 칸·목표 체크·코인처럼 그림인 부분은 시안 HTML을 그대로 렌더링한 그림(Art/UI/Hud)을 쓴다.
    /// 왼쪽 위 퀘스트 카드에 단계 칩·장소·할 일·상황·진행 칸·목표 줄을 두고, 카드를 누르면 여행 수첩이 열린다(예전 지도 버튼은 지운다).
    /// 오른쪽 위 휴대폰 버튼 왼쪽에는 같은 크기의 게임 시계 칩과 코인 칩을 둔다. 유리는 뒤 화면을 흐리게 비추는 테마 유리를 쓴다.
    /// 퀘스트 카드 바로 아래에는 NPC가 준비해 건네기로 한 것을 기다리는 카드를 같은 유리·폭으로 둔다.
    /// </summary>
    public static class QuestCardBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Card";
        private const string PlaceholderMenuPath =
            "Tools/A305/Mission/Apply Quest Card Placeholder";
        private const string CoinChangeMenuPath = "Tools/A305/Mission/Apply Coin Change Labels";
        private const string FollowUpMenuPath = "Tools/A305/Mission/Apply Follow-up Card";
        private const string QuestIconMenuPath = "Tools/A305/Mission/Apply Quest Icon";
        private const string QuestIconArtPath = "Assets/Art/UI/Quest/QuestHeaderIcon.png";
        private const string CoinFloatMaterialPath = "Assets/Art/UI/Hud/Jua Coin Float.mat";
        private const string WhitePanelPath = "Assets/Art/UI/Mission/GlassPanel.png";
        private const string ClosePath = "Assets/Art/UI/Feedback/FeedbackClose.png";

        // 서버 상태를 받기 전 카드의 할 일. 카드 자체를 눌러 수첩을 열며, 카드가 커지지 않게 한 줄에 들어가는 길이로 쓴다.
        internal const string DirectionPlaceholder = "카드를 눌러 퀘스트를 확인하세요.";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private const string ArtFolder = "Assets/Art/UI/Hud";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        private const string BodyFontPath = "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 렌더링한 그림은 사방에 그림자·빛 번짐 자리(12px, 휴대폰 버튼은 32·24)를 둔다.
        private const float ArtPad = 12;

        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        /// <summary>HUD를 다시 만들지 않고 코인 칩에 코인이 바뀔 때 보이는 글자(+10·−8)와 산 것 알약만 더한다.</summary>
        [MenuItem(CoinChangeMenuPath)]
        public static void ApplyCoinChangeToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                    CoinChangeLabels(
                        root.GetComponent<FirstDayMissionView>(),
                        (RectTransform)root.transform.Find("Content/Status/Coins")
                    )
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>HUD를 다시 만들지 않고 하는 퀘스트가 없을 때 카드 대신 두는 퀘스트 아이콘만 만든다.</summary>
        [MenuItem(QuestIconMenuPath)]
        public static void ApplyQuestIconToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                    QuestIcon(
                        root.GetComponent<FirstDayMissionView>(),
                        root.transform.Find("Content")
                    )
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>카드를 다시 만들지 않고 서버 상태를 받기 전의 할 일 문구만 맞춘다.</summary>
        [MenuItem(PlaceholderMenuPath)]
        public static void ApplyPlaceholderToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                    root
                        .transform.Find("Content/CurrentGoal/Direction")
                        .GetComponent<TMP_Text>()
                        .text = DirectionPlaceholder
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>HUD를 다시 만들지 않고 퀘스트 카드 아래 기다리는 카드만 다시 만들어 연결한다.</summary>
        [MenuItem(FollowUpMenuPath)]
        public static void ApplyFollowUpCardToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, FollowUpCard);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            var view = root.GetComponent<FirstDayMissionView>();
            Transform content = root.transform.Find("Content");
            Transform mapButton = content.Find("MapButton");
            if (mapButton != null)
                Object.DestroyImmediate(mapButton.gameObject);
            QuestCard(view, (RectTransform)content.Find("CurrentGoal"));
            StatusChips(view, content);
            QuestIcon(view, content);
            // 수첩은 카드 자리에 펼친다. 펼친 동안 카드는 숨는다.
            var journal = (RectTransform)content.Find("Journal");
            journal.anchoredPosition = new Vector2(28 * Px, -28 * Px);
            FollowUpCard(root);
        }

        // 퀘스트 카드 바로 아래 기다리는 카드. 퀘스트 카드와 같은 폭·테마 유리·옆 여백을 쓰고, 머리말(Jua 18, 옅은 갈색) 아래에
        // 줄마다 목표 줄과 같은 동그라미 체크(24) + 받을 것(Jua 20)·누가 어디서(Pretendard 15) + 남은 시간(Jua 22, 강조색)을 둔다.
        // 자리는 FollowUpCardView가 실행 중에 퀘스트 카드 높이에 맞춰 옮긴다. 기다리는 것이 없으면 숨으므로 처음에는 끈다.
        private static void FollowUpCard(GameObject root)
        {
            var view = root.GetComponent<FirstDayMissionView>();
            Transform content = root.transform.Find("Content");
            var questCard = (RectTransform)content.Find("CurrentGoal");
            Transform old = content.Find("FollowUpCard");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform card = Rect("FollowUpCard", content);
            card.SetSiblingIndex(questCard.GetSiblingIndex() + 1);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0, 1);
            card.sizeDelta = new Vector2(questCard.sizeDelta.x, 0);
            card.anchoredPosition =
                questCard.anchoredPosition + Vector2.down * (questCard.rect.height + 12 * Px);
            Image glass = BuilderTools.GetOrAddComponent<Image>(card.gameObject);
            Theme(card, UiThemeRole.Panel);
            // 알림만 보이는 카드라 뒤의 장면을 누르는 것을 막지 않는다.
            glass.raycastTarget = false;
            var column = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(card.gameObject);
            column.padding = new RectOffset(Round(23), Round(23), Round(18), Round(20));
            column.spacing = 10 * Px;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(card.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text header = Text(
                card,
                "Header",
                DisplayFontPath,
                18,
                Muted,
                FollowUpCardText.Header
            );
            header.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform rows = Rect("Rows", card);
            var list = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(rows.gameObject);
            list.spacing = 8 * Px;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            FollowUpRowView row = FollowUpRow(rows);

            var cardView = BuilderTools.GetOrAddComponent<FollowUpCardView>(root);
            BuilderTools.SetReference(cardView, "card", card);
            BuilderTools.SetReference(cardView, "questCard", questCard);
            BuilderTools.SetReference(cardView, "questIcon", content.Find("QuestIcon"));
            BuilderTools.SetReference(cardView, "rows", rows);
            BuilderTools.SetReference(cardView, "waitingMark", Sprite("CheckEmpty"));
            BuilderTools.SetReference(cardView, "readyMark", Sprite("CheckDone"));
            BuilderTools.SetReference(view, "followUpCard", cardView);
            row.gameObject.SetActive(true);
            card.gameObject.SetActive(false);
        }

        // 기다리는 것 하나의 줄. 다 되면 줄 바탕(강조색, 처음에는 투명)이 두 번 반짝인다.
        private static FollowUpRowView FollowUpRow(RectTransform rows)
        {
            Color accent = Design().ToneColor(UiTone.Primary);
            RectTransform row = Row(rows, "Row", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(
                Round(6),
                Round(8),
                Round(6),
                Round(6)
            );

            RectTransform flashRect = Rect("Flash", row);
            Fill(flashRect);
            flashRect.SetAsFirstSibling();
            BuilderTools.GetOrAddComponent<LayoutElement>(flashRect.gameObject).ignoreLayout = true;
            var flash = BuilderTools.GetOrAddComponent<Image>(flashRect.gameObject);
            // 큰 지도와 같은 흰 둥근 판 그림(모서리 44)을 모서리 16으로 줄여 쓴다.
            flash.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePanelPath);
            flash.type = UnityEngine.UI.Image.Type.Sliced;
            flash.pixelsPerUnitMultiplier = 44f / (16 * Px);
            flash.color = new Color(accent.r, accent.g, accent.b, 0f);
            flash.raycastTarget = false;

            RectTransform mark = Box(row, "Mark", 24, 24);
            Art(mark, "CheckEmpty", 24 + 2 * ArtPad, 28 + 2 * ArtPad, ArtPad, ArtPad);

            RectTransform text = Rect("Text", row);
            var lines = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(text.gameObject);
            lines.spacing = 2 * Px;
            lines.childAlignment = TextAnchor.MiddleLeft;
            lines.childControlWidth = lines.childControlHeight = true;
            lines.childForceExpandWidth = true;
            lines.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<LayoutElement>(text.gameObject).flexibleWidth = 1;
            TMP_Text title = Text(text, "Title", DisplayFontPath, 20, Ink, "아이스 라테");
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.margin = new Vector4(0, 2 * Px, 0, 0);
            TMP_Text detail = Text(text, "Detail", BodyFontPath, 15, Muted, "Mia · 카페");
            detail.alignment = TextAlignmentOptions.MidlineLeft;
            detail.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text countdown = Text(row, "Countdown", DisplayFontPath, 22, accent, "0:45");
            countdown.alignment = TextAlignmentOptions.MidlineRight;
            countdown.margin = new Vector4(0, 2 * Px, 0, 0);

            // 줄 끝 닫기 X(28). 다른 창과 같은 닫기 그림(52에 그림자 자리 왼쪽·오른쪽 14, 위 12, 아래 20)을 줄여 쓴다.
            const float CloseSize = 28;
            const float CloseScale = CloseSize / 52f;
            RectTransform close = Box(row, "Close", CloseSize, CloseSize);
            RectTransform closeArt = Rect("Art", close);
            closeArt.anchorMin = Vector2.zero;
            closeArt.anchorMax = Vector2.one;
            closeArt.offsetMin = new Vector2(-14, -20) * CloseScale * Px;
            closeArt.offsetMax = new Vector2(14, 12) * CloseScale * Px;
            var closeImage = BuilderTools.GetOrAddComponent<Image>(closeArt.gameObject);
            closeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ClosePath);
            closeImage.raycastTarget = false;
            var hit = BuilderTools.GetOrAddComponent<Image>(close.gameObject);
            hit.color = Color.clear;
            var closeButton = BuilderTools.GetOrAddComponent<Button>(close.gameObject);
            closeButton.targetGraphic = closeImage;

            var rowView = BuilderTools.GetOrAddComponent<FollowUpRowView>(row.gameObject);
            QuestBoardBuilder.Listen(closeButton, rowView.Close);
            BuilderTools.SetReference(rowView, "mark", mark.Find("Art").GetComponent<Image>());
            BuilderTools.SetReference(rowView, "title", title);
            BuilderTools.SetReference(rowView, "detail", detail);
            BuilderTools.SetReference(rowView, "countdown", countdown);
            BuilderTools.SetReference(rowView, "flash", flash);
            return rowView;
        }

        // 시안 .quest: 왼쪽·위 28, 폭 410, 안쪽 여백 위 21·옆 23·아래 23(테두리 1 포함), 모서리 32
        private static void QuestCard(FirstDayMissionView view, RectTransform card)
        {
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0, 1);
            card.anchoredPosition = new Vector2(28 * Px, -28 * Px);
            card.sizeDelta = new Vector2(410 * Px, card.sizeDelta.y);
            Theme(card, UiThemeRole.Panel);
            var column = card.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(Round(23), Round(23), Round(21), Round(23));
            column.spacing = 0;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(card.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            // 카드를 누르면 여행 수첩을 연다.
            var open = BuilderTools.GetOrAddComponent<Button>(card.gameObject);
            open.targetGraphic = card.GetComponent<Image>();
            BuilderTools.SetReference(view, "journalButton", open);
            foreach (string old in new[] { "Header", "Progress", "Checklist", "Goals" })
            {
                Transform part = card.Find(old);
                if (part != null)
                    Object.DestroyImmediate(part.gameObject);
            }

            // 단계 칩(64.19×32, 코랄) + 장소(Jua 18, 칩과 10 간격)
            RectTransform header = Row(card, "Header", 10);
            Height(header, 32);
            RectTransform chip = Box(header, "StepChip", 64.19f, 32);
            Art(chip, "QuestChip", 64.19f + 2 * ArtPad, 32 + 2 * ArtPad, ArtPad, ArtPad);
            TMP_Text chipLabel = Text(chip, "Label", DisplayFontPath, 16, Color.white, "1 / 7");
            Fill(chipLabel.rectTransform);
            chipLabel.alignment = TextAlignmentOptions.Center;
            chipLabel.margin = new Vector4(0, 1 * Px, 0, 0);
            TMP_Text place = Text(header, "Place", DisplayFontPath, 18, Muted, "호텔에 도착");
            place.alignment = TextAlignmentOptions.MidlineLeft;
            place.margin = new Vector4(0, 2 * Px, 0, 0);
            place.overflowMode = TextOverflowModes.Ellipsis;
            BuilderTools.GetOrAddComponent<LayoutElement>(place.gameObject).flexibleWidth = 1;

            // 할 일(Jua 26, 위 12), 상황(Pretendard 16, 위 4)
            var direction = card.Find("Direction").GetComponent<TMP_Text>();
            Style(direction, DisplayFontPath, 26, Ink);
            direction.text = DirectionPlaceholder;
            direction.margin = new Vector4(0, 12 * Px, 0, 0);
            direction.textWrappingMode = TextWrappingModes.Normal;
            direction.alignment = TextAlignmentOptions.MidlineLeft;
            // 시안 줄 높이(글자 크기의 1.2배)를 지켜 아래 줄들이 시안 자리에 온다.
            LineBox(direction, 12, 26 * 1.2f);
            var situation = card.Find("Situation").GetComponent<TMP_Text>();
            Style(situation, BodyFontPath, 16, Muted);
            situation.margin = new Vector4(0, 4 * Px, 0, 0);
            situation.textWrappingMode = TextWrappingModes.Normal;
            situation.alignment = TextAlignmentOptions.MidlineLeft;
            LineBox(situation, 4, 19);

            // 진행 칸(46.86×8, 칸 사이 6, 위 14). 칸 그림은 빛 번짐 자리만큼 겹쳐 놓는다.
            RectTransform progress = Rect("Progress", card);
            Height(progress, 14 + 8);
            RectTransform marks = Rect("Marks", progress);
            marks.anchorMin = new Vector2(0, 0);
            marks.anchorMax = new Vector2(1, 0);
            marks.pivot = new Vector2(0, 0);
            marks.anchoredPosition = Vector2.zero;
            marks.sizeDelta = new Vector2(0, 8 * Px);
            var bar = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(marks.gameObject);
            bar.spacing = (6 - 2 * ArtPad) * Px;
            bar.padding = new RectOffset(-Round(ArtPad), 0, 0, 0);
            bar.childAlignment = TextAnchor.MiddleLeft;
            bar.childControlWidth = bar.childControlHeight = false;
            bar.childForceExpandWidth = bar.childForceExpandHeight = false;
            for (int i = 0; i < 7; i++)
            {
                RectTransform mark = Rect("Mark" + i, marks);
                mark.sizeDelta = new Vector2((46.86f + 2 * ArtPad) * Px, (8 + 2 * ArtPad) * Px);
                Picture(
                    mark.gameObject,
                    i == 0 ? Sprite("QuestStepCurrent") : Sprite("QuestStepUpcoming")
                );
            }

            // 목표 줄(위 14, 줄 높이 24, 줄 사이 9): 동그라미 체크(24) + 이름(Pretendard 17, 체크와 10 간격)
            RectTransform goals = Rect("Goals", card);
            var list = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(goals.gameObject);
            list.padding = new RectOffset(0, 0, Round(14), 0);
            list.spacing = 9 * Px;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            RectTransform goal = Row(goals, "Goal", 10);
            Height(goal, 24);
            RectTransform check = Box(goal, "Check", 24, 24);
            Art(check, "CheckEmpty", 24 + 2 * ArtPad, 28 + 2 * ArtPad, ArtPad, ArtPad);
            TMP_Text goalLabel = Text(goal, "Label", BodyFontPath, 17, Ink, "예약 확인");
            goalLabel.alignment = TextAlignmentOptions.MidlineLeft;
            BuilderTools.GetOrAddComponent<LayoutElement>(goalLabel.gameObject).flexibleWidth = 1;

            string[] order = { "Header", "Direction", "Situation", "Progress", "Goals" };
            for (int i = 0; i < order.Length; i++)
                card.Find(order[i]).SetSiblingIndex(i);
            BuilderTools.SetReference(view, "stepChip", chipLabel);
            BuilderTools.SetReference(view, "place", place);
            BuilderTools.SetReference(view, "progress", marks);
            BuilderTools.SetReference(view, "goals", goals);
            BuilderTools.SetReference(view, "doneMark", Sprite("QuestStepDone"));
            BuilderTools.SetReference(view, "currentMark", Sprite("QuestStepCurrent"));
            BuilderTools.SetReference(view, "upcomingMark", Sprite("QuestStepUpcoming"));
            BuilderTools.SetReference(view, "goalOpen", Sprite("CheckEmpty"));
            BuilderTools.SetReference(view, "goalDone", Sprite("CheckDone"));
        }

        // 하는 퀘스트가 없을 때(시작 전, 끝낸 뒤) 카드 대신 두는 퀘스트 아이콘: 카드 자리(왼쪽·위 28)에 시계·코인 칩과 같은 유리 알약(높이 64),
        // 안에 퀘스트 목록 머리줄의 지도 앱 아이콘(44)과 '퀘스트'(Jua 21). 누르면 퀘스트 목록을 연다. 처음에는 숨겨 두고 카드가 정한다.
        private static void QuestIcon(FirstDayMissionView view, Transform content)
        {
            Transform old = content.Find("QuestIcon");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            RectTransform chip = Chip(content, "QuestIcon");
            Object.DestroyImmediate(chip.GetComponent<LayoutElement>());
            RectTransform row = Row(chip, "Row", 10);
            Fill(row);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(
                Round(10),
                Round(22),
                0,
                0
            );
            // 머리줄 아이콘 그림(60에 그림자 자리 왼쪽·오른쪽 14, 위 8, 아래 22)을 44로 줄인다.
            const float IconSize = 44;
            const float IconScale = IconSize / 60f;
            RectTransform icon = Box(row, "Icon", IconSize, IconSize);
            RectTransform art = Rect("Art", icon);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-14, 8) * IconScale * Px;
            art.sizeDelta = new Vector2(60 + 28, 60 + 30) * IconScale * Px;
            Picture(art.gameObject, AssetDatabase.LoadAssetAtPath<Sprite>(QuestIconArtPath));
            TMP_Text label = Text(row, "Label", DisplayFontPath, 21, Ink, "퀘스트");
            label.margin = new Vector4(0, 2 * Px, 0, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(0, 1);
            chip.anchoredPosition = new Vector2(28, -28) * Px;
            float width = 10 + IconSize + 10 + label.GetPreferredValues("퀘스트").x / Px + 22;
            chip.sizeDelta = new Vector2(width, 64) * Px;
            Image glass = chip.Find("Glass").GetComponent<Image>();
            glass.raycastTarget = true;
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = glass;
            QuestBoardBuilder.Listen(button, view.OpenQuestBoard);
            BuilderTools.SetReference(view, "questIcon", button);
            chip.gameObject.SetActive(false);
        }

        // 시안 .tr: 오른쪽·위 28, 칸 사이 12. 휴대폰 버튼(64) 왼쪽에 시계 칩과 코인 칩(둘 다 174×64)
        private static void StatusChips(FirstDayMissionView view, Transform content)
        {
            Transform old = content.Find("Status");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            RectTransform status = Row(content, "Status", 12);
            status.anchorMin = status.anchorMax = status.pivot = Vector2.one;
            status.anchoredPosition = new Vector2(-(28 + 64 + 12) * Px, -28 * Px);
            status.sizeDelta = new Vector2(0, 64 * Px);
            status.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(status.gameObject).horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // 시계: Jua 21(위 2). 옆 코인 숫자와 같은 크기다.
            RectTransform clockChip = Chip(status, "Clock");
            TMP_Text clock = Text(
                clockChip,
                "Label",
                DisplayFontPath,
                21,
                Ink,
                "1일차 · 오전 8:00"
            );
            Fill(clock.rectTransform);
            clock.alignment = TextAlignmentOptions.Center;
            clock.margin = new Vector4(0, 2 * Px, 0, 0);

            // 코인: 코인 그림(30)은 숫자 크기에 맞추고, 숫자는 옆 시계와 같은 Jua 21(위 2), 단위 Jua 18(위 2), 칸 사이 10. 칩 가운데에 모은다.
            RectTransform coinChip = Chip(status, "Coins");
            RectTransform row = Row(coinChip, "Row", 10);
            Fill(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            // 코인 그림(42에 그림자 자리 12)을 30 크기로 줄여 그대로 비율을 지킨다.
            const float CoinSize = 30;
            const float CoinScale = CoinSize / 42f;
            RectTransform coin = Box(row, "Coin", CoinSize, CoinSize);
            Art(
                coin,
                "Coin",
                (42 + 2 * ArtPad) * CoinScale,
                (48 + 2 * ArtPad) * CoinScale,
                ArtPad * CoinScale,
                ArtPad * CoinScale
            );
            TMP_Text amount = Text(row, "Amount", DisplayFontPath, 21, Ink, "0");
            amount.margin = new Vector4(0, 2 * Px, 0, 0);
            amount.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text unit = Text(row, "Unit", DisplayFontPath, 18, Muted, "코인");
            unit.margin = new Vector4(0, 2 * Px, 0, 0);
            unit.alignment = TextAlignmentOptions.MidlineLeft;
            BuilderTools.SetReference(view, "clock", clock);
            BuilderTools.SetReference(view, "coins", amount);
            BuilderTools.SetReference(view, "statusBar", status.gameObject);
            CoinChangeLabels(view, coinChip);
        }

        // 코인이 바뀌면 칩 아래 흰 알약에 산 것·보상 안내를, 알약 왼쪽에 떠오르는 +10·−8(Jua 40, 흰 테두리)을 보인다(시안 f1·f3, 높이 46).
        // 처음에는 숨겨 두고 FirstDayMissionView가 코인이 바뀔 때 잠깐 켠다.
        private static void CoinChangeLabels(FirstDayMissionView view, RectTransform coinChip)
        {
            foreach (string old in new[] { "Float", "Receipt" })
            {
                Transform part = coinChip.Find(old);
                if (part != null)
                    Object.DestroyImmediate(part.gameObject);
            }

            RectTransform receipt = Rect("Receipt", coinChip);
            receipt.anchorMin = receipt.anchorMax = new Vector2(1, 0);
            receipt.pivot = Vector2.one;
            receipt.anchoredPosition = new Vector2(0, -10) * Px;
            receipt.sizeDelta = new Vector2(0, 46 * Px);
            BuilderTools.GetOrAddComponent<LayoutElement>(receipt.gameObject).ignoreLayout = true;
            var pill = BuilderTools.GetOrAddComponent<Image>(receipt.gameObject);
            // 큰 지도와 같은 흰 둥근 판 그림(모서리 44)을 알약 높이의 절반 모서리로 줄여 쓴다.
            pill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePanelPath);
            pill.type = UnityEngine.UI.Image.Type.Sliced;
            pill.pixelsPerUnitMultiplier = 44f / (23 * Px);
            pill.color = new Color(1, 1, 1, .92f);
            pill.raycastTarget = false;
            var row = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(receipt.gameObject);
            row.padding = new RectOffset(Round(16), Round(18), 0, 0);
            row.spacing = 8 * Px;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(receipt.gameObject).horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            const float CoinSize = 24;
            const float CoinScale = CoinSize / 42f;
            RectTransform coin = Box(receipt, "Coin", CoinSize, CoinSize);
            Art(
                coin,
                "Coin",
                (42 + 2 * ArtPad) * CoinScale,
                (48 + 2 * ArtPad) * CoinScale,
                ArtPad * CoinScale,
                ArtPad * CoinScale
            );
            TMP_Text label = Text(receipt, "Label", DisplayFontPath, 19, Ink, "우산 −8코인");
            label.richText = true;
            label.margin = new Vector4(0, 2 * Px, 0, 0);

            // 떠오르는 글자는 알약 왼쪽에 붙여, 알약이 글자 길이만큼 넓어져도 겹치지 않게 한다.
            TMP_Text amount = Text(receipt, "Float", DisplayFontPath, 40, Ink, "+10");
            amount.fontSharedMaterial = CoinFloatMaterial();
            amount.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform floatRect = amount.rectTransform;
            floatRect.anchorMin = floatRect.anchorMax = new Vector2(0, .5f);
            floatRect.pivot = new Vector2(1, .5f);
            floatRect.anchoredPosition = new Vector2(-8, 0) * Px;
            floatRect.sizeDelta = new Vector2(100, 46) * Px;
            BuilderTools.GetOrAddComponent<LayoutElement>(amount.gameObject).ignoreLayout = true;
            amount.gameObject.SetActive(false);
            receipt.gameObject.SetActive(false);

            BuilderTools.SetReference(view, "coinFloat", amount);
            BuilderTools.SetReference(view, "coinReceipt", label);
            BuilderTools.SetReference(view, "coinChip", coinChip);
        }

        // 떠오르는 +10·−8은 장면 위에서도 읽히게 흰 테두리를 두른 Jua 재질로 그린다.
        private static Material CoinFloatMaterial()
        {
            TMP_FontAsset font = Font(DisplayFontPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CoinFloatMaterialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "Jua Coin Float" };
                AssetDatabase.CreateAsset(material, CoinFloatMaterialPath);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, .28f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, .15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 양 끝이 둥근 유리 알약(174×64). 유리 그림의 그림자 여백만큼 바탕을 넓혀, 9칸 나누기 모서리가 줄지 않고 완전한 반원이 되게 한다.
        private static RectTransform Chip(Transform parent, string name)
        {
            RectTransform chip = Box(parent, name, 174, 64);
            UiThemeDefinition design = Design();
            const float Radius = 26;
            const float Margin = 8;
            float scale = Radius / (32 * Px);
            RectTransform glass = Rect("Glass", chip);
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = -Vector2.one * (Margin / scale);
            glass.offsetMax = Vector2.one * (Margin / scale);
            glass.SetAsFirstSibling();
            var image = BuilderTools.GetOrAddComponent<Image>(glass.gameObject);
            image.sprite = design.InputSprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = scale;
            image.material = design.GlassMaterial;
            image.color = design.CardTint;
            image.raycastTarget = false;
            return chip;
        }

        private static RectTransform Row(Transform parent, string name, float spacing)
        {
            RectTransform row = Rect(name, parent);
            var layout = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            layout.spacing = spacing * Px;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        // 레이아웃에서 정해진 크기를 갖는 칸
        private static RectTransform Box(Transform parent, string name, float width, float height)
        {
            RectTransform box = Rect(name, parent);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(box.gameObject);
            size.minWidth = size.preferredWidth = width * Px;
            size.minHeight = size.preferredHeight = height * Px;
            size.flexibleWidth = 0;
            return box;
        }

        // 시안 줄 상자(위 여백 + 줄 높이)를 최소 높이로 둔다. 여러 줄이 되면 글자 높이만큼 늘어난다.
        private static void LineBox(TMP_Text text, float top, float lineHeight)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(text.gameObject);
            size.minHeight = (top + lineHeight) * Px;
        }

        private static void Height(RectTransform target, float height)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject);
            size.minHeight = size.preferredHeight = height * Px;
        }

        // 칸 왼쪽 위에서 (-left, +top)만큼 바깥으로 넓힌 자리에 렌더링한 그림을 깐다.
        private static void Art(
            RectTransform box,
            string sprite,
            float width,
            float height,
            float left,
            float top
        )
        {
            RectTransform art = Rect("Art", box);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-left * Px, top * Px);
            art.sizeDelta = new Vector2(width * Px, height * Px);
            art.SetAsFirstSibling();
            Picture(art.gameObject, Sprite(sprite));
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
        }

        private static void Picture(GameObject target, Sprite sprite)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(target);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.material = null;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string fontPath,
            float size,
            Color color,
            string value
        )
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                parent,
                name,
                Font(fontPath),
                size * Px,
                color,
                value
            );
            Style(text, fontPath, size, color);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        // 테마가 글꼴을 바꾸지 않게 역할을 떼고 시안 글꼴·크기·색으로 쓴다.
        private static void Style(TMP_Text text, string fontPath, float size, Color color)
        {
            var role = text.GetComponent<ThemedElement>();
            if (role != null)
                Object.DestroyImmediate(role);
            text.font = Font(fontPath);
            text.fontSharedMaterial = text.font.material;
            text.fontSize = size * Px;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false;
            text.color = color;
            text.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static int Round(float px) => Mathf.RoundToInt(px * Px);

        private static TMP_FontAsset Font(string path) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        private static UiThemeDefinition Design() =>
            AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath).Themes[0];

        // 렌더링한 그림은 크기를 줄이지 않고 그대로 가져온다.
        private static Sprite Sprite(string name)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Theme(Transform target, UiThemeRole role)
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }
    }
}
