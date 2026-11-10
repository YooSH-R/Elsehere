using A305.Api;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Learning;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// HUD의 [이웃] 창(마을 NPC 목록)을 [피드백 목록](<see cref="FeedbackListBuilder"/>)과 같은 판·자리로 배치한다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 머리줄에 [이웃] 아이콘·제목·열린 수·닫기, 그 아래 NPC 4줄(흰 유리 칸에 색 아이콘·이름과 작은 역할·"장소 · 대화 N번 · 아는 정도 N"·오른쪽 호감도),
    /// 아래 가운데에 쪽 넘김을 둔다. 열린 이웃을 누르면 줄 자리에 정보 카드가 뜬다. 받지 못했을 때의 [다시 시도]는 줄 자리 가운데 안내 아래에 둔다.
    /// 여는 버튼은 휴대폰의 [복습장] 버튼 오른쪽 빈칸에 둔 작은 아이콘 버튼이다.
    /// </summary>
    public static class NeighborsListBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Neighbors List";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string IconPath = "Assets/Art/UI/Phone/PhoneAppNeighbors.png";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";
        private const string LifeDataPath = "Assets/Data/Npc/NpcLife.json";

        private static readonly Vector2 SheetSize = new Vector2(1080, 620);

        // [피드백 목록]과 같은 줄 자리
        private const float RowsTop = 126;
        private const float RowHeight = 84;
        private const float RowStep = 98;
        private const float RowsLeft = 37;
        private const float RowWidth = 1006;
        private const float PagerTop = 535;

        // 휴대폰 [복습장] 버튼(ReviewWorkbookBuilder, 시안 px)의 오른쪽 빈칸. 앱 칸의 오른쪽 끝(282 + 110)을 넘지 않는다.
        private const float ReviewRight = 98 + 234;
        private const float ReviewTop = 655;
        private const float ReviewHeight = 54;
        private const float PhoneButtonGap = 8;
        private const float PhoneButtonWidth = 52;

        private static readonly string[] AvatarSprites =
        {
            "HistoryAvatarSky",
            "HistoryAvatarGreen",
            "HistoryAvatarYellow",
            "HistoryAvatarCoral",
        };

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(HudPrefabPath, root => Apply(root.transform));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// HUD에 [이웃] 창과 휴대폰의 [이웃] 버튼을 만든다. 있으면 다시 배치만 한다. 테마는 UiThemeBuilder가 입힌다(Tools → A305 → UI → Add Neighbors App).
        /// </summary>
        public static void Add(Transform hud)
        {
            var view = BuilderTools.GetOrAddComponent<NeighborsView>(hud.gameObject);
            BuilderTools.SetReference(
                view,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(ApiSettingsPath, MenuPath)
            );
            BuilderTools.SetReference(
                view,
                "lifeData",
                BuilderTools.LoadRequiredAsset<TextAsset>(LifeDataPath, MenuPath)
            );
            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                hud,
                "Neighbors",
                new Vector2(.5f, .5f),
                760f,
                Vector2.zero
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            UiBuilderTools.FindOrCreateButton(
                panel,
                "RetryButton",
                NeighborsText.Retry,
                bold,
                200f
            );
            Card(panel, bold);
            PhoneButton((RectTransform)hud.Find("Panel/Menu"), view);
            Apply(hud);
            panel.gameObject.SetActive(false);
        }

        /// <param name="hud">[이웃] 창(Neighbors)과 <see cref="NeighborsView"/>가 있는 HUD</param>
        public static void Apply(Transform hud)
        {
            var view = hud.GetComponent<NeighborsView>();
            var panel = (RectTransform)hud.Find("Neighbors");
            if (view == null || panel == null)
                return;

            Sheet(panel);
            Shade(hud, panel, view);
            Header(panel, view);
            Pager(panel, view);
            Rows(panel, view);
            Messages(panel, view);
            CardLayout(panel, view);
            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.RequireReferences(view);
        }

        // [복습장] 오른쪽 빈칸의 작은 버튼: 휴대폰 앱처럼 [이웃] 아이콘 아래에 이름을 둔다. 누르는 자리는 버튼 전체다.
        private static void PhoneButton(RectTransform phone, NeighborsView view)
        {
            Transform button = FindOrNewButton(phone, "NeighborsButton");
            var layout = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            layout.ignoreLayout = true;
            F.TopLeft(
                (RectTransform)button,
                ReviewRight + PhoneButtonGap,
                ReviewTop,
                PhoneButtonWidth,
                ReviewHeight
            );
            Image hit = button.GetComponent<Image>();
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;

            RectTransform icon = F.Rect("Icon", button);
            F.TopLeft(icon, 7, 0, 38, 38);
            Image iconImage = F.Picture(
                icon.gameObject,
                AssetDatabase.LoadAssetAtPath<Sprite>(IconPath)
            );
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            TMP_Text label = Label(button, "Label", F.DisplayFontPath, 15, F.Ink);
            label.text = NeighborsText.AppLabel;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            F.TopLeft(label.rectTransform, 0, 38, PhoneButtonWidth, 16);
            BuilderTools.SetReference(view, "openButton", button.GetComponent<Button>());
        }

        // 판: 화면 가운데 1080×620. 테마 판이 붙인 세로 배치는 지우고 자리를 직접 정한다.
        private static void Sheet(RectTransform panel)
        {
            foreach (var layout in panel.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            foreach (var fitter in panel.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = SheetSize * F.Px;
        }

        // [피드백 목록]과 같은 그림자로 뒤 장면을 덮는다. 창 바로 앞 형제라 조작법 카드도 그 아래에 놓인다.
        private static void Shade(Transform hud, RectTransform panel, NeighborsView view)
        {
            RectTransform shade = F.Rect("NeighborsShade", hud);
            int panelIndex = panel.GetSiblingIndex();
            shade.SetSiblingIndex(
                shade.GetSiblingIndex() < panelIndex ? panelIndex - 1 : panelIndex
            );
            F.Fill(shade);
            Image image = F.Picture(shade.gameObject, F.Sprite("HistoryShade"));
            image.raycastTarget = true;
            shade.gameObject.SetActive(panel.gameObject.activeSelf);
            BuilderTools.SetReference(view, "shade", shade.gameObject);
        }

        // 머리줄(높이 104): [이웃] 아이콘(60), 제목(Jua 36), 열린 수(Jua 22), 오른쪽 닫기(52). [피드백 목록]과 같은 자리다.
        private static void Header(RectTransform panel, NeighborsView view)
        {
            // 앱 아이콘 그림은 아이콘(240px) 둘레에 사방 24px 그림자 자리가 있어, 60으로 줄이면 사방 6이다.
            RectTransform icon = F.Rect("AppIcon", panel);
            F.TopLeft(icon, 37 - 6, 23 - 6, 72, 75);
            F.Picture(icon.gameObject, AssetDatabase.LoadAssetAtPath<Sprite>(IconPath));
            TMP_Text title = Label(panel, "Title", F.DisplayFontPath, 36, F.Ink);
            title.text = NeighborsText.Title;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.margin = new Vector4(0, 3 * F.Px, 0, 0);
            F.TopLeft(title.rectTransform, 113, 33.5f, 200, 39);
            TMP_Text count = Label(panel, "Count", F.DisplayFontPath, 22, F.Muted);
            count.alignment = TextAlignmentOptions.MidlineLeft;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.margin = new Vector4(0, 3 * F.Px, 0, 0);
            F.TopLeft(count.rectTransform, 200, 37, 300, 33);
            BuilderTools.SetReference(view, "countText", count);
            Transform close = FindOrNewButton(panel, "CloseButton");
            F.ArtButton(close, "FeedbackClose", 52, 52, 14, 12, 80, 84);
            var closeRect = (RectTransform)close;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-35, -27) * F.Px;
            BuilderTools.SetReference(view, "closeButton", close.GetComponent<Button>());
        }

        // 아래 가운데 쪽 넘김: [<](50) 쪽 번호(Jua 19) [>](50)
        private static void Pager(RectTransform panel, NeighborsView view)
        {
            RectTransform pager = F.Rect("Pager", panel);
            F.TopLeft(pager, (SheetSize.x - 163.44f) / 2, PagerTop, 163.44f, 50);
            Transform previous = FindOrNewButton(pager, "PreviousButton");
            F.ArtButton(previous, "FeedbackPrevious", 50, 50, 22, 12, 94, 96);
            F.TopLeft((RectTransform)previous, 0, 0, 50, 50);
            TMP_Text number = Label(pager, "PageNumber", F.DisplayFontPath, 19, F.Muted);
            number.alignment = TextAlignmentOptions.Midline;
            number.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(number.rectTransform, 50, 0, 63.44f, 50);
            // 양옆 둥근 유리 버튼(두께 4px)의 화살표가 두께 절반만큼 내려가 있어 같은 높이로 맞춘다.
            number.rectTransform.anchoredPosition -= new Vector2(0, 2 * F.Px);
            Transform next = FindOrNewButton(pager, "NextButton");
            F.ArtButton(next, "FeedbackNext", 50, 50, 22, 12, 94, 96);
            F.TopLeft((RectTransform)next, 113.44f, 0, 50, 50);
            BuilderTools.SetReference(view, "pager", pager.gameObject);
            BuilderTools.SetReference(view, "previousButton", previous.GetComponent<Button>());
            BuilderTools.SetReference(view, "nextButton", next.GetComponent<Button>());
            BuilderTools.SetReference(view, "pageText", number);
        }

        // NPC 줄 4개. 한 쪽에 보일 만큼만 두고 NeighborsView가 쪽마다 채운다.
        private static void Rows(RectTransform panel, NeighborsView view)
        {
            RectTransform rows = F.Rect("Rows", panel);
            F.TopLeft(rows, RowsLeft, RowsTop, RowWidth, RowStep * 3 + RowHeight);
            var shown = new Object[NeighborsBook.PageSize];
            for (int slot = 0; slot < shown.Length; slot++)
                shown[slot] = Row(rows, slot);
            SetList(view, "rows", shown);
            BuilderTools.SetReference(view, "rowsGroup", rows.gameObject);
            var avatars = new Object[AvatarSprites.Length];
            for (int i = 0; i < avatars.Length; i++)
                avatars[i] = F.Sprite(AvatarSprites[i]);
            SetList(view, "avatarSprites", avatars);
        }

        // 한 줄(1006×84): 흰 유리 칸, 색 아이콘(52)과 이름 첫 글자, 이름과 작은 역할(Jua 24), "장소 · 대화 N번 · 아는 정도 N"(16), 오른쪽 호감도(Jua 26).
        // 줄 전체가 버튼이라 열린 이웃을 누르면 정보 카드가 뜨고, 잠긴 NPC 줄은 흐리게 그린다.
        private static NeighborRow Row(RectTransform rows, int slot)
        {
            RectTransform row = F.Rect("Row" + slot, rows);
            F.TopLeft(row, 0, RowStep * slot, RowWidth, RowHeight);
            var component = BuilderTools.GetOrAddComponent<NeighborRow>(row.gameObject);
            RectTransform tile = F.Rect("Tile", row);
            F.Spread(tile, 16, 12, 16, 32);
            Image tileImage = F.Picture(tile.gameObject, null);
            F.Sliced(tileImage, F.Sprite("FeedbackTile", F.TileBorder));
            tileImage.raycastTarget = true;
            var button = BuilderTools.GetOrAddComponent<Button>(row.gameObject);
            button.targetGraphic = tileImage;
            var group = BuilderTools.GetOrAddComponent<CanvasGroup>(row.gameObject);
            // 아이콘 그림은 그림자 자리(왼 14·위 8)만큼 넓고, 이름 글자는 그 안의 52 칸 가운데에 둔다.
            RectTransform avatar = F.Rect("Avatar", row);
            F.TopLeft(avatar, 19 - 14, 16 - 8, 80, 82);
            Image avatarImage = F.Picture(avatar.gameObject, F.Sprite(AvatarSprites[0]));
            avatarImage.raycastTarget = false;
            TMP_Text letter = Label(avatar, "Letter", F.DisplayFontPath, 22, Color.white);
            letter.alignment = TextAlignmentOptions.Midline;
            letter.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(letter.rectTransform, 14, 8, 52, 52);
            TMP_Text name = Label(row, "Name", F.DisplayFontPath, 24, F.Ink);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            // 칸 높이가 Jua 줄 높이보다 낮아 말줄임이면 뒤가 잘리므로, 넘쳐도 그대로 그린다.
            name.overflowMode = TextOverflowModes.Overflow;
            F.TopLeft(name.rectTransform, 87, 15.3f, 720, 26.39f);
            TMP_Text detail = Label(row, "Detail", F.BodyFontPath, 16, F.Muted);
            detail.alignment = TextAlignmentOptions.MidlineLeft;
            detail.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(detail.rectTransform, 87, 49.69f, 720, 19);
            TMP_Text affinity = Label(row, "Affinity", F.DisplayFontPath, 26, F.Ink);
            affinity.alignment = TextAlignmentOptions.MidlineRight;
            affinity.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(affinity.rectTransform, 823, 22, 160, 40);
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                text.raycastTarget = false;
            BuilderTools.SetReference(component, "button", button);
            BuilderTools.SetReference(component, "group", group);
            BuilderTools.SetReference(component, "avatar", avatarImage);
            BuilderTools.SetReference(component, "letter", letter);
            BuilderTools.SetReference(component, "nameText", name);
            BuilderTools.SetReference(component, "detailText", detail);
            BuilderTools.SetReference(component, "affinityText", affinity);
            return component;
        }

        // 줄이 없을 때 줄 자리 가운데의 안내(불러오는 중·없음·실패)와 그 아래 [다시 시도], 줄이 있을 때 마지막 줄과 쪽 넘김 사이의 안내
        private static void Messages(RectTransform panel, NeighborsView view)
        {
            TMP_Text empty = Label(panel, "Empty", F.BodyFontPath, 19, F.Muted);
            empty.alignment = TextAlignmentOptions.Midline;
            empty.textWrappingMode = TextWrappingModes.Normal;
            F.TopLeft(empty.rectTransform, RowsLeft, RowsTop, RowWidth, RowStep * 3 + RowHeight);
            TMP_Text status = Label(panel, "Status", F.BodyFontPath, 16, F.Muted);
            status.alignment = TextAlignmentOptions.Midline;
            status.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(
                status.rectTransform,
                RowsLeft,
                RowsTop + RowStep * 3 + RowHeight,
                RowWidth,
                30
            );
            Transform retry = panel.Find("RetryButton");
            if (retry != null)
                F.TopLeft((RectTransform)retry, (SheetSize.x - 200) / 2, 380, 200, 56);
            BuilderTools.SetReference(view, "emptyText", empty);
            BuilderTools.SetReference(view, "statusText", status);
            if (retry != null)
                BuilderTools.SetReference(view, "retryButton", retry.GetComponent<Button>());
        }

        // 정보 카드: 줄 자리와 쪽 넘김 자리를 함께 쓰는 흰 유리 칸. 이름과 작은 역할(Jua 30), 한 줄씩 쓴 정보(16), 아래 가운데 [목록으로]
        private static void Card(RectTransform panel, TMP_FontAsset bold)
        {
            RectTransform card = F.Rect("Card", panel);
            UiBuilderTools.FindOrCreateButton(card, "BackButton", NeighborsText.Back, bold, 200f);
            card.gameObject.SetActive(false);
        }

        private static void CardLayout(RectTransform panel, NeighborsView view)
        {
            var card = (RectTransform)panel.Find("Card");
            if (card == null)
                return;

            F.TopLeft(card, RowsLeft, RowsTop, RowWidth, PagerTop + 50 - RowsTop);
            RectTransform tile = F.Rect("Tile", card);
            tile.SetAsFirstSibling();
            F.Spread(tile, 16, 12, 16, 32);
            F.Sliced(F.Picture(tile.gameObject, null), F.Sprite("FeedbackTile", F.TileBorder));
            TMP_Text name = Label(card, "Name", F.DisplayFontPath, 30, F.Ink);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Overflow;
            F.TopLeft(name.rectTransform, 48, 36, 900, 36);
            TMP_Text facts = Label(card, "Facts", F.BodyFontPath, 18, F.Ink);
            facts.alignment = TextAlignmentOptions.TopLeft;
            facts.textWrappingMode = TextWrappingModes.Normal;
            facts.lineSpacing = 18;
            F.TopLeft(facts.rectTransform, 48, 88, 900, 250);
            // 아래 줄 가운데를 기준으로 왼쪽에 [지도에서 보기](또는 그 대신 이유), 오른쪽에 [목록으로]를 둔다.
            Transform back = card.Find("BackButton");
            if (back != null)
            {
                var backLayout = BuilderTools.GetOrAddComponent<LayoutElement>(back.gameObject);
                backLayout.ignoreLayout = true;
                F.TopLeft((RectTransform)back, RowWidth / 2 + ButtonGap / 2, 350, 200, 56);
                BuilderTools.SetReference(view, "backButton", back.GetComponent<Button>());
            }
            Button map = UiBuilderTools.FindOrCreateButton(
                card,
                "MapButton",
                NeighborsText.ShowOnMap,
                UiBuilderTools.BoldFont(),
                MapButtonWidth
            );
            BuilderTools.GetOrAddComponent<LayoutElement>(map.gameObject).ignoreLayout = true;
            float mapLeft = RowWidth / 2 - ButtonGap / 2 - MapButtonWidth;
            F.TopLeft((RectTransform)map.transform, mapLeft, 350, MapButtonWidth, 56);
            TMP_Text hint = Label(card, "MapHint", F.BodyFontPath, 17, F.Ink);
            hint.alignment = TextAlignmentOptions.MidlineRight;
            hint.textWrappingMode = TextWrappingModes.Normal;
            F.TopLeft(hint.rectTransform, 48, 350, RowWidth / 2 - ButtonGap / 2 - 48, 56);
            hint.gameObject.SetActive(false);
            BuilderTools.SetReference(view, "card", card.gameObject);
            BuilderTools.SetReference(view, "cardNameText", name);
            BuilderTools.SetReference(view, "cardFactsText", facts);
            BuilderTools.SetReference(view, "mapButton", map);
            BuilderTools.SetReference(view, "mapHintText", hint);
        }

        // 정보 카드 아래 줄 버튼 사이 간격과 [지도에서 보기] 폭
        private const float ButtonGap = 24;
        private const float MapButtonWidth = 240;

        private static Transform FindOrNewButton(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            return button.transform;
        }

        private static TMP_Text Label(
            Transform parent,
            string name,
            string fontPath,
            float size,
            Color color
        )
        {
            Transform existing = parent.Find(name);
            TMP_Text text =
                existing != null
                    ? existing.GetComponent<TMP_Text>()
                    : new GameObject(
                        name,
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    ).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false);
            F.Style(text, fontPath, size, color);
            text.richText = true;
            text.margin = Vector4.zero;
            return text;
        }

        private static void SetList(Object owner, string propertyName, Object[] values)
        {
            var fields = new SerializedObject(owner);
            SerializedProperty list = fields.FindProperty(propertyName);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
