using System.Linq;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.EditorTools.Weather;
using A305.EditorTools.World;
using A305.Mission;
using A305.Player;
using A305.Theme;
using A305.Wayfinding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ui = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Mission
{
    public static class FirstDayMissionBuilder
    {
        internal const string ViewPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private static readonly Color Ink = new Color(.16f, .24f, .2f);
        private static readonly Color Cream = new Color(.98f, .96f, .91f, .96f);
        private const string MenuPath = "Tools/A305/Mission/Build First Day Journey";
        private const string MiniMapMenuPath = "Tools/A305/Mission/Apply Live Mini Map";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            FirstDayShopBuilder.BuildShop();
            TownMapBuilder.Layout townMap = null;
            foreach (
                string name in new[]
                {
                    "HomeScene",
                    "TownScene",
                    "CafeScene",
                    "HotelScene",
                    "ShopScene",
                }
            )
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                SceneGroups groups = BuilderTools.EnsureGroups(scene);
                ConnectToScene(
                    groups,
                    BuilderTools.FindSingleInScene<DialogueController>(scene),
                    BuilderTools.FindSingleInScene<PlayerMover>(scene)
                );
                if (name == "TownScene")
                {
                    FirstDayShopBuilder.ConnectTownEntrance(groups);
                    townMap = TownMapBuilder.Read(scene);
                    QuestNpcSpotBuilder.Apply(groups);
                }
                BuilderTools.SaveScene(scene, path);
            }
            BuilderTools.UpdateOrCreatePrefab(
                ViewPath,
                "FirstDayMission",
                root =>
                {
                    TownMapBuilder.Draw(root, townMap);
                    AddLiveMiniMap(root);
                }
            );
            BuilderTools.AddToSceneList("Assets/Scenes/Production/ShopScene.unity");
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log("First-day journey connected to all five production spaces.");
        }

        [MenuItem(MiniMapMenuPath)]
        public static void ApplyLiveMiniMap()
        {
            BuilderTools.ThrowIfPlaying();
            if (!BuilderTools.UpdatePrefabIfExists(ViewPath, AddLiveMiniMap))
                throw new System.InvalidOperationException("첫날 미션 프리팹이 없습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log("Live mini map added to the first-day mission prefab.");
        }

        /// <summary>이미 만든 큰 지도에 도로 위 길 안내 켜기·끄기 버튼만 다시 둔다. 지도와 미니맵은 그대로 둔다.</summary>
        public static void ApplyRouteToggle()
        {
            BuilderTools.ThrowIfPlaying();
            bool updated = BuilderTools.UpdatePrefabIfExists(
                ViewPath,
                root =>
                    AddRouteToggle(
                        (RectTransform)root.transform.Find("Content/MapOverlay/Panel"),
                        RoundedSprite(GlassSpritePath, 0f)
                    )
            );
            if (!updated)
                throw new System.InvalidOperationException("첫날 미션 프리팹이 없습니다.");
        }

        private static void AddLiveMiniMap(GameObject root)
        {
            Transform content = root.transform.Find("Content");
            if (content == null)
                throw new System.InvalidOperationException("첫날 미션 화면(Content)이 없습니다.");

            foreach (string name in new[] { "MiniMap", "MapOverlay" })
            {
                Transform old = content.Find(name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            Sprite glassSprite = RoundedSprite(GlassSpritePath, 0f);

            // 미니맵을 누르거나 폰 메뉴의 [마을 지도]로 화면 가운데에 펼치는 큰 지도(시안 g7). 미니맵과 같은 유리 판과 마을 지도 그림을 쓴다.
            RectTransform overlay = Rect("MapOverlay", content);
            Stretch(overlay);
            RectTransform outside = Rect("Outside", overlay);
            Stretch(outside);
            Image dim = outside.gameObject.AddComponent<Image>();
            dim.color = new Color(.07f, .11f, .17f, .47f);
            Button close = outside.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;

            // 판(시안 1420 폭, 모서리 52). 지도 그림 전체(2.5:1)가 들어가도록 높이는 시안보다 줄였다. 자리·크기는 시안 px다.
            RectTransform large = Rect("Panel", overlay);
            Anchor(
                large,
                new Vector2(.5f, .5f),
                Vector2.zero,
                new Vector2(LargeMapWidth, LargeMapHeight) * Ui.Px
            );
            Glass(large, 44f / (52 * Ui.Px));

            // 머리줄: 하늘색 지도 앱 아이콘(60), 제목(Jua 36), 범례(17). 그림은 아이콘 그림자 자리를 포함한다.
            RectTransform icon = Ui.Rect("Icon", large);
            Ui.TopLeft(icon, 29 - 14, 25 - 8, 60 + 28, 60 + 30);
            Ui.Picture(icon.gameObject, MiniMapBuilder.Sprite("MapHeaderIcon"));
            TMP_Text title = HeaderText(large, "Title", 105, 35.5f, 240, 39, 36);
            title.text = "마을 지도";
            title.margin = new Vector4(0, 3 * Ui.Px, 0, 0);
            TMP_Text largeLegend = HeaderText(large, "Legend", 274.7f, 25, 560, 60, 17);
            largeLegend.text = FirstDayMissionView.MiniMapLegend(null);

            // 닫기 X는 다른 창과 같은 공용 모양으로 둔다(S15P31A305-525).
            RectTransform closeFace = Rect("CloseButton", large);
            Button closeButton = closeFace.gameObject.AddComponent<Button>();
            UiCloseButton.Corner(closeFace, UiCloseButton.WindowSize);

            // 지도 창(시안 .tile, 모서리 36): 미니맵과 같은 마을 지도 그림 전체를 창 폭에 맞춰 보인다.
            RectTransform window = Ui.Rect("TownMap", large);
            Ui.TopLeft(window, LargeMapInset, LargeMapTop, LargeMapArtWidth, LargeMapArtHeight);
            Image windowShape = window.gameObject.AddComponent<Image>();
            windowShape.sprite = glassSprite;
            windowShape.type = Image.Type.Sliced;
            windowShape.pixelsPerUnitMultiplier = 44f / (36 * Ui.Px);
            windowShape.raycastTarget = false;
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            TownMiniMapView largeMap = MiniMapBuilder.MapLayers(
                window,
                window.gameObject,
                LargeMapArtWidth * Ui.Px / TownMapArt.Size.x
            );
            AddRouteToggle(large, glassSprite);
            overlay.gameObject.SetActive(false);

            // 왼쪽 아래 미니맵은 시안 지도 그림으로 따로 만든다.
            MiniMapBuilder.Apply(root);
            overlay.SetAsLastSibling();

            FirstDayMissionView view = root.GetComponent<FirstDayMissionView>();
            BuilderTools.SetReference(view, "mapOverlay", overlay.gameObject);
            BuilderTools.SetReference(view, "largeMap", largeMap);
            BuilderTools.SetReference(view, "largeMapLegend", largeLegend);
            BuilderTools.SetReference(view, "mapCloseButton", closeButton);
            BuilderTools.SetReference(view, "mapOutsideButton", close);
        }

        // 큰 지도 판과 지도 창(시안 px). 창은 시안 .tile 자리(왼쪽 29, 위 103, 아래 27)에 지도 그림 비율로 둔다.
        private const float LargeMapWidth = 1420;
        private const float LargeMapInset = 29;
        private const float LargeMapTop = 103;
        private const float LargeMapArtWidth = LargeMapWidth - LargeMapInset * 2;
        private static readonly float LargeMapArtHeight =
            LargeMapArtWidth * TownMapArt.Size.y / TownMapArt.Size.x;
        private static readonly float LargeMapHeight = LargeMapTop + LargeMapArtHeight + 27;
        private static readonly Color MapInk = new Color32(40, 56, 80, 255);

        // 시안 자리(판 왼쪽 위 기준 px)에 Jua 글자를 둔다.
        private static TMP_Text HeaderText(
            RectTransform panel,
            string name,
            float x,
            float y,
            float width,
            float height,
            float size
        )
        {
            RectTransform rect = Ui.Rect(name, panel);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Ui.Style(text, Ui.DisplayFontPath, size, Ui.Ink);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Ui.TopLeft(rect, x, y, width, height);
            return text;
        }

        // 뒤 거리가 흐려지지 않고 비치는 하늘색 유리 판과 흰 테두리. 큰 지도와 미니맵이 함께 쓴다.
        // Outline 효과는 판 전체를 흰색으로 겹쳐 그려 반투명이 사라지므로 테두리는 링 그림으로 따로 그린다.
        // cornerScale은 판 그림 모서리(44)를 키우거나 줄이는 비율이다.
        internal static Image Glass(RectTransform panel, float cornerScale = 1f)
        {
            Image glass = panel.gameObject.AddComponent<Image>();
            glass.sprite = RoundedSprite(GlassSpritePath, 0f);
            glass.type = Image.Type.Sliced;
            glass.pixelsPerUnitMultiplier = cornerScale;
            glass.color = new Color32(176, 222, 255, 110);
            RectTransform rim = Rect("Rim", panel);
            Stretch(rim);
            Image rimImage = rim.gameObject.AddComponent<Image>();
            rimImage.sprite = RoundedSprite(GlassRimPath, 3f);
            rimImage.type = Image.Type.Sliced;
            rimImage.pixelsPerUnitMultiplier = cornerScale;
            rimImage.color = new Color(1f, 1f, 1f, .92f);
            rimImage.raycastTarget = false;
            return glass;
        }

        // 머리줄 오른쪽, 닫기 X 왼쪽에 흰 알약으로 도로 위 길 안내를 켜고 끄는 버튼을 둔다. 세로 가운데는 제목 줄(시안 위 55)에 맞춘다.
        private static void AddRouteToggle(RectTransform panel, Sprite sprite)
        {
            Transform old = panel.Find("RouteToggle");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            RectTransform chip = Rect("RouteToggle", panel);
            chip.anchorMin = chip.anchorMax = Vector2.one;
            chip.pivot = new Vector2(1, .5f);
            chip.anchoredPosition = new Vector2(
                -(UiCloseButton.Inset + UiCloseButton.WindowSize + 16),
                -55 * Ui.Px
            );
            chip.sizeDelta = new Vector2(200, 40);
            Image face = chip.gameObject.AddComponent<Image>();
            face.sprite = sprite;
            face.type = Image.Type.Sliced;
            face.color = new Color(1, 1, 1, .86f);
            Button button = chip.gameObject.AddComponent<Button>();
            TMP_Text label = Text(chip, "Label", 19, GuideRouteToggle.Label(true));
            label.font = UiBuilderTools.BoldFont();
            label.color = MapInk;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            Stretch((RectTransform)label.transform);
            var toggle = chip.gameObject.AddComponent<GuideRouteToggle>();
            BuilderTools.SetReference(toggle, "button", button);
            BuilderTools.SetReference(toggle, "label", label);
        }

        /// <summary>
        /// 모서리를 크게 둥글린 흰 판 그림. 테두리 굵기(stroke)가 0이면 속이 찬 판, 아니면 그 굵기의 테두리만 그린다.
        /// 색은 Image가 칠한다. 그림 파일이 있으면 그대로 쓴다.
        /// </summary>
        private static Sprite RoundedSprite(string path, float stroke)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(GlassSpriteFolder);
            const int Size = 128;
            const float Radius = 44f;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = x + .5f;
                    float py = y + .5f;
                    float cx = Mathf.Clamp(px, Radius, Size - Radius);
                    float cy = Mathf.Clamp(py, Radius, Size - Radius);
                    float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float inside = Mathf.Clamp01(Radius - distance + .5f);
                    if (stroke > 0f)
                    {
                        // 판 가장자리에서 stroke만큼 안쪽까지만 칠한다.
                        float edge = Mathf.Min(Mathf.Min(px, Size - px), Mathf.Min(py, Size - py));
                        float depth = distance > 0f ? Radius - distance : edge;
                        inside *= Mathf.Clamp01(stroke - depth + .5f);
                    }
                    byte alpha = (byte)(255 * inside);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(48, 48, 48, 48);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private const string GlassSpriteFolder = "Assets/Art/UI/Mission";
        private const string GlassSpritePath = GlassSpriteFolder + "/GlassPanel.png";
        private const string GlassRimPath = GlassSpriteFolder + "/GlassRim.png";

        public static void PrepareAssets()
        {
            BuilderTools.UpdateOrCreatePrefab(ViewPath, "FirstDayMission", SetUpView);
        }

        public static void ConnectToScene(
            SceneGroups groups,
            DialogueController dialogue,
            PlayerMover player
        )
        {
            var view = BuilderTools.FindSingleInScene<FirstDayMissionView>(groups.Scene);
            if (view == null)
                view = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(ViewPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<FirstDayMissionView>();
            var controller = BuilderTools.FindSingleInScene<FirstDayMissionController>(
                groups.Scene
            );
            if (controller == null)
                controller = BuilderTools
                    .CreateEmpty(
                        "FirstDayMission",
                        groups.Systems,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .gameObject.AddComponent<FirstDayMissionController>();
            BuilderTools.SetReference(
                controller,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    "Assets/Data/Api/ApiSettings.asset",
                    MenuPath
                )
            );
            BuilderTools.SetReference(controller, "view", view);
            BuilderTools.SetReference(controller, "dialogue", dialogue);
            BuilderTools.SetReference(controller, "player", player);
            string sceneCode = groups.Scene.name.Replace("Scene", "").ToUpperInvariant();
            BuilderTools.SetText(controller, "sceneCode", sceneCode);
            var legacy = BuilderTools.FindSingleInScene<MissionController>(groups.Scene);
            if (legacy != null)
                BuilderTools.SetReference(legacy, "firstDayMission", controller);
            if (sceneCode == "TOWN")
                BuilderTools.SetReference(
                    controller,
                    "weather",
                    TownWeatherBuilder.ConnectToScene(groups, player.transform)
                );
            // 튜토리얼 진행에 따라 TutorialDirector가 표시하므로 처음에는 숨긴다.
            view.SetAvailable(false);
        }

        private static void SetUpView(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, 108, true);
            var view = BuilderTools.GetOrAddComponent<FirstDayMissionView>(root);
            Transform existing = root.transform.Find("Content");
            // 지도 버튼 배치나 그 뒤의 퀘스트 카드 배치는 유지하고 카드만 다시 맞춘다.
            if (
                existing != null
                && (
                    existing.Find("MapButton") != null
                    || existing.Find("CurrentGoal/Header") != null
                )
            )
            {
                CompactJournal(existing);
                QuestCardBuilder.Apply(root);
                return;
            }
            // 오른쪽 위 수첩 버튼으로 만든 이전 배치는 단어장 버튼과 겹치므로 지우고 왼쪽 위 지도 버튼 배치로 다시 만든다.
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            RectTransform content = Rect("Content", root.transform);
            Stretch(content);
            RectTransform banner = UiBuilderTools.FindOrCreateAnchoredPanel(
                content,
                "CurrentGoal",
                new Vector2(0, 1),
                640,
                new Vector2(112, -24)
            );
            banner.GetComponent<Image>().color = Cream;
            TMP_Text situation = Text(banner, "Situation", 24, "퀘스트를 불러오는 중이에요.");
            // 할 일 자리 문구는 QuestCardBuilder가 채운다.
            TMP_Text direction = Text(banner, "Direction", 21, "");
            Button journalButton = MapButton(content);
            RectTransform journal = Rect("Journal", content);
            Anchor(
                journal,
                new Vector2(0, 1),
                new Vector2(24, -112),
                new Vector2(TownMapBuilder.DrawerWidth, 900)
            );
            journal.gameObject.AddComponent<Image>().color = Cream;
            var column = journal.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 22, 22);
            column.spacing = 12;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandHeight = false;
            Text(journal, "Title", 26, "퀘스트");
            TMP_Text journalGoal = Text(journal, "Goal", 20, "");
            TownMapBuilder.CreateShell(journal, UiBuilderTools.RegularFont());
            RectTransform viewport = Rect("Viewport", journal);
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scrollSize = viewport.gameObject.AddComponent<LayoutElement>();
            scrollSize.flexibleHeight = 1;
            scrollSize.minHeight = 240;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            RectTransform scrollContent = UiBuilderTools.FindOrCreateColumn(
                viewport,
                "JourneyContent"
            );
            scrollContent.anchorMin = new Vector2(0, 1);
            scrollContent.anchorMax = Vector2.one;
            scrollContent.pivot = new Vector2(.5f, 1);
            scrollContent.sizeDelta = Vector2.zero;
            scrollContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text journey = Text(scrollContent, "Journey", 22, "퀘스트를 불러오는 중이에요.");
            TMP_Text belongings = Text(scrollContent, "Belongings", 20, "");
            scroll.viewport = viewport;
            scroll.content = scrollContent;
            TMP_Text message = Text(journal, "Message", 19, "");
            Button start = Button(journal, "StartButton", "퀘스트 시작", 300);
            Button rest = Button(journal, "RestButton", "카페에서 잠깐 쉬기", 300);
            Button retry = Button(journal, "RetryButton", "다시 불러오기", 300);
            Button close = Button(journal, "CloseButton", "수첩 닫기", 300);
            rest.gameObject.SetActive(false);
            retry.gameObject.SetActive(false);
            BuilderTools.SetReference(view, "content", content.gameObject);
            BuilderTools.SetReference(view, "journal", journal.gameObject);
            BuilderTools.SetReference(view, "situation", situation);
            BuilderTools.SetReference(view, "direction", direction);
            BuilderTools.SetReference(view, "journalGoal", journalGoal);
            BuilderTools.SetReference(
                view,
                "townMap",
                journal.GetComponentInChildren<TownMapView>(true)
            );
            BuilderTools.SetReference(view, "journey", journey);
            BuilderTools.SetReference(view, "belongings", belongings);
            BuilderTools.SetReference(view, "message", message);
            BuilderTools.SetReference(view, "journalButton", journalButton);
            BuilderTools.SetReference(view, "startButton", start);
            BuilderTools.SetReference(view, "restButton", rest);
            BuilderTools.SetReference(view, "retryButton", retry);
            BuilderTools.SetReference(view, "closeButton", close);
            journal.gameObject.SetActive(false);
            CompactJournal(content);
            QuestCardBuilder.Apply(root);
        }

        private static void CompactJournal(Transform content)
        {
            Transform journal = content.Find("Journal");
            var column = journal.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(36, 36, 32, 32);
            column.spacing = 20;
            journal.Find("TownMap").GetComponent<LayoutElement>().preferredHeight = 224;
            journal.Find("Title").GetComponent<TMP_Text>().text = "여행 수첩";
            journal.Find("Title").GetComponent<TMP_Text>().fontSize = 28;
            journal.Find("Legend/PlayerLabel").GetComponent<TMP_Text>().text = "나";
            journal.Find("Legend/DestinationLabel").GetComponent<TMP_Text>().text = "목표";
            foreach (
                TMP_Text label in journal.Find("TownMap").GetComponentsInChildren<TMP_Text>(true)
            )
                if (label.text.StartsWith("CONVENIENCE") || label.text == "SHOP")
                    label.text = "DRUGSTORE";
            var list = journal.Find("Viewport/JourneyContent/Journey").GetComponent<TMP_Text>();
            list.fontSize = 24;
            list.lineSpacing = 44;
            list.paragraphSpacing = 8;
            Transform details = journal.Find("Viewport/JourneyContent");
            var detailLayout = details.GetComponent<VerticalLayoutGroup>();
            detailLayout.padding = new RectOffset(0, 0, 8, 16);
            detailLayout.spacing = 28;
            var items = details.Find("Belongings").GetComponent<TMP_Text>();
            items.lineSpacing = 12;
            var goal = journal.Find("Goal").GetComponent<TMP_Text>();
            goal.fontSize = 22;
            Transform close = journal.Find("CloseButton");
            UiCloseButton.Corner(close, UiCloseButton.WindowSize);
            close.SetAsLastSibling();
            EnsureActionRow(journal);
            journal.Find("Actions/StartButton/Label").GetComponent<TMP_Text>().text = "시작";
            journal.Find("Actions/RestButton/Label").GetComponent<TMP_Text>().text = "쉬기";
            journal.Find("Actions/RetryButton/Label").GetComponent<TMP_Text>().text = "다시 시도";
        }

        /// <summary>기존 버튼 참조와 동작을 유지하며 퀘스트 조작을 한 줄로 모은다.</summary>
        internal static void EnsureActionRow(Transform journal)
        {
            Transform row = journal.Find("Actions");
            if (row == null)
                row = Rect("Actions", journal);
            var layout = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var rowSize = BuilderTools.GetOrAddComponent<LayoutElement>(row.gameObject);
            rowSize.minHeight = rowSize.preferredHeight = 72;
            row.SetAsLastSibling();
            foreach (string name in new[] { "StartButton", "RetryButton", "RestButton" })
            {
                Transform button = journal.Find(name);
                if (button != null)
                    button.SetParent(row, false);
                else
                    button = row.Find(name);
                if (button == null)
                    continue;
                var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
                size.minWidth = size.preferredWidth = 184;
                size.minHeight = size.preferredHeight = 72;
                size.flexibleWidth = 0;
                TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();
                label.font = UiBuilderTools.BoldFont();
                label.fontSize = 24;
                label.fontStyle = FontStyles.Bold;
                var labelRect = (RectTransform)label.transform;
                labelRect.offsetMin = new Vector2(16, 12);
                labelRect.offsetMax = new Vector2(-16, -12);
                label.margin = Vector4.zero;
                label.alignment = TextAlignmentOptions.Midline;
            }
        }

        /// <summary>접은 지도 아이콘 아래에 '지도'를 적은 버튼. 누르면 지도와 여정이 담긴 수첩을 연다.</summary>
        private static Button MapButton(Transform content)
        {
            RectTransform rect = Rect("MapButton", content);
            Anchor(rect, new Vector2(0, 1), new Vector2(24, -24), new Vector2(76, 76));
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = TownMapBuilder.RoundedSprite();
            background.type = Image.Type.Sliced;
            background.color = Cream;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .25f);
            shadow.effectDistance = new Vector2(0, -3);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            RectTransform icon = Rect("Icon", rect);
            Anchor(icon, new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(48, 48));
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = TownMapBuilder.Icon();
            image.preserveAspect = true;
            image.raycastTarget = false;
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                rect,
                "Label",
                UiBuilderTools.BoldFont(),
                17,
                Ink,
                "지도"
            );
            label.alignment = TextAlignmentOptions.Center;
            Anchor(
                (RectTransform)label.transform,
                new Vector2(.5f, 0),
                new Vector2(0, 3),
                new Vector2(76, 22)
            );
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, int size, string value) =>
            UiBuilderTools.FindOrCreateText(
                parent,
                name,
                UiBuilderTools.RegularFont(),
                size,
                Ink,
                value
            );

        private static Button Button(Transform parent, string name, string label, int width)
        {
            Button button = UiBuilderTools.FindOrCreateButton(
                parent,
                name,
                label,
                UiBuilderTools.BoldFont(),
                width
            );
            button.GetComponent<Image>().color = new Color(.22f, .43f, .33f);
            return button;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
