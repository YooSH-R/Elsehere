using System.Linq;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.EditorTools.Weather;
using A305.EditorTools.World;
using A305.Mission;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Mission
{
    public static class FirstDayMissionBuilder
    {
        private const string ViewPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
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

        private static void AddLiveMiniMap(GameObject root)
        {
            Transform content = root.transform.Find("Content");
            Transform journalMap = content?.Find("Journal/TownMap");
            if (journalMap == null)
                throw new System.InvalidOperationException("수첩의 마을 지도부터 생성해야 합니다.");

            Transform old = content.Find("MiniMap");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform panel = Rect("MiniMap", content);
            Anchor(panel, Vector2.zero, new Vector2(24, 24), new Vector2(252, 264));
            Image glass = panel.gameObject.AddComponent<Image>();
            glass.sprite = TownMapBuilder.RoundedSprite();
            glass.type = Image.Type.Sliced;
            glass.color = new Color32(169, 219, 255, 138);
            glass.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .9f);
            outline.effectDistance = new Vector2(2, 2);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.27f, .44f, .58f, .2f);
            shadow.effectDistance = new Vector2(0, -5);

            TMP_Text title = Text(panel, "Title", 17, "마을 지도");
            title.font = UiBuilderTools.BoldFont();
            title.color = new Color32(51, 70, 88, 255);
            Anchor(
                (RectTransform)title.transform,
                new Vector2(0, 1),
                new Vector2(20, -10),
                new Vector2(155, 32)
            );

            Button expand = Button(panel, "Expand", "↗", 30);
            RectTransform expandRect = (RectTransform)expand.transform;
            Anchor(expandRect, Vector2.one, new Vector2(-13, -12), new Vector2(30, 30));
            expand.GetComponent<Image>().color = new Color(1, 1, 1, .42f);
            TMP_Text expandLabel = expand.GetComponentInChildren<TMP_Text>();
            expandLabel.fontSize = 22;
            expandLabel.color = new Color32(75, 100, 121, 255);
            expandLabel.alignment = TextAlignmentOptions.Center;

            GameObject map = Object.Instantiate(journalMap.gameObject, panel);
            map.name = "TownMap";
            RectTransform mapRect = (RectTransform)map.transform;
            Anchor(mapRect, Vector2.zero, new Vector2(13, 41), new Vector2(226, 176));
            CanvasGroup mapGlass = map.AddComponent<CanvasGroup>();
            mapGlass.alpha = .74f;
            mapGlass.blocksRaycasts = false;
            foreach (TMP_Text label in map.GetComponentsInChildren<TMP_Text>(true))
                label.gameObject.SetActive(false);

            TMP_Text footer = Text(
                panel,
                "Destination",
                13,
                FirstDayMissionView.MiniMapLegend(null)
            );
            footer.font = UiBuilderTools.BoldFont();
            footer.color = new Color32(51, 70, 88, 255);
            Anchor(
                (RectTransform)footer.transform,
                Vector2.zero,
                new Vector2(15, 8),
                new Vector2(222, 28)
            );

            FirstDayMissionView view = root.GetComponent<FirstDayMissionView>();
            BuilderTools.SetReference(view, "miniMapPanel", panel.gameObject);
            BuilderTools.SetReference(view, "miniMap", map.GetComponent<TownMapView>());
            BuilderTools.SetReference(view, "miniMapButton", expand);
            BuilderTools.SetReference(view, "miniMapDestination", footer);
        }

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
            if (existing != null && existing.Find("MapButton") != null)
            {
                CompactJournal(existing);
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
            TMP_Text situation = Text(banner, "Situation", 24, "첫날의 작은 소동");
            TMP_Text direction = Text(
                banner,
                "Direction",
                21,
                "지도 버튼을 눌러 여정을 확인하세요."
            );
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
            Text(journal, "Title", 26, "첫날의 작은 소동");
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
            Button start = Button(journal, "StartButton", "여정 시작", 300);
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
            var close = (RectTransform)journal.Find("CloseButton");
            close.GetComponent<LayoutElement>().ignoreLayout = true;
            close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
            close.anchoredPosition = new Vector2(-24, -24);
            close.sizeDelta = new Vector2(48, 48);
            Transform oldOutline = close.Find("Outline");
            if (oldOutline != null)
                Object.DestroyImmediate(oldOutline.gameObject);
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
