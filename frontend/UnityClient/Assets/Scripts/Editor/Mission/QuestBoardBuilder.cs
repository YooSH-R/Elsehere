using A305.EditorTools.Common;
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
    /// 퀘스트 목록(시안 f2)과 도장 지도(시안 q2)를 첫날 미션 프리팹의 Content/QuestBoard로 만든다. 자리는 시안 px × 1.2 캔버스 단위이고,
    /// 그림은 시안을 2.4배로 렌더링한 Art/UI/Quest 조각이다. 판은 다른 시트처럼 테마 유리(Panel)다.
    /// 왼쪽 위 퀘스트 카드를 누르면 수첩 대신 목록을 열고, 목록의 카드를 누르면 그 퀘스트의 도장 지도로 가도록 버튼을 연결한다.
    /// 연계 퀘스트의 잠금을 푸는 연출(띠·자물쇠·흰 막 조각·빛 테두리·[퀘스트 시작])도 목록 위에 만든다.
    /// 머리줄 제목 옆 [이야기 | 장소 연습] 탭과 장소 연습 쪽은 PracticeBoardBuilder가 만든다.
    /// </summary>
    public static class QuestBoardBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Board";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string CatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";
        private const string ArtFolder = "Assets/Art/UI/Quest";
        private const string ClosePath = "Assets/Art/UI/Feedback/FeedbackClose.png";
        private const float Px = Ui.Px;

        // 시트 모서리(시안 48)
        private const float SheetCorner = 48;

        // 렌더링한 그림은 2.4배라 캔버스 1단위 = 그림 2px
        private const float ArtPixels = 2.4f;

        // 목록(시안 f2) 판 1240×500 안: 카드 378×322(가로 394 간격, 한 쪽 한 줄 세 장). 도장 지도(시안 q2) 판은 1240×770이고
        // 들르는 곳 칸은 282.5×96(간격 12)이다.
        private const int CardsPerPage = 3;
        private const float CardWidth = 378;
        private const float CardHeight = 322;

        // 쪽이 여럿이면 판을 570으로 늘리고 카드 아래 22에 쪽 넘기기를 둔다.
        private const float PagerTop = 111 + CardHeight + 22;

        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);
        private static readonly Color ArrowInk = new Color32(179, 165, 143, 255);

        private static readonly string[] Icons = { "Home", "Chat", "Star", "Book", "Map" };

        private static readonly (string Place, string Art)[] Stamps =
        {
            ("호텔", "Hotel"),
            ("카페", "Cafe"),
            ("드럭스토어", "Drugstore"),
            ("거리", "Street"),
            ("식당", "Diner"),
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
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
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

            // 바깥은 조금 어둡게 칠하고 누르면 닫는다.
            RectTransform shade = Ui.Rect("Shade", board);
            Ui.Fill(shade);
            shade.gameObject.AddComponent<Image>().color = new Color(.07f, .11f, .17f, .35f);
            Listen(Plain(shade.gameObject.AddComponent<Button>()), view.Close);

            RectTransform sheet = Ui.Rect("Sheet", board);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.anchoredPosition = Vector2.zero;
            sheet.sizeDelta = new Vector2(1240, 500) * Px;
            Glass(sheet, SheetCorner);
            Set(fields, "sheet", sheet);

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

        // 목록(f2): 머리줄 아이콘·'퀘스트'·탭·닫기, 카드 한 줄 세 장, 쪽이 여럿이면 아래 가운데 쪽 넘기기
        private static void BuildList(
            QuestBoardView view,
            SerializedObject fields,
            RectTransform list
        )
        {
            Picture(list, "HeaderIcon", 37, 23, 60, 60, (14, 8, 14, 22), Art("QuestHeaderIcon"));
            TMP_Text title = Text(list, "Title", 113, 33.5f, 400, 39, Ui.DisplayFontPath, 36, Ink);
            title.text = "퀘스트";
            title.margin = new Vector4(0, 3 * Px, 0, 0);
            PracticeBoardBuilder.Tabs(view, list, storyShown: true);
            Listen(Close(list), view.Close);

            // 들르는 곳 알약과 사이 화살표 견본. 알약 그림은 사방 4의 여백이 있어 그만큼 안쪽 여백과 간격을 고친다.
            // 알약 폭은 QuestBoardView가 글자 폭에 맞춰 정하고, 글자는 알약 가운데에 둔다.
            RectTransform chip = Ui.Rect("RouteChip", list);
            Image chipFace = chip.gameObject.AddComponent<Image>();
            Ui.Sliced(chipFace, Art("QuestRoute", Vector4.one * Mathf.Floor(19 * ArtPixels)));
            chipFace.raycastTarget = false;
            TMP_Text chipLabel = Text(chip, "Label", 0, 0, 60, 20, Ui.DisplayFontPath, 15, Ink);
            Ui.Fill(chipLabel.rectTransform);
            chipLabel.alignment = TextAlignmentOptions.Midline;
            chip.gameObject.SetActive(false);
            TMP_Text arrow = Text(list, "RouteArrow", 0, 0, 10, 30, Ui.BodyFontPath, 13, ArrowInk);
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
            float left = 620 - pagerWidth / 2;
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
                Muted
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
            Set(fields, "cardSprite", Art("QuestCard"));
            Set(fields, "currentCardSprite", Art("QuestCardOn"));
            Set(fields, "nowChip", Art("QuestChipNow"));
            Set(fields, "newChip", Art("QuestChipNew"));
            Set(fields, "routeSprite", chipFace.sprite);
            Set(
                fields,
                "visitedRouteSprite",
                Art("QuestRouteDone", Vector4.one * Mathf.Floor(19 * ArtPixels))
            );
            SerializedProperty icons = fields.FindProperty("icons");
            icons.arraySize = Icons.Length;
            for (int i = 0; i < Icons.Length; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = Art("QuestIcon" + Icons[i]);
        }

        // 카드(시안 .qc): 아이콘(52)·상태 칩·제목(Jua 23)·설명(15), 아래에 들르는 곳·보상 줄. 끝낸 퀘스트는 오른쪽 위 '완료' 도장
        private static void BuildCard(
            QuestBoardView view,
            SerializedProperty slot,
            RectTransform list,
            int index
        )
        {
            RectTransform card = Ui.Rect("Card" + index, list);
            card.name = "Card";
            Ui.TopLeft(card, 37 + index * 394, 111, CardWidth, CardHeight);
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
                (24, 16, 24, 40),
                Art("QuestCard")
            );
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            UnityEventTools.AddIntPersistentListener(button.onClick, view.ShowCard, index);
            EditorAndRuntime(button.onClick);

            Image icon = Picture(
                card,
                "Icon",
                21,
                21,
                52,
                52,
                (14, 8, 14, 22),
                Art("QuestIconHome")
            );
            RectTransform chip = Ui.Rect("Chip", card);
            chip.anchorMin = chip.anchorMax = new Vector2(0, 1);
            chip.pivot = Vector2.one;
            chip.anchoredPosition = new Vector2(CardWidth - 21 + 10, -(31 - 6)) * Px;
            Image chipImage = Ui.Picture(chip.gameObject, Art("QuestChipNow"));
            chip.sizeDelta = chipImage.sprite.rect.size / 2;
            Image done = Picture(
                card,
                "DoneStamp",
                331 - 37.58f,
                45 - 37.58f,
                75.16f,
                75.16f,
                (12, 12, 12, 12),
                Art("QuestDoneStamp")
            );

            TMP_Text title = Text(card, "Title", 21, 85, 336, 28, Ui.DisplayFontPath, 23, Ink);
            title.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text description = Text(
                card,
                "Description",
                21,
                119.44f,
                336,
                44,
                Ui.BodyFontPath,
                15,
                Muted
            );
            description.textWrappingMode = TextWrappingModes.Normal;
            description.alignment = TextAlignmentOptions.TopLeft;
            description.lineSpacing = 6;

            // 들르는 곳 알약과 화살표는 QuestBoardView가 보상 줄 위에서부터 놓는다(알약 그림 여백 4만큼 넓은 칸).
            RectTransform route = Ui.Rect("Route", card);
            Ui.TopLeft(route, 21 - 4, 239 - 4, 336 + 8, 38);

            RectTransform meta = Ui.Rect("MetaRow", card);
            Ui.TopLeft(meta, 21, 281, 336, 22);
            Picture(meta, "Coin", 0, 0, 22, 22, (6, 4, 6, 8), Art("QuestCoin"));
            TMP_Text metaText = Text(meta, "Meta", 30, 0, 306, 22, Ui.BodyFontPath, 14, Muted);

            // 잠긴 퀘스트: 카드 모양(모서리 26)의 옅은 흰 막과 가운데 자물쇠(64)
            RectTransform locked = Ui.Rect("Locked", card);
            Ui.Fill(locked);
            Image veil = locked.gameObject.AddComponent<Image>();
            Ui.Sliced(veil, Art("QuestMapMask", Vector4.one * Mathf.Ceil(26 * ArtPixels)));
            veil.color = new Color(1, 1, 1, .35f);
            veil.raycastTarget = false;
            Picture(
                locked,
                "Lock",
                (CardWidth - 64) / 2,
                (CardHeight - 64) / 2,
                64,
                64,
                (16, 10, 16, 26),
                Art("QuestLock")
            );
            locked.gameObject.SetActive(false);

            Set(slot, "Root", card);
            Set(slot, "Button", button);
            Set(slot, "Group", group);
            Set(slot, "Background", background);
            Set(slot, "Icon", icon);
            Set(slot, "Chip", chipImage);
            Set(slot, "DoneStamp", done.gameObject);
            Set(slot, "Title", title);
            Set(slot, "Description", description);
            Set(slot, "Route", route);
            Set(slot, "MetaRow", meta);
            Set(slot, "Meta", metaText);
            Set(slot, "Locked", locked.gameObject);
            card.gameObject.SetActive(false);
        }

        // 연계 퀘스트 잠금 해제(시안 f2): 판 위로 내려오는 띠와, 열리는 카드에 겹쳐 두는 빛 테두리·흰 막·자물쇠·조각·반짝이·[퀘스트 시작].
        // 자리는 카드 왼쪽 위 기준 시안 px이다. 돌고 커지는 부품은 가운데를 축으로 둔다.
        private static QuestUnlockEffect BuildUnlock(QuestBoardView view, RectTransform list)
        {
            RectTransform toast = Ui.Rect("UnlockToast", list);
            toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(.5f, 1);
            toast.anchoredPosition = new Vector2(0, 108) * Px;
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
                Art("QuestCardGlow")
            );
            var glowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            glowGroup.blocksRaycasts = false;
            RectTransform veil = Ui.Rect("Veil", unlock);
            Ui.Fill(veil);
            Image veilFace = veil.gameObject.AddComponent<Image>();
            Ui.Sliced(veilFace, Art("QuestMapMask", Vector4.one * Mathf.Ceil(26 * ArtPixels)));
            veilFace.color = new Color(1, 1, 1, .35f);
            veilFace.raycastTarget = false;
            Vector2 lockCentre = new Vector2(CardWidth, CardHeight) / 2;
            RectTransform closedLock = Centred(
                unlock,
                "ClosedLock",
                lockCentre,
                new Vector2(64, 64),
                (16, 10, 16, 26),
                Art("QuestLock")
            );
            RectTransform openLock = Centred(
                unlock,
                "OpenLock",
                lockCentre,
                new Vector2(64, 64),
                (16, 10, 16, 26),
                Art("QuestLockOpen")
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
                    new Vector2(spot.x, spot.y),
                    new Vector2(spot.w * 26 / 22, spot.h * 20 / 16),
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
                float scale = spot.size / 26;
                RectTransform spark = Centred(
                    unlock,
                    "Spark",
                    new Vector2(spot.x + 21.22f * scale / 2, spot.y + 26 * scale / 2),
                    new Vector2(41.22f, 46) * scale,
                    (0, 0, 0, 0),
                    Art("QuestSpark")
                );
                spark.gameObject.SetActive(false);
                sparks.GetArrayElementAtIndex(i).objectReferenceValue = spark;
            }

            // [퀘스트 시작]: 보상 줄 오른쪽에 0.9배로 둔다.
            Button start = ArtButton(
                unlock,
                "CardStart",
                244.06f,
                255.5f,
                112.94f,
                45,
                (22 * .9f, 12 * .9f, 22 * .9f, 40 * .9f),
                Art("QuestStartButton")
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

            RectTransform dot = Mark(map, "Dot", 9, 9, Art("QuestPathDot"));
            dot.gameObject.SetActive(false);
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
            Set(fields, "dotTemplate", dot);
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
