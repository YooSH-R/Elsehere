using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// HUD의 [피드백 목록] 창을 확정 시안(m3를 다듬은 것)대로 배치한다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 판(1080×620)은 테마 유리 그대로 두고, 머리줄에 앱 아이콘·제목·닫기만, 그 아래 대화 4줄(흰 유리 칸에 색 아이콘·"NPC · 장소"·시간·[보기]),
    /// 아래 가운데에 쪽 넘김을 둔다. 그림은 대화 피드백 화면과 같은 렌더링 그림(Art/UI/Feedback)을 쓴다.
    /// </summary>
    public static class FeedbackListBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Feedback List";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string Title = "피드백 목록";

        private static readonly Vector2 SheetSize = new Vector2(1080, 620);

        // 첫 줄 위치(머리줄 104 + 띄움 22)와 줄 간격(칸 84 + 사이 14)
        private const float RowsTop = 126;
        private const float RowHeight = 84;
        private const float RowStep = 98;
        private const float RowsLeft = 37;
        private const float RowWidth = 1006;
        private const float PagerTop = 535;

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

        /// <param name="hud">[피드백 목록] 창(History)과 <see cref="ConversationHistoryView"/>가 있는 HUD</param>
        public static void Apply(Transform hud)
        {
            var view = hud.GetComponent<ConversationHistoryView>();
            var panel = (RectTransform)hud.Find("History");
            if (view == null || panel == null)
                return;

            Sheet(panel);
            Shade(hud, panel, view);
            Header(panel, view);
            Pager(panel, view);
            Rows(panel, view);
            Messages(panel, view);
            // 예전 글자 목록·설명·버튼 줄은 줄마다 [보기]가 생겨 쓰지 않는다.
            foreach (string old in new[] { "Hint", "List", "Buttons" })
            {
                Transform part = panel.Find(old);
                if (part != null)
                    Object.DestroyImmediate(part.gameObject);
            }
            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.RequireReferences(view);
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

        // 목록 뒤 장면 전체를 시안 그림자(.shade: 가운데 옅고 가장자리 진한 갈색)로 덮는다. 목록 바로 앞 형제라 조작법 카드도 그 아래에 놓인다.
        private static void Shade(Transform hud, RectTransform panel, ConversationHistoryView view)
        {
            RectTransform shade = F.Rect("HistoryShade", hud);
            int panelIndex = panel.GetSiblingIndex();
            // 다시 빌드해도 목록 바로 앞에 남도록, 이미 앞에 있으면 한 칸 앞 자리로 둔다.
            shade.SetSiblingIndex(
                shade.GetSiblingIndex() < panelIndex ? panelIndex - 1 : panelIndex
            );
            F.Fill(shade);
            Image image = F.Picture(shade.gameObject, F.Sprite("HistoryShade"));
            image.raycastTarget = true;
            shade.gameObject.SetActive(panel.gameObject.activeSelf);
            BuilderTools.SetReference(view, "shade", shade.gameObject);
        }

        // 머리줄(높이 104): 앱 아이콘(60), 제목(Jua 36), 오른쪽 닫기(52). 대화 피드백 화면과 같은 자리다.
        private static void Header(RectTransform panel, ConversationHistoryView view)
        {
            RectTransform icon = F.Rect("AppIcon", panel);
            F.TopLeft(icon, 37 - 16, 23 - 12, 92, 94);
            F.Picture(icon.gameObject, F.Sprite("FeedbackIcon"));

            TMP_Text title = Label(panel, "Title", F.DisplayFontPath, 36, F.Ink);
            title.text = Title;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.margin = new Vector4(0, 3 * F.Px, 0, 0);
            F.TopLeft(title.rectTransform, 113, 33.5f, 600, 39);

            Transform close = panel.Find("CloseButton") ?? panel.Find("Buttons/CloseButton");
            close = close != null ? close : NewButton(panel, "CloseButton");
            close.SetParent(panel, false);
            // 닫기 X는 다른 창과 같은 공용 모양으로 둔다(S15P31A305-525).
            UiCloseButton.Corner(close, UiCloseButton.WindowSize);
            BuilderTools.SetReference(view, "closeButton", close.GetComponent<Button>());
        }

        // 아래 가운데 쪽 넘김: [<](50) 쪽 번호(Jua 19) [>](50)
        private static void Pager(RectTransform panel, ConversationHistoryView view)
        {
            RectTransform pager = F.Rect("Pager", panel);
            F.TopLeft(pager, (SheetSize.x - 163.44f) / 2, PagerTop, 163.44f, 50);

            Transform previous = Move(panel, pager, "PreviousButton");
            F.ArtButton(previous, "FeedbackPrevious", 50, 50, 22, 12, 94, 96);
            F.TopLeft((RectTransform)previous, 0, 0, 50, 50);

            TMP_Text number = Label(pager, "PageNumber", F.DisplayFontPath, 19, F.Muted);
            number.alignment = TextAlignmentOptions.Midline;
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.margin = Vector4.zero;
            F.TopLeft(number.rectTransform, 50, 0, 63.44f, 50);
            // 양옆 둥근 유리 버튼(두께 4px)의 화살표가 두께 절반만큼 내려가 있어 같은 높이로 맞춘다.
            number.rectTransform.anchoredPosition -= new Vector2(0, 2 * F.Px);

            Transform next = Move(panel, pager, "NextButton");
            F.ArtButton(next, "FeedbackNext", 50, 50, 22, 12, 94, 96);
            F.TopLeft((RectTransform)next, 113.44f, 0, 50, 50);

            BuilderTools.SetReference(view, "previousButton", previous.GetComponent<Button>());
            BuilderTools.SetReference(view, "nextButton", next.GetComponent<Button>());
            BuilderTools.SetReference(view, "pageText", number);
        }

        // 대화 줄 4개. 한 쪽에 보일 만큼만 두고 ConversationHistoryView가 쪽마다 채운다.
        private static void Rows(RectTransform panel, ConversationHistoryView view)
        {
            RectTransform rows = F.Rect("Rows", panel);
            F.TopLeft(rows, RowsLeft, RowsTop, RowWidth, RowStep * 3 + RowHeight);
            var shown = new Object[ConversationHistoryPages.PageSize];
            for (int slot = 0; slot < shown.Length; slot++)
                shown[slot] = Row(rows, slot);
            SetList(view, "rows", shown);

            var avatars = new Object[AvatarSprites.Length];
            for (int i = 0; i < avatars.Length; i++)
                avatars[i] = F.Sprite(AvatarSprites[i]);
            SetList(view, "avatarSprites", avatars);
        }

        // 한 줄(1006×84): 흰 유리 칸, 색 아이콘(52)과 이름 첫 글자, "NPC · 장소"(Jua 24), 시간(16), 오른쪽 [보기]
        private static ConversationHistoryRow Row(RectTransform rows, int slot)
        {
            RectTransform row = F.Rect("Row" + slot, rows);
            F.TopLeft(row, 0, RowStep * slot, RowWidth, RowHeight);
            var component = BuilderTools.GetOrAddComponent<ConversationHistoryRow>(row.gameObject);

            RectTransform tile = F.Rect("Tile", row);
            F.Spread(tile, 16, 12, 16, 32);
            Image tileImage = F.Picture(tile.gameObject, null);
            F.Sliced(tileImage, F.Sprite("FeedbackTile", F.TileBorder));

            // 아이콘 그림은 그림자 자리(왼 14·위 8)만큼 넓고, 이름 글자는 그 안의 52 칸 가운데에 둔다.
            RectTransform avatar = F.Rect("Avatar", row);
            F.TopLeft(avatar, 19 - 14, 16 - 8, 80, 82);
            Image avatarImage = F.Picture(avatar.gameObject, F.Sprite(AvatarSprites[0]));
            TMP_Text letter = Label(avatar, "Letter", F.DisplayFontPath, 22, Color.white);
            letter.alignment = TextAlignmentOptions.Midline;
            letter.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(letter.rectTransform, 14, 8, 52, 52);

            TMP_Text title = Label(row, "Title", F.DisplayFontPath, 24, F.Ink);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            // 칸 높이(26.39)가 Jua 줄 높이보다 낮아 말줄임이면 이름 뒤가 잘리므로, 넘쳐도 그대로 그린다.
            title.overflowMode = TextOverflowModes.Overflow;
            F.TopLeft(title.rectTransform, 87, 15.3f, 821.72f, 26.39f);

            TMP_Text subtitle = Label(row, "When", F.BodyFontPath, 16, F.Muted);
            subtitle.alignment = TextAlignmentOptions.MidlineLeft;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(subtitle.rectTransform, 87, 49.69f, 821.72f, 19);

            Transform open = row.Find("OpenButton") ?? NewButton(row, "OpenButton");
            F.Clear(open, keepLabel: true);
            F.TopLeft((RectTransform)open, 924.72f, 22, 58.28f, 40);
            F.ArtFace(open, "HistoryOpen", 22, 12, 102.28f, 92);
            TMP_Text openLabel = Label(open, "Label", F.DisplayFontPath, 16, Color.white);
            openLabel.text = "보기";
            openLabel.textWrappingMode = TextWrappingModes.NoWrap;
            F.Lowered(openLabel, 5);

            BuilderTools.SetReference(component, "avatar", avatarImage);
            BuilderTools.SetReference(component, "letter", letter);
            BuilderTools.SetReference(component, "title", title);
            BuilderTools.SetReference(component, "subtitle", subtitle);
            BuilderTools.SetReference(component, "openButton", open.GetComponent<Button>());
            return component;
        }

        // 대화가 없을 때 줄 자리 가운데의 안내, 마지막 줄과 쪽 넘김 사이의 불러오는 중·실패 안내
        private static void Messages(RectTransform panel, ConversationHistoryView view)
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

            BuilderTools.SetReference(view, "emptyText", empty);
            BuilderTools.SetReference(view, "statusText", status);
        }

        private static Transform Move(Transform panel, Transform pager, string name)
        {
            Transform button =
                pager.Find(name) ?? panel.Find("Buttons/" + name) ?? NewButton(pager, name);
            button.SetParent(pager, false);
            return button;
        }

        private static Transform NewButton(Transform parent, string name)
        {
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
