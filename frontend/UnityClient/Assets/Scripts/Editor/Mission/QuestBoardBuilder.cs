using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Mission;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Ui = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트 목록(시안 mock.html #story)과 도장 지도(시안 q2)를 첫날 미션 프리팹의 Content/QuestBoard로 만든다. 자리는 시안 px × 1.2 캔버스 단위이고,
    /// 그림은 시안을 2.4배로 렌더링한 Art/UI/Quest 조각이다. 판은 시안의 흰 유리 판(.panel) 그림이다.
    /// 왼쪽 위 퀘스트 카드를 누르면 수첩 대신 목록을 열고, 목록의 카드를 누르면 그 퀘스트의 도장 지도로 가도록 버튼을 연결한다.
    /// 연계 퀘스트의 잠금을 푸는 연출(띠·자물쇠·흰 막 조각·빛 테두리·[퀘스트 시작])도 목록 위에 만든다.
    /// 머리줄 제목 옆 [이야기 | 장소 연습] 탭과 장소 연습 쪽은 PracticeBoardBuilder가 만든다. 두 탭은 같은 판(시안 #story·#place)을 쓴다.
    /// </summary>
    public static class QuestBoardBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Board";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private const string ArtFolder = "Assets/Art/UI/Quest";
        private const string ClosePath = "Assets/Art/UI/Feedback/FeedbackClose.png";
        private const float Px = Ui.Px;

        // 렌더링한 그림은 2.4배라 캔버스 1단위 = 그림 2px
        private const float ArtPixels = 2.4f;

        // [이야기]·[장소 연습] 탭의 판은 시안 .panel(1520×800, 모서리 46)이다. 판 그림은 그림자 여백(왼 80, 위 50, 오른 80, 아래 110)까지 그렸다.
        // 도장 지도(시안 q2) 판은 1240×770이다.
        internal const float TabSheetWidth = 1520;
        internal const float TabSheetHeight = 800;
        private static readonly (float left, float top, float right, float bottom) PanelPad = (
            80,
            50,
            80,
            110
        );

        // 카드(시안 c4 .cards.small .card 400×420, 모서리 34): 한 쪽에 세 장씩, 판 폭에 사이 60씩 고르게 벌린 자리(왼쪽 100.5·560·1019.5, 위 229)에 둔다.
        // 카드 그림은 그림자 여백(왼 30, 위 20, 오른 30, 아래 40)까지 그렸다. 들르는 곳 칸은 282.5×96(간격 12)이다.
        private const int CardsPerPage = 3;
        private const float CardWidth = 400;
        private const float CardHeight = 420;
        private const float CardTop = 229;
        private static readonly float[] CardLefts = { 100.5f, 560, 1019.5f };
        private static readonly (float left, float top, float right, float bottom) CardPad = (
            30,
            20,
            30,
            40
        );

        // 아이콘 타일(시안 c4 68) 그림은 퀘스트 완료 팡파레와 같은 비율의 여백(52 타일의 14·8·14·22)을 둔다.
        private const float IconPadScale = 68f / 52f;

        // '완료' 도장·자물쇠 그림은 104 크기로 그렸고, 줄인 카드에서는 88로 줄여 쓴다.
        private const float BadgeScale = 88f / 104f;

        // 쪽이 여럿이면 카드 아래 22에 쪽 넘기기를 둔다.
        private const float PagerTop = CardTop + CardHeight + 22;

        // 시안의 반투명 잉크(.58)를 흰 카드에 얹은 색. 선형 색 공간의 UI는 반투명 글자가 더 옅어 보여 불투명으로 쓴다.
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(134, 127, 119, 255);
        private static readonly Color PagerInk = new Color32(125, 110, 91, 255);

        private static readonly string[] Icons = { "Hotel", "Diner", "Post" };

        private static readonly (string Place, string Art)[] Stamps =
        {
            ("호텔", "Hotel"),
            ("카페", "Cafe"),
            ("드럭스토어", "Drugstore"),
            ("거리", "Street"),
            ("햄버거 가게", "Diner"),
            ("빨래방", "Laundry"),
            ("은행", "Bank"),
            ("이발소", "Barber"),
            ("꽃집", "Florist"),
            ("빵집", "Bakery"),
            ("생활용품점", "HomeStore"),
            ("오락실", "Arcade"),
            ("우체국", "PostOffice"),
        };

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            AlignBackArrow();
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/Mission/Align Quest Back Arrow")]
        public static void AlignBackArrow()
        {
            // 머리줄 버튼은 76px이고 그림자 여백은 왼·오른쪽 16, 위 10, 아래 22px다.
            // 글꼴의 기준선 대신 원과 화살표가 같은 도형 중심을 사용하게 한다.
            const int size = 260;
            const float radius = 38 * ArtPixels;
            Vector2 center = new Vector2(54 * ArtPixels, size - 48 * ArtPixels);
            var art = new UiArtCanvas(size, size);
            art.SoftShadow(
                center.x - radius,
                center.y - radius - 9,
                center.x + radius,
                center.y + radius - 9,
                radius,
                22,
                new Color32(82, 61, 37, 36)
            );
            art.Circle(center.x, center.y, radius, new Color32(235, 233, 230, 255));
            art.Circle(center.x, center.y, radius - 1.2f, Color.white);
            art.Polygon(
                Ink,
                center + new Vector2(10.5f, 16.5f),
                center + new Vector2(2.1f, 16.5f),
                center + new Vector2(-10.5f, 0),
                center + new Vector2(2.1f, -16.5f),
                center + new Vector2(10.5f, -16.5f),
                center + new Vector2(-2.1f, 0)
            );
            Texture2D texture = art.ToTexture();
            try
            {
                string path = ArtFolder + "/QuestBackRound.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static void Apply(GameObject root)
        {
            var mission = root.GetComponent<FirstDayMissionView>();
            Transform content = root.transform.Find("Content");
            Transform old = content.Find("QuestBoard");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform board = Ui.Rect("QuestBoard", content);
            Ui.Fill(board);
            board.SetAsLastSibling();
            var view = board.gameObject.AddComponent<QuestBoardView>();
            var fields = new SerializedObject(view);

            // 바깥을 누르면 닫는다. 시안처럼 바깥을 어둡게 칠하지 않는다.
            RectTransform shade = Ui.Rect("Shade", board);
            Ui.Fill(shade);
            shade.gameObject.AddComponent<Image>().color = Color.clear;
            Listen(Plain(shade.gameObject.AddComponent<Button>()), view.Close);

            RectTransform sheet = Ui.Rect("Sheet", board);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.anchoredPosition = Vector2.zero;
            sheet.sizeDelta = new Vector2(TabSheetWidth, TabSheetHeight) * Px;
            // 판 안을 누르면 닫지 않도록 보이지 않는 바탕으로 막고, 시안 판 그림은 그림자 여백까지 넓혀 깐다.
            sheet.gameObject.AddComponent<Image>().color = Color.clear;
            RectTransform panel = Ui.Rect("Panel", sheet);
            Ui.Spread(panel, PanelPad.left, PanelPad.top, PanelPad.right, PanelPad.bottom);
            Image panelFace = panel.gameObject.AddComponent<Image>();
            // 그림 2.4배: 모서리 46 + 그림자 여백
            Ui.Sliced(
                panelFace,
                Art(
                    "QuestPanel",
                    new Vector4(
                        PanelPad.left + 46,
                        PanelPad.bottom + 46,
                        PanelPad.right + 46,
                        PanelPad.top + 46
                    ) * ArtPixels
                )
            );
            panelFace.raycastTarget = false;
            Set(fields, "sheet", sheet);

            BuildChooser(view, fields, Page("Choose", sheet));
            BuildList(view, fields, Page("List", sheet));
            BuildMap(view, fields, Page("Map", sheet));
            PracticeBoardBuilder.Build(view, fields, sheet);
            fields.ApplyModifiedPropertiesWithoutUndo();
            board.gameObject.SetActive(false);

            // 퀘스트 카드는 수첩 대신 목록을 연다.
            var questCard = (Button)
                new SerializedObject(mission).FindProperty("journalButton").objectReferenceValue;
            for (int i = questCard.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(questCard.onClick, i);
            Listen(questCard, mission.OpenQuestBoard);
            BuilderTools.SetReference(mission, "questBoard", view);
        }

        // 고르기(시안 2-B ① c2): 머리줄 퀘스트 책 아이콘·'퀘스트'·닫기, 가운데에 큰 그림 두 개(600×280, 왼쪽 100·820, 위 225)와
        // 그 아래 이름(Jua 60, 위 515), 사이에 세로 줄(2×460, 왼쪽 759·위 170). 그림은 시안을 그림자·빛 번짐(사방 40)까지 렌더링했다.
        // 그림과 이름을 덮는 칸(718×350)을 누르면 그 쪽을 연다.
        private static void BuildChooser(
            QuestBoardView view,
            SerializedObject fields,
            RectTransform page
        )
        {
            PracticeBoardBuilder.Header(view, page, PracticeBoardBuilder.QuestPage.Choose);
            Picture(page, "Divider", 759, 170, 2, 460, (0, 0, 0, 0), Art("QuestPickDivider"));
            Option(
                page,
                "StoryOption",
                41,
                100,
                "이야기",
                Art("QuestPickStory", default, true),
                view.ShowStory
            );
            Option(
                page,
                "PlaceOption",
                761,
                820,
                "장소 연습",
                Art("QuestPickPlace", default, true),
                view.ShowPractice
            );
            Set(fields, "choosePage", page.gameObject);
        }

        // 고르기 칸 하나: 누르는 자리(왼쪽 left, 위 225, 718×350), 그림(왼쪽 stageLeft, 600×280), 그 아래 이름
        private static void Option(
            RectTransform page,
            string name,
            float left,
            float stageLeft,
            string label,
            Sprite art,
            UnityAction choose
        )
        {
            RectTransform option = Ui.Rect(name, page);
            Ui.TopLeft(option, left, 225, 718, 350);
            option.gameObject.AddComponent<Image>().color = Color.clear;
            Image picture = Picture(
                option,
                "Art",
                stageLeft - left,
                0,
                600,
                280,
                (40, 40, 40, 40),
                art
            );
            // TMP의 Jua는 칸 가운데에 두면 브라우저보다 낮게 그려져, 캡처로 잰 만큼(Jua 46에서 시안 3.5px) 글자 크기에 맞춰 올린다.
            TMP_Text text = Text(
                option,
                "Label",
                stageLeft - left,
                515 - 225 - 3.5f * 60 / 46,
                600,
                60,
                Ui.DisplayFontPath,
                60,
                Ink
            );
            text.text = label;
            text.alignment = TextAlignmentOptions.Midline;
            var button = option.gameObject.AddComponent<Button>();
            button.targetGraphic = picture;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.98f, .98f, .98f);
            colors.pressedColor = new Color(.9f, .9f, .9f);
            button.colors = colors;
            Listen(button, choose);
        }

        // 목록(시안 c4): 머리줄 ‹·이야기 아이콘·'이야기'·'퀘스트'·닫기, 카드 한 줄 세 장, 쪽이 여럿이면 아래 가운데 쪽 넘기기
        private static void BuildList(
            QuestBoardView view,
            SerializedObject fields,
            RectTransform list
        )
        {
            PracticeBoardBuilder.Header(view, list, PracticeBoardBuilder.QuestPage.Story);

            // 들르는 곳 알약(시안 c4 .rp 높이 34, 양옆 13, Jua 17)과 사이 화살표(본문 16) 견본.
            // 알약 폭은 QuestBoardView가 글자 폭에 맞춰 정하고, 글자는 알약 가운데에 둔다.
            RectTransform chip = Ui.Rect("RouteChip", list);
            Image chipFace = chip.gameObject.AddComponent<Image>();
            Ui.Sliced(chipFace, Art("QuestRoutePill", new Vector4(17, 0, 17, 0) * ArtPixels));
            chipFace.raycastTarget = false;
            TMP_Text chipLabel = Text(chip, "Label", 0, 0, 60, 34, Ui.DisplayFontPath, 17, Ink);
            Ui.Fill(chipLabel.rectTransform);
            chipLabel.alignment = TextAlignmentOptions.Midline;
            // TMP의 Jua는 칸 가운데에 두면 브라우저보다 낮게 그려져, 캡처로 잰 만큼(Jua 19에서 시안 2.5px) 글자 크기에 맞춰 올린다.
            chipLabel.rectTransform.offsetMin = chipLabel.rectTransform.offsetMax = new Vector2(
                0,
                2.5f * 17 / 19 * Px
            );
            chip.gameObject.SetActive(false);
            TMP_Text arrow = Text(list, "RouteArrow", 0, 0, 10, 34, Ui.BodyFontPath, 16, Ink);
            arrow.text = "›";
            arrow.alignment = TextAlignmentOptions.Midline;
            arrow.gameObject.SetActive(false);
            Set(fields, "routeChip", chipFace);
            Set(fields, "routeArrow", arrow);

            SerializedProperty cards = fields.FindProperty("cards");
            cards.arraySize = CardsPerPage;
            for (int slot = 0; slot < CardsPerPage; slot++)
                BuildCard(view, cards.GetArrayElementAtIndex(slot), list, slot);
            Set(fields, "unlock", BuildUnlock(view, list));

            // 쪽 넘기기: [앞] 12 [1 / 2] 12 [뒤], 판 가운데
            const float pagerWidth = 163.44f;
            float left = (TabSheetWidth - pagerWidth) / 2;
            Listen(
                ArtButton(
                    list,
                    "Previous",
                    left,
                    PagerTop,
                    50,
                    50,
                    (22, 12, 22, 40),
                    Art("QuestBack")
                ),
                view.PreviousPage
            );
            TMP_Text pageText = Text(
                list,
                "Page",
                left + 62,
                PagerTop,
                pagerWidth - 124,
                50,
                Ui.DisplayFontPath,
                19,
                PagerInk
            );
            pageText.alignment = TextAlignmentOptions.Midline;
            pageText.margin = new Vector4(0, 2 * Px, 0, 0);
            Listen(
                ArtButton(
                    list,
                    "Next",
                    left + pagerWidth - 50,
                    PagerTop,
                    50,
                    50,
                    (22, 12, 22, 40),
                    Art("QuestNext")
                ),
                view.NextPage
            );
            Set(fields, "previousButton", list.Find("Previous").GetComponent<Button>());
            Set(fields, "nextButton", list.Find("Next").GetComponent<Button>());
            Set(fields, "pageText", pageText);

            Set(fields, "listPage", list.gameObject);
            Set(fields, "cardSprite", Art("QuestCardLarge", CardBorder));
            Set(fields, "currentCardSprite", Art("QuestCardLargeOn", CardBorder));
            Set(fields, "nowChip", Art("QuestChipNowLarge"));
            Set(fields, "newChip", Art("QuestChipNewLarge"));
            Set(fields, "routeSprite", chipFace.sprite);
            Set(
                fields,
                "visitedRouteSprite",
                Art("QuestRoutePillDone", new Vector4(17, 0, 17, 0) * ArtPixels)
            );
            SerializedProperty icons = fields.FindProperty("icons");
            icons.arraySize = Icons.Length;
            for (int i = 0; i < Icons.Length; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = Art(
                    "QuestCardIcon" + Icons[i]
                );
        }

        // 카드 그림의 늘리는 자리(그림 px): 모서리 34 + 그림자 여백
        private static Vector4 CardBorder =>
            new Vector4(
                CardPad.left + 34,
                CardPad.bottom + 34,
                CardPad.right + 34,
                CardPad.top + 34
            ) * ArtPixels;

        // 카드(시안 c4 .cards.small .card): 아이콘(68)·상태 칩(오른쪽 26, 위 34, 높이 38)·제목(Jua 28)·설명(17, 줄 높이 1.55)과
        // 아래 70에 들르는 곳 알약 줄, 아래 28에 보상 줄. 끝낸 퀘스트는 오른쪽 위 '완료' 도장(88), 잠긴 퀘스트는 카드째 .55로 흐리게 하고
        // 가운데(세로 52%)에 자물쇠를 둔다. 자리는 테두리 1.5 카드 기준이며, 지금 하는 퀘스트의 카드는 테두리가 두꺼워
        // QuestBoardView가 안의 내용(Content)을 들인다. 아래 줄들은 카드 아래를 기준으로 둔다.
        private static void BuildCard(
            QuestBoardView view,
            SerializedProperty slot,
            RectTransform list,
            int index
        )
        {
            RectTransform card = Ui.Rect("Card" + index, list);
            card.name = "Card";
            Ui.TopLeft(card, CardLefts[index], CardTop, CardWidth, CardHeight);
            Image area = card.gameObject.AddComponent<Image>();
            area.color = Color.clear;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            Image background = Picture(
                card,
                "Background",
                0,
                0,
                CardWidth,
                CardHeight,
                CardPad,
                null
            );
            Ui.Sliced(background, Art("QuestCardLarge", CardBorder));
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            // 잠긴 카드는 카드째 흐리게 하므로 누를 수 없을 때 색을 더 바꾸지 않는다.
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.98f, .98f, .98f);
            colors.pressedColor = new Color(.92f, .92f, .92f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
            UnityEventTools.AddIntPersistentListener(button.onClick, view.ShowCard, index);
            EditorAndRuntime(button.onClick);

            RectTransform content = Ui.Rect("Content", card);
            Ui.Fill(content);
            Image icon = Picture(
                content,
                "Icon",
                31,
                31,
                68,
                68,
                (14 * IconPadScale, 8 * IconPadScale, 14 * IconPadScale, 22 * IconPadScale),
                Art("QuestCardIconHotel")
            );
            RectTransform chip = Ui.Rect("Chip", content);
            chip.anchorMin = chip.anchorMax = chip.pivot = Vector2.one;
            chip.anchoredPosition = new Vector2(-27.5f, -35.5f) * Px;
            Image chipImage = Ui.Picture(chip.gameObject, Art("QuestChipNowLarge"));
            chip.sizeDelta = chipImage.sprite.rect.size / 2 * (38f / 44f);
            // '완료' 도장(88, -14도 기울임)은 기울인 모양까지 104 크기로 그렸다.
            Image done = Picture(
                content,
                "DoneStamp",
                286.5f,
                17.5f,
                88,
                88,
                (12 * BadgeScale, 12 * BadgeScale, 12 * BadgeScale, 12 * BadgeScale),
                Art("QuestDoneStampLarge")
            );

            // TMP의 Jua는 칸 가운데에 두면 브라우저보다 낮게 그려져, 캡처로 잰 만큼(Jua 33에서 시안 3.5px) 글자 크기에 맞춰 올린다.
            TMP_Text title = Text(
                content,
                "Title",
                31,
                125 - 3.5f * 28 / 33,
                338,
                35,
                Ui.DisplayFontPath,
                28,
                Ink
            );
            title.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text description = Text(
                content,
                "Description",
                31,
                DescriptionTop,
                338,
                53,
                Ui.BodyFontPath,
                17,
                Muted
            );
            description.textWrappingMode = TextWrappingModes.Normal;
            description.alignment = TextAlignmentOptions.TopLeft;
            description.lineSpacing = DescriptionLineSpacing;

            // 들르는 곳 알약과 화살표는 QuestBoardView가 줄 수만큼 위로 쌓는다(카드 아래 71에서 끝남).
            RectTransform route = Ui.Rect("Route", content);
            route.anchorMin = route.anchorMax = route.pivot = Vector2.zero;
            route.anchoredPosition = new Vector2(31, 71) * Px;
            route.sizeDelta = new Vector2(342, 34) * Px;

            // 보상 줄: 코인(26)과 사이 8, 글자(본문 16). 카드 아래 29에 둔다.
            RectTransform meta = Ui.Rect("MetaRow", content);
            meta.anchorMin = meta.anchorMax = meta.pivot = Vector2.zero;
            meta.anchoredPosition = new Vector2(31, 29) * Px;
            meta.sizeDelta = new Vector2(342, 26) * Px;
            Picture(meta, "Coin", 0, 0, 26, 26, (0, 0, 0, 0), Art("QuestCoinLarge"));
            TMP_Text metaText = Text(meta, "Meta", 34, 0, 308, 26, Ui.BodyFontPath, 16, Muted);

            // 잠긴 퀘스트: 카드 가운데(세로 52%)의 자물쇠(시안 .lock 88). 카드째 흐리게 하는 것은 QuestBoardView가 한다.
            RectTransform locked = Ui.Rect("Locked", content);
            Ui.Fill(locked);
            Picture(
                locked,
                "Lock",
                (CardWidth - 88) / 2,
                CardHeight * .52f - 88 / 2f,
                88,
                88,
                LockBadgePad,
                Art("QuestLockBadge")
            );
            locked.gameObject.SetActive(false);

            Set(slot, "Root", card);
            Set(slot, "Button", button);
            Set(slot, "Group", group);
            Set(slot, "Background", background);
            Set(slot, "Content", content);
            Set(slot, "Icon", icon);
            Set(slot, "Chip", chipImage);
            Set(slot, "DoneStamp", done.gameObject);
            Set(slot, "Title", title);
            Set(slot, "Description", description);
            Set(slot, "Route", route);
            Set(slot, "Meta", metaText);
            Set(slot, "Locked", locked.gameObject);
            card.gameObject.SetActive(false);
        }

        // 설명(본문 17, 줄 높이 1.55 = 26.35)의 첫 줄 자리와 줄 사이. TMP 기본 줄 높이(글자 크기의 1.2배)에 시안 줄 높이까지의 차이를 더하고,
        // 첫 줄은 시안 줄의 위 여백만큼 내린다(본문 18·줄 높이 1.6을 캡처로 맞춘 값을 같은 방식으로 옮겼다).
        private const float DescriptionTop = 172.2f;
        private const float DescriptionLineSpacing = 35;

        // 줄인 카드의 자물쇠(88) 그림자 여백. 그림은 104 크기(그림자 여백 20·12·20·28)로 그렸다.
        private static readonly (float left, float top, float right, float bottom) LockBadgePad = (
            20 * BadgeScale,
            12 * BadgeScale,
            20 * BadgeScale,
            28 * BadgeScale
        );

        // 연계 퀘스트 잠금 해제: 머리줄로 내려오는 띠와, 열리는 카드에 겹쳐 두는 빛 테두리·흰 막(잠긴 카드의 흐림)·자물쇠·조각·반짝이·[퀘스트 시작].
        // 자리는 카드 왼쪽 위 기준 시안 px이다. 조각·반짝이는 첫 연출(시안 f2 카드 378×322)의 자리를 자물쇠 둘레로 넓혀 둔다.
        // 돌고 커지는 부품은 가운데를 축으로 둔다.
        private static QuestUnlockEffect BuildUnlock(QuestBoardView view, RectTransform list)
        {
            RectTransform toast = Ui.Rect("UnlockToast", list);
            toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(.5f, 1);
            // 판이 화면 높이에 가까워 위에 띠를 둘 자리가 없으므로, 머리줄 가운데 빈 곳('퀘스트' 끝 416과 닫기 1403 사이)에
            // 머리 아이콘(위 35, 높이 76)과 세로 가운데로 둔다.
            toast.anchoredPosition =
                new Vector2((416 + 1403) / 2f - TabSheetWidth / 2, -(35 + 76 / 2f - 72 / 2f)) * Px;
            toast.sizeDelta = new Vector2(409.62f, 72) * Px;
            RectTransform toastArt = Ui.Rect("Art", toast);
            Ui.Spread(toastArt, 24, 12, 24, 54);
            Ui.Picture(toastArt.gameObject, Art("QuestUnlockToast"));
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toast.gameObject.SetActive(false);

            RectTransform unlock = Ui.Rect("Unlock", list);
            Ui.TopLeft(unlock, 0, 0, CardWidth, CardHeight);
            var effect = unlock.gameObject.AddComponent<QuestUnlockEffect>();
            var fields = new SerializedObject(effect);

            Image glow = Picture(
                unlock,
                "Glow",
                -60,
                -60,
                CardWidth + 120,
                CardHeight + 120,
                (0, 0, 0, 0),
                Art("QuestCardGlowLarge")
            );
            var glowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            glowGroup.blocksRaycasts = false;
            RectTransform veil = Ui.Rect("Veil", unlock);
            Ui.Fill(veil);
            Image veilFace = veil.gameObject.AddComponent<Image>();
            // 잠긴 카드는 카드째 .55로 흐리므로, 같은 모습이 되게 흰 막을 .45로 덮는다.
            Ui.Sliced(veilFace, Art("QuestCardVeil", Vector4.one * Mathf.Ceil(34 * ArtPixels)));
            veilFace.color = new Color(1, 1, 1, .45f);
            veilFace.raycastTarget = false;
            Vector2 lockCentre = new Vector2(CardWidth / 2, CardHeight * .52f);
            RectTransform closedLock = Centred(
                unlock,
                "ClosedLock",
                lockCentre,
                new Vector2(88, 88),
                LockBadgePad,
                Art("QuestLockBadge")
            );
            RectTransform openLock = Centred(
                unlock,
                "OpenLock",
                lockCentre,
                new Vector2(88, 88),
                LockBadgePad,
                Art("QuestLockBadgeOpen")
            );
            openLock.gameObject.SetActive(false);

            // 흰 막 조각(시안 가운데 자리·크기·기울기). 조각 그림은 22×16에 사방 2 여백이다.
            (float x, float y, float w, float h, float angle)[] shardSpots =
            {
                (161, 96, 22, 16, 20),
                (247, 103, 18, 14, -30),
                (138, 180, 16, 20, 60),
                (272, 167, 20, 14, 140),
                (203, 64, 14, 12, 80),
            };
            SerializedProperty shards = fields.FindProperty("shards");
            shards.arraySize = shardSpots.Length;
            for (int i = 0; i < shardSpots.Length; i++)
            {
                var spot = shardSpots[i];
                RectTransform shard = Centred(
                    unlock,
                    "Shard",
                    AroundLock(spot.x, spot.y, lockCentre),
                    new Vector2(spot.w * 26 / 22, spot.h * 20 / 16) * SpreadScale,
                    (0, 0, 0, 0),
                    Art("QuestShard")
                );
                shard.localRotation = Quaternion.Euler(0, 0, -spot.angle);
                shard.gameObject.SetActive(false);
                shards.GetArrayElementAtIndex(i).objectReferenceValue = shard;
            }

            // 반짝이(시안 왼쪽 위 자리와 글자 크기). 그림은 글자 26의 별(21.22×26)에 사방 10 여백이다.
            (float x, float y, float size)[] sparkSpots =
            {
                (110, 110, 22),
                (290, 120, 26),
                (170, 40, 18),
                (300, 200, 18),
                (90, 190, 16),
            };
            SerializedProperty sparks = fields.FindProperty("sparks");
            sparks.arraySize = sparkSpots.Length;
            for (int i = 0; i < sparkSpots.Length; i++)
            {
                var spot = sparkSpots[i];
                float scale = spot.size / 26 * SpreadScale;
                RectTransform spark = Centred(
                    unlock,
                    "Spark",
                    AroundLock(spot.x + 21.22f * scale / 2, spot.y + 26 * scale / 2, lockCentre),
                    new Vector2(41.22f, 46) * scale,
                    (0, 0, 0, 0),
                    Art("QuestSpark")
                );
                spark.gameObject.SetActive(false);
                sparks.GetArrayElementAtIndex(i).objectReferenceValue = spark;
            }

            // [퀘스트 시작](시안 .go 모양, 높이 56): 보상 줄(아래 29, 높이 26) 오른쪽 끝(카드 오른쪽 27.5)에 세로 가운데를 맞춘다.
            const float startWidth = 142.06f;
            Button start = ArtButton(
                unlock,
                "CardStart",
                CardWidth - 27.5f - startWidth,
                CardHeight - 29 - 13 - 28,
                startWidth,
                56,
                (22, 12, 22, 40),
                Art("QuestCardStart")
            );
            CentrePivot((RectTransform)start.transform);
            Listen(start, view.RequestStart);

            fields.FindProperty("toast").objectReferenceValue = toastGroup;
            fields.FindProperty("glow").objectReferenceValue = glowGroup;
            fields.FindProperty("veil").objectReferenceValue = veilFace;
            fields.FindProperty("closedLock").objectReferenceValue = closedLock;
            fields.FindProperty("openLock").objectReferenceValue = openLock;
            fields.FindProperty("startButton").objectReferenceValue = start.transform;
            fields.ApplyModifiedPropertiesWithoutUndo();
            unlock.gameObject.SetActive(false);
            return effect;
        }

        // 첫 연출의 조각·반짝이 자리(시안 f2 카드, 자물쇠 가운데 189·161)를 지금 카드의 자물쇠 둘레로 옮겨 넓힌다.
        private const float SpreadScale = CardWidth / 378f;

        private static Vector2 AroundLock(float x, float y, Vector2 lockCentre) =>
            lockCentre + new Vector2(x - 189, y - 161) * SpreadScale;

        // 가운데(시안 px, 부모 왼쪽 위 기준)를 축으로 둔 그림. 그림은 pad만큼 바깥으로 넓다.
        internal static RectTransform Centred(
            Transform parent,
            string name,
            Vector2 centre,
            Vector2 size,
            (float left, float top, float right, float bottom) pad,
            Sprite sprite
        )
        {
            RectTransform rect = Ui.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(centre.x, -centre.y) * Px;
            rect.sizeDelta = size * Px;
            RectTransform art = Ui.Rect("Art", rect);
            Ui.Spread(art, pad.left, pad.top, pad.right, pad.bottom);
            Ui.Picture(art.gameObject, sprite);
            return rect;
        }

        // 왼쪽 위에 둔 칸을 같은 자리에서 가운데를 축으로 바꾼다. 커지거나 도는 연출이 가운데에서 일어난다.
        internal static void CentrePivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition += new Vector2(size.x / 2, -size.y / 2);
        }

        // 도장 지도(q2): 뒤로·제목·도장 수 칩·[퀘스트 시작]·닫기, 둥근 지도, 아래 들르는 곳 칸 줄
        private static void BuildMap(
            QuestBoardView view,
            SerializedObject fields,
            RectTransform page
        )
        {
            Listen(
                ArtButton(page, "Back", 37, 28, 50, 50, (22, 12, 22, 40), Art("QuestBack")),
                view.ShowList
            );
            TMP_Text title = Text(
                page,
                "MapTitle",
                103,
                33.5f,
                860,
                39,
                Ui.DisplayFontPath,
                36,
                Ink
            );
            title.margin = new Vector4(0, 3 * Px, 0, 0);

            RectTransform count = Ui.Rect("CountChip", page);
            Ui.TopLeft(count, 505.83f, 37, 96, 32);
            RectTransform countArt = Ui.Rect("Art", count);
            Ui.Spread(countArt, 10, 6, 10, 14);
            Image countFace = countArt.gameObject.AddComponent<Image>();
            Ui.Sliced(countFace, Art("QuestCountChip", new Vector4(26, 30, 26, 22) * ArtPixels));
            countFace.raycastTarget = false;
            TMP_Text countText = Text(
                count,
                "Count",
                0,
                0,
                96,
                32,
                Ui.DisplayFontPath,
                16,
                Color.white
            );
            Ui.Fill(countText.rectTransform);
            countText.alignment = TextAlignmentOptions.Midline;
            countText.margin = new Vector4(0, 1 * Px, 0, 0);

            Button start = ArtButton(
                page,
                "Start",
                1137 - 125.48f,
                28,
                125.48f,
                50,
                (22, 12, 22, 40),
                Art("QuestStartButton")
            );
            Listen(start, view.RequestStart);
            Listen(Close(page), view.Close);

            RectTransform box = Ui.Rect("MapBox", page);
            Ui.TopLeft(box, 37, 109, 1168, 467);
            Image mask = box.gameObject.AddComponent<Image>();
            Ui.Sliced(mask, Art("QuestMapMask", Vector4.one * Mathf.Ceil(26 * ArtPixels)));
            box.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform map = Ui.Rect("MapArt", box);
            Ui.TopLeft(map, 0, 0, 1168, 467);
            Ui.Picture(map.gameObject, Art("QuestMap", default, mipmaps: true));

            RectTransform stamp = Mark(map, "Stamp", 190, 190, Art("QuestStampHotel"));
            stamp.gameObject.SetActive(false);
            RectTransform pin = Mark(map, "Pin", 44, 44, null);
            RectTransform pinArt = Ui.Rect("Art", pin);
            Ui.Spread(pinArt, 16, 16, 16, 24);
            Ui.Picture(pinArt.gameObject, Art("QuestPin"));
            TMP_Text pinNumber = Text(
                pin,
                "PinNumber",
                0,
                0,
                44,
                44,
                Ui.DisplayFontPath,
                20,
                Color.white
            );
            Ui.Lowered(pinNumber, 4);
            RectTransform now = Ui.Rect("NowLabel", map);
            Ui.TopLeft(now, 0, 0, 89.03f, 32);
            RectTransform nowArt = Ui.Rect("Art", now);
            Ui.Spread(nowArt, 10, 6, 10, 14);
            Ui.Picture(nowArt.gameObject, Art("QuestNowChip"));

            // 들르는 곳 칸 줄: 그림자까지 보이게 칸 바깥 여백을 둔 창에서 옆으로 넘긴다.
            RectTransform stops = Ui.Rect("Stops", page);
            Ui.TopLeft(stops, 37 - 22, 590 - 14, 1166 + 44, 96 + 46);
            stops.gameObject.AddComponent<RectMask2D>();
            stops.gameObject.AddComponent<Image>().color = Color.clear;
            var scroll = stops.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform row = Ui.Rect("Content", stops);
            row.anchorMin = Vector2.zero;
            row.anchorMax = new Vector2(0, 1);
            row.pivot = new Vector2(0, .5f);
            row.offsetMin = row.offsetMax = Vector2.zero;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(Round(22), Round(22), Round(14), 0);
            rowLayout.spacing = 12 * Px;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            scroll.viewport = stops;
            scroll.content = row;
            BuildStop(fields.FindProperty("stopTemplate"), row);

            Set(fields, "mapPage", page.gameObject);
            Set(fields, "startButton", start);
            Set(fields, "mapTitle", title);
            Set(fields, "countChip", count);
            Set(fields, "countText", countText);
            Set(fields, "map", map);
            Set(fields, "stampTemplate", stamp.GetComponent<Image>());
            Set(fields, "pin", pin);
            Set(fields, "pinNumber", pinNumber);
            Set(fields, "nowLabel", now);
            Set(fields, "stopsContent", row);
            Set(fields, "stopSprite", Art("QuestStop"));
            Set(fields, "currentStopSprite", Art("QuestStopOn"));
            Set(fields, "slotSprite", Art("QuestSlot"));
            Set(fields, "currentSlotSprite", Art("QuestSlotNow"));
            SerializedProperty places = fields.FindProperty("placeStamps");
            places.arraySize = Stamps.Length;
            for (int i = 0; i < Stamps.Length; i++)
            {
                SerializedProperty item = places.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Place").stringValue = Stamps[i].Place;
                item.FindPropertyRelative("Sprite").objectReferenceValue = Art(
                    "QuestStamp" + Stamps[i].Art
                );
            }
            page.gameObject.SetActive(false);
        }

        // 들르는 곳 칸(시안 .st 282.5×96): 번호 칸(58) 또는 받은 도장(64), '장소 · 이름'(Jua 19), 할 일(14)
        private static void BuildStop(SerializedProperty slot, RectTransform row)
        {
            RectTransform stop = Ui.Rect("Stop", row);
            stop.anchorMin = stop.anchorMax = stop.pivot = new Vector2(0, 1);
            stop.sizeDelta = new Vector2(282.5f, 96) * Px;
            Image background = Picture(
                stop,
                "Background",
                0,
                0,
                282.5f,
                96,
                (22, 14, 22, 32),
                Art("QuestStop")
            );
            Image number = Picture(stop, "Slot", 15, 19, 58, 58, (4, 4, 4, 4), Art("QuestSlot"));
            TMP_Text numberText = Text(
                number.transform,
                "Number",
                0,
                0,
                66,
                66,
                Ui.DisplayFontPath,
                22,
                Muted
            );
            Ui.Fill(numberText.rectTransform);
            numberText.alignment = TextAlignmentOptions.Midline;
            // 도장 그림(178에 여백 6)을 칸 64에 맞춘다.
            float size = 64 * 190f / 178f;
            Image stamp = Picture(
                stop,
                "StopStamp",
                15 - (size - 64) / 2,
                16 - (size - 64) / 2,
                size,
                size,
                (0, 0, 0, 0),
                Art("QuestStampHotel")
            );
            // '장소 · 이름'과 할 일을 한 묶음으로 칸 세로 가운데에 둔다(시안 사이 4). 할 일이 없으면 제목만 가운데에 온다.
            RectTransform words = Ui.Rect("Words", stop);
            Ui.TopLeft(words, 91, 0, 176.5f, 96);
            var column = words.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleLeft;
            column.spacing = 4 * Px;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            TMP_Text title = Text(
                words,
                "StopTitle",
                0,
                0,
                176.5f,
                28,
                Ui.DisplayFontPath,
                19,
                Ink
            );
            title.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text todo = Text(words, "Todo", 0, 0, 176.5f, 20, Ui.BodyFontPath, 14, Muted);
            todo.overflowMode = TextOverflowModes.Ellipsis;
            Set(slot, "Root", stop);
            Set(slot, "Background", background);
            Set(slot, "Slot", number);
            Set(slot, "Number", numberText);
            Set(slot, "Stamp", stamp);
            Set(slot, "Title", title);
            Set(slot, "Todo", todo);
            stop.gameObject.SetActive(false);
        }

        private static RectTransform Page(string name, RectTransform sheet)
        {
            RectTransform page = Ui.Rect(name, sheet);
            Ui.Fill(page);
            return page;
        }

        // 테마 유리 판. 테마 판 그림의 모서리(시안 32)를 판 모서리(corner)로 키운다.
        internal static void Glass(RectTransform sheet, float corner)
        {
            Image glass = sheet.gameObject.AddComponent<Image>();
            glass.pixelsPerUnitMultiplier = 32f / corner;
            var role = sheet.gameObject.AddComponent<ThemedElement>();
            var roleFields = new SerializedObject(role);
            roleFields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(CatalogPath);
            roleFields.FindProperty("role").enumValueIndex = (int)UiThemeRole.Panel;
            roleFields.ApplyModifiedPropertiesWithoutUndo();
            role.ApplyCurrent();
        }

        // 머리줄 오른쪽 닫기. 1240 판에서는 x 1153이다.
        internal static Button Close(RectTransform page, float x = 1153) =>
            ArtButton(
                page,
                "Close",
                x,
                27,
                52,
                52,
                (14, 12, 14, 20),
                AssetDatabase.LoadAssetAtPath<Sprite>(ClosePath)
            );

        // 그림만 있는 요소. 자리는 그림자 여백(pad)을 뺀 시안 요소 자리다.
        internal static Image Picture(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            (float left, float top, float right, float bottom) pad,
            Sprite sprite
        )
        {
            RectTransform rect = Ui.Rect(name, parent);
            Ui.TopLeft(
                rect,
                x - pad.left,
                y - pad.top,
                width + pad.left + pad.right,
                height + pad.top + pad.bottom
            );
            return Ui.Picture(rect.gameObject, sprite);
        }

        // 그림 버튼. 누르는 자리는 시안 요소 자리이고, 그림(그림자 포함)은 그 바깥으로 pad만큼 넓다.
        internal static Button ArtButton(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            (float left, float top, float right, float bottom) pad,
            Sprite sprite
        )
        {
            RectTransform rect = Ui.Rect(name, parent);
            Ui.TopLeft(rect, x, y, width, height);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            RectTransform art = Ui.Rect("Art", rect);
            Ui.Spread(art, pad.left, pad.top, pad.right, pad.bottom);
            Image face = Ui.Picture(art.gameObject, sprite);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.disabledColor = new Color(1, 1, 1, .45f);
            button.colors = colors;
            return button;
        }

        private static RectTransform Mark(
            Transform map,
            string name,
            float width,
            float height,
            Sprite sprite
        )
        {
            RectTransform mark = Ui.Rect(name, map);
            mark.anchorMin = mark.anchorMax = new Vector2(0, 1);
            mark.pivot = new Vector2(.5f, .5f);
            mark.sizeDelta = new Vector2(width, height) * Px;
            if (sprite != null)
                Ui.Picture(mark.gameObject, sprite);
            return mark;
        }

        internal static TMP_Text Text(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            string font,
            float size,
            Color color
        )
        {
            RectTransform rect = Ui.Rect(name, parent);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Ui.Style(text, font, size, color);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Ui.TopLeft(rect, x, y, width, height);
            return text;
        }

        private static Button Plain(Button button)
        {
            button.transition = Selectable.Transition.None;
            return button;
        }

        // EditMode 테스트와 Editor에서도 누르면 동작하도록 Editor·실행 모두에서 부른다.
        internal static void Listen(Button button, UnityAction action)
        {
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorAndRuntime(button.onClick);
        }

        private static void EditorAndRuntime(UnityEvent clicked) =>
            clicked.SetPersistentListenerState(
                clicked.GetPersistentEventCount() - 1,
                UnityEventCallState.EditorAndRuntime
            );

        private static void Set(SerializedObject fields, string name, Object value) =>
            fields.FindProperty(name).objectReferenceValue = value;

        private static void Set(SerializedProperty slot, string name, Object value) =>
            slot.FindPropertyRelative(name).objectReferenceValue = value;

        private static int Round(float px) => Mathf.RoundToInt(px * Px);

        // 렌더링한 그림을 그대로 가져온다. 늘려 쓰는 알약·칩·창 그림은 둥근 모서리 자리(border, 그림 px)를 둔다.
        internal static Sprite Art(string name, Vector4 border = default, bool mipmaps = false)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
