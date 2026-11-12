using System.Linq;
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
    /// HUD의 [대화 노트] 창(피드백 목록과 복습장을 합친 창)을 확정 시안(N2, 배포된 피드백 목록 창 기준)대로 배치한다.
    /// 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다. 판(1080×620)은 테마 유리 그대로 두고, 머리줄에 앱 아이콘·제목·장소 탭(전체·음식·쇼핑·생활 서비스·안전·일상)·닫기,
    /// 그 아래 대화 4줄(흰 유리 칸에 색 아이콘·"NPC · 장소"·시간·[피드백 보기]·[다시 연습]), 아래 가운데에 쪽 넘김을 둔다.
    /// 그림은 대화 피드백 화면과 같은 렌더링 그림(Art/UI/Feedback)을 쓴다.
    /// </summary>
    public static class FeedbackListBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Feedback List";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string Title = "대화 노트";

        // 장소 탭은 ConversationHistoryView의 장소 묶음 차례와 같다. 두 글자 탭은 60, "생활 서비스"는 104로 둔다.
        private static readonly string[] TabNames =
        {
            "전체",
            PlaceGroupText.Name(PlaceGroup.Food),
            PlaceGroupText.Name(PlaceGroup.Shopping),
            PlaceGroupText.Name(PlaceGroup.Services),
            PlaceGroupText.Name(PlaceGroup.Safety),
            PlaceGroupText.Name(PlaceGroup.Daily),
        };
        private static readonly float[] TabWidths = { 60, 60, 60, 104, 60, 60 };

        // 장소 탭(시안 .tabs): 옅은 알약(높이 40, 안쪽 3)에 탭(높이 34)을 붙이고, 고른 탭만 흰 알약(On)이 보인다.
        // 오른쪽 끝은 닫기 앞(1000)에 맞춘다.
        private const float TabsRight = 1000;
        private const float TabsTop = 16;
        private const float TabHeight = 34;
        private const float TabsInset = 3;
        private static readonly float TabsTrackWidth = TabsInset * 2 + TabWidths.Sum();

        // 피드백 화면의 고른 탭 그림은 높이 42 탭용이라 34 탭에서는 그림자 자리와 둥근 끝을 같은 비율로 줄인다.
        private const float TabShrink = TabHeight / 42;

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

        [MenuItem("Tools/A305/Learning/Align Conversation Note Unread Count")]
        public static void AlignUnreadCountToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root =>
                    AlignUnreadCount(
                        root.transform.Find("History/UnreadCount")?.GetComponent<TMP_Text>()
                    )
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 이미 배치한 대화 노트 창에 장소 탭과 그 아래 미확인 수만 다시 적용한다(S15P31A305-823).
        /// 전체 배치는 그림 칸을 새로 만들어 프리팹이 크게 바뀌므로 이 둘만 맞춘다.
        /// </summary>
        [MenuItem("Tools/A305/Learning/Apply Conversation Note Place Tabs")]
        public static void ApplyPlaceTabs()
        {
            BuilderTools.UpdatePrefabIfExists(
                HudPrefabPath,
                root =>
                {
                    var view = root.GetComponent<ConversationHistoryView>();
                    var panel = (RectTransform)root.transform.Find("History");
                    Tabs(panel, view);
                    AlignUnreadCount(panel.Find("UnreadCount")?.GetComponent<TMP_Text>());
                    BuilderTools.RequireReferences(view);
                }
            );
            AssetDatabase.SaveAssets();
        }

        internal static void AlignUnreadCount(TMP_Text unread)
        {
            if (unread == null)
                return;

            // 장소 탭 아래에 별도 줄을 두고 첫 대화 카드와도 여백을 확보한다. 폭과 오른쪽 끝은 장소 탭 묶음과 같다.
            F.Style(unread, F.DisplayFontPath, 17, F.Muted);
            F.TopLeft(
                unread.rectTransform,
                TabsRight - TabsTrackWidth,
                TabsTop + 40 + 16,
                TabsTrackWidth,
                24
            );
            unread.alignment = TextAlignmentOptions.MidlineRight;
            unread.textWrappingMode = TextWrappingModes.NoWrap;
            unread.margin = Vector4.zero;
        }

        /// <param name="hud">[대화 노트] 창(History)과 <see cref="ConversationHistoryView"/>가 있는 HUD</param>
        public static void Apply(Transform hud)
        {
            var view = hud.GetComponent<ConversationHistoryView>();
            var panel = (RectTransform)hud.Find("History");
            if (view == null || panel == null)
                return;

            Sheet(panel);
            Shade(hud, panel, view);
            Header(panel, view);
            Tabs(panel, view);
            AlignUnreadCount(panel.Find("UnreadCount")?.GetComponent<TMP_Text>());
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

            Transform close = panel.Find("CloseButton");
            if (close == null)
                close = panel.Find("Buttons/CloseButton");
            if (close == null)
                close = NewButton(panel, "CloseButton");
            close.SetParent(panel, false);
            // 닫기 X는 다른 창과 같은 공용 모양으로 둔다(S15P31A305-525).
            UiCloseButton.Corner(close, UiCloseButton.WindowSize);
            BuilderTools.SetReference(view, "closeButton", close.GetComponent<Button>());
        }

        // 머리줄 오른쪽 장소 탭. 누르면 ConversationHistoryView가 장소를 알리고, 고른 탭의 On 그림을 켠다.
        private static void Tabs(RectTransform panel, ConversationHistoryView view)
        {
            RectTransform track = F.Rect("Tabs", panel);
            F.TopLeft(track, TabsRight - TabsTrackWidth, TabsTop, TabsTrackWidth, 40);
            Image trackImage = F.Picture(track.gameObject, null);
            F.Sliced(trackImage, F.Sprite("FeedbackTabs", new Vector4(72, 72, 72, 72)));
            // 그림 높이 144(반지름 72)를 탭 묶음 높이 40에 맞춰 둥근 끝이 반원이 되게 한다.
            trackImage.pixelsPerUnitMultiplier = 144 / (40 * F.Px);

            var tabs = new Object[TabNames.Length];
            float left = TabsInset;
            for (int i = 0; i < TabNames.Length; i++)
            {
                RectTransform tab = F.Rect("Tab" + i, track);
                F.TopLeft(tab, left, TabsInset, TabWidths[i], TabHeight);
                left += TabWidths[i];
                Image hit = BuilderTools.GetOrAddComponent<Image>(tab.gameObject);
                hit.sprite = null;
                hit.color = Color.clear;
                var button = BuilderTools.GetOrAddComponent<Button>(tab.gameObject);
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;

                RectTransform on = F.Rect("On", tab);
                F.Spread(on, 14 * TabShrink, 10 * TabShrink, 14 * TabShrink, 18 * TabShrink);
                Image onImage = F.Picture(on.gameObject, null);
                F.Sliced(onImage, F.Sprite("FeedbackTabOn", new Vector4(84, 94, 84, 74)));
                onImage.pixelsPerUnitMultiplier = F.ArtScale / TabShrink;
                on.gameObject.SetActive(i == 0);

                TMP_Text label = Label(tab, "Label", F.DisplayFontPath, 17, F.Muted);
                label.text = TabNames[i];
                label.textWrappingMode = TextWrappingModes.NoWrap;
                F.Lowered(label, 2);
                tabs[i] = button;
            }
            SetList(view, "tabs", tabs);
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

        // 한 줄(1006×84): 흰 유리 칸, 색 아이콘(52)과 이름 첫 글자, "NPC · 장소"(Jua 24), 시간(16), 오른쪽 [피드백 보기]·[다시 연습]
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

            // [피드백 보기]: 흰 알약(피드백 화면의 고른 탭과 같은 그림), [다시 연습]: 코랄 알약. 둘 다 높이 40이고 사이는 12다.
            Transform open = PillButton(
                row,
                "OpenButton",
                "피드백 보기",
                765,
                112,
                "FeedbackTabOn",
                new Vector4(84, 94, 84, 74),
                new Vector4(14, 10, 14, 18),
                F.Ink,
                3
            );
            Transform practice = PillButton(
                row,
                "PracticeButton",
                "다시 연습",
                889,
                94,
                "HistoryOpen",
                new Vector4(101, 0, 101, 0),
                new Vector4(22, 12, 22, 40),
                Color.white,
                5
            );

            BuilderTools.SetReference(component, "avatar", avatarImage);
            BuilderTools.SetReference(component, "letter", letter);
            BuilderTools.SetReference(component, "title", title);
            BuilderTools.SetReference(component, "subtitle", subtitle);
            BuilderTools.SetReference(component, "openButton", open.GetComponent<Button>());
            BuilderTools.SetReference(component, "practiceButton", practice.GetComponent<Button>());
            return component;
        }

        // 줄 오른쪽 알약 버튼. 버튼 칸(폭×40)은 시안 크기이고, 늘려 쓰는 그림(border: 둥근 끝 자리, 그림 px)은
        // 그림자 자리(pad: 왼·위·오른·아래, 시안 px)만큼 넓혀 깐다.
        private static Transform PillButton(
            Transform row,
            string name,
            string caption,
            float left,
            float width,
            string art,
            Vector4 border,
            Vector4 pad,
            Color labelColor,
            float lip
        )
        {
            Transform button = row.Find(name);
            if (button == null)
                button = NewButton(row, name);
            F.Clear(button, keepLabel: true);
            F.TopLeft((RectTransform)button, left, 22, width, 40);
            F.ArtFace(button, art, pad.x, pad.y, width + pad.x + pad.z, 40 + pad.y + pad.w);
            // ArtFace는 그림을 늘리지 않는 설정으로 가져오므로, 둥근 끝 자리를 다시 정해 폭만 늘린다.
            F.Sliced(button.Find("Art").GetComponent<Image>(), F.Sprite(art, border));
            TMP_Text label = Label(button, "Label", F.DisplayFontPath, 16, labelColor);
            label.text = caption;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            F.Lowered(label, lip);
            return button;
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
            Transform button = pager.Find(name);
            if (button == null)
                button = panel.Find("Buttons/" + name);
            if (button == null)
                button = NewButton(pager, name);
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
