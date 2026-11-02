using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
using A305.EditorTools.Learning;
using A305.EditorTools.Mission;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 집에서 고르는 화면 테마 3개(말랑 젤리·스티커 팝·구름 유리)를 만든다.
    /// 제목 글꼴·아이콘·창과 버튼 그림·테마 데이터를 준비하고, 기존 화면 프리팹에 테마 역할을 붙인 뒤 집 씬에 테마 고르기 화면을 둔다.
    /// 그림과 테마 데이터는 없을 때만 만들어 디자인 수정을 덮어쓰지 않는다.
    /// </summary>
    public static class UiThemeBuilder
    {
        public const string MenuPath = "Tools/A305/UI/Build Themes";
        private const string ArtFolder = "Assets/Art/UI/Theme";
        private const string IconFolder = ArtFolder + "/Icons";
        private const string DataFolder = "Assets/Data/Theme";
        private const string CatalogPath = DataFolder + "/UiThemeCatalog.asset";
        private const string PickerPath = "Assets/Prefabs/Theme/ThemePicker.prefab";
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string OfferPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string EntryPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string HomeScenePath = "Assets/Scenes/Production/HomeScene.unity";
        private const string FontFolder = "Assets/Art/UI/Fonts";
        private const string HudMenuPath = "Panel/Menu";

        // 오른쪽 위 메뉴(≡) 버튼과 펼친 목록의 크기
        private const float MenuButtonSize = 78;
        private const float MenuWidth = 300;
        private const float MenuRowHeight = 64;

        // 창의 동작 버튼은 글자가 잘 보이도록 통통한 알약으로 둔다.
        private const float ActionPillHeight = 70;

        // 주의: 테마 순서는 UiThemeId 순서와 같다.
        private static readonly ThemeSpec[] Specs =
        {
            new ThemeSpec(
                UiThemeId.Jelly,
                "말랑 젤리",
                "말랑한 사탕 버튼과 크림색 창",
                "Assets/ThirdParty/Jua/Jua-Regular.ttf",
                "#4A3B33",
                "#7A6352",
                "#B8461F",
                Color.white,
                Color.white,
                new[] { "#FFF8EC", "#FFCB52", "#FF8A5B", "#69B4F0", "#7CC48A" },
                new[] { "#4A3B33", "#4A3B33", "#FFFFFF", "#FFFFFF", "#FFFFFF" },
                "#E8453C",
                outlinedIcons: false,
                // 지도: 바탕·인도·도로·점선·이름·갈 곳, 그리고 집·카페·호텔·드럭스토어 칸
                map: new[]
                {
                    "#FFF3DC",
                    "#F5DDB3",
                    "#A9B4CF",
                    "#FFFFFF",
                    "#FFFFFF",
                    "#FF5A4E",
                    "#F2A65A",
                    "#7CC48A",
                    "#69B4F0",
                    "#FF8A5B",
                }
            ),
            new ThemeSpec(
                UiThemeId.Sticker,
                "스티커 팝",
                "굵은 테두리 스티커와 말풍선",
                "Assets/ThirdParty/DoHyeon/DoHyeon-Regular.ttf",
                "#2D3A5A",
                "#55607A",
                "#C8452A",
                Color.white,
                Color.white,
                new[] { "#FFFFFF", "#FFD54A", "#FF7A59", "#5AA9E6", "#4CC9A6" },
                new[] { "#2D3A5A", "#2D3A5A", "#2D3A5A", "#2D3A5A", "#2D3A5A" },
                "#FF7A59",
                outlinedIcons: true,
                map: new[]
                {
                    "#FFFFFF",
                    "#FFE8B0",
                    "#6B7896",
                    "#FFD54A",
                    "#FFFFFF",
                    "#FF4F5E",
                    "#FFB547",
                    "#4CC9A6",
                    "#5AA9E6",
                    "#FF7A59",
                }
            ),
            new ThemeSpec(
                UiThemeId.Cloud,
                "구름 유리",
                "하늘빛 반투명 창과 둥근 아이콘",
                "Assets/ThirdParty/Sunflower/Sunflower-Bold.ttf",
                "#24384F",
                "#4F6378",
                "#2D6FB5",
                new Color(1, 1, 1, 0.9f),
                new Color(1, 1, 1, 0.94f),
                new[] { "#FFFFFF", "#3D8BD9", "#FF8F6B", "#DCEEFF", "#8FD6B5" },
                new[] { "#24384F", "#FFFFFF", "#FFFFFF", "#24384F", "#24384F" },
                "#FF8F6B",
                outlinedIcons: false,
                map: new[]
                {
                    "#EAF4FF",
                    "#FFFFFF",
                    "#BCD0EC",
                    "#FFFFFF",
                    "#FFFFFF",
                    "#FF8F6B",
                    "#F6A97C",
                    "#6CC59A",
                    "#6FA8E8",
                    "#F48FA3",
                }
            ),
        };

        private static UiThemeCatalog catalog;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            StyleFeaturePrefabs();
            Scene home = EditorSceneManager.OpenScene(HomeScenePath);
            ConnectPicker(BuilderTools.EnsureGroups(home));
            BuilderTools.SaveScene(home, HomeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("화면 테마 3개와 집 안 테마 고르기 화면을 준비했습니다.");
        }

        /// <summary>글꼴·아이콘·그림·테마 데이터와 테마 고르기 프리팹을 준비한다. 씬은 열지 않는다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.EnsureFolder(IconFolder + "/Outlined");
            BuilderTools.EnsureFolder(DataFolder);
            UiIconSet plain = IconSet("Icons", outlined: false);
            UiIconSet outlined = IconSet("IconsOutlined", outlined: true);
            var definitions = new List<UiThemeDefinition>();
            foreach (ThemeSpec spec in Specs)
                definitions.Add(Definition(spec, spec.OutlinedIcons ? outlined : plain));
            catalog = BuilderTools.LoadOrCreateAsset<UiThemeCatalog>(CatalogPath);
            var fields = new SerializedObject(catalog);
            SerializedProperty themes = fields.FindProperty("themes");
            themes.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                themes.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BuilderTools.EnsureFolder(Path.GetDirectoryName(PickerPath));
            BuilderTools.UpdateOrCreatePrefab(PickerPath, "ThemePicker", SetUpPicker);
        }

        /// <summary>대화·피드백·단어장·미션·튜토리얼·단어 카드·대화 제안·로그인 화면 프리팹에 테마 역할을 붙이고 기본 테마로 칠한다.</summary>
        public static void StyleFeaturePrefabs()
        {
            if (catalog == null)
                catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(HudPath, StyleHud);
            Style("Assets/Prefabs/Learning/FeedbackScreen.prefab", StyleFeedback);
            Style("Assets/Prefabs/Learning/VocabularyScreen.prefab", StyleVocabulary);
            Style("Assets/Prefabs/Mission/MissionPanel.prefab", StyleMission);
            Style("Assets/Prefabs/Mission/FirstDayMission.prefab", StyleFirstDay);
            Style("Assets/Prefabs/Dialogue/DialoguePanel.prefab", StyleDialogue);
            Style("Assets/Prefabs/Tutorial/TutorialGuide.prefab", StyleTutorial);
            Style("Assets/Prefabs/ObjectHover/WordTooltip.prefab", StyleTooltip);
            Style(PickerPath, StyleThemeLauncher);
            Style(OfferPath, StyleOffer);
            Style(EntryPath, StyleEntry);
        }

        /// <summary>대화 제안·로그인과 가입·피드백 목록·내 정보 창의 바탕과 버튼을 다른 화면과 같은 테마 규칙으로 맞춘다.</summary>
        [MenuItem("Tools/A305/UI/Apply Unified Dialogs")]
        public static void ApplyUnifiedDialogs()
        {
            BuilderTools.ThrowIfPlaying();
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(HudPath, StyleHud);
            Style(OfferPath, StyleOffer);
            Style(EntryPath, StyleEntry);
            AssetDatabase.SaveAssets();
            Debug.Log("대화 제안·로그인·피드백 목록·내 정보 창을 테마 규칙으로 맞췄습니다.");
        }

        [MenuItem("Tools/A305/UI/Apply Approved HUD Buttons")]
        public static void ApplyApprovedButtons()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(HudPath, StyleHud);
            Style(PickerPath, StyleThemeLauncher);
            Style("Assets/Prefabs/Mission/FirstDayMission.prefab", StyleFirstDay);
            AssetDatabase.SaveAssets();
        }

        /// <summary>대화창의 로딩(대사 칸 로딩, 피드백 로딩 창)만 테마로 칠한다. 대화창의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyDialogueWaitingTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style("Assets/Prefabs/Dialogue/DialoguePanel.prefab", StyleDialogueWaiting);
        }

        [MenuItem("Tools/A305/UI/Apply Text Insets And Alignment")]
        public static void ApplyTextInsets()
        {
            ApplyApprovedButtons();
            Style("Assets/Prefabs/Dialogue/DialoguePanel.prefab", StyleDialogue);
            Style("Assets/Prefabs/Learning/FeedbackScreen.prefab", StyleFeedback);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 집 씬에 테마 고르기 화면을 두고(이미 있으면 그대로 둔다), HUD 메뉴의 [테마]를 켜서 고르기 화면을 열게 잇는다.
        /// </summary>
        public static void ConnectPicker(SceneGroups groups)
        {
            var picker = BuilderTools.FindSingleInScene<ThemePickerView>(groups.Scene);
            if (picker == null)
            {
                GameObject placed = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(PickerPath, MenuPath),
                    groups.UI,
                    Vector3.zero,
                    Quaternion.identity
                );
                placed.name = "ThemePicker";
                picker = placed.GetComponent<ThemePickerView>();
            }
            var hud = BuilderTools.FindSingleInScene<LearningHudView>(groups.Scene);
            Transform themeButton =
                hud != null ? hud.transform.Find(HudMenuPath + "/ThemeButton") : null;
            if (themeButton == null)
            {
                Debug.LogWarning(
                    "집 씬 HUD 메뉴에 [테마]가 없어 테마 고르기 화면을 잇지 못했습니다."
                );
                return;
            }
            // 프리팹 인스턴스 안의 켜짐 상태가 씬 변경으로 남도록 직렬화 속성으로 바꾼다.
            var active = new SerializedObject(themeButton.gameObject);
            active.FindProperty("m_IsActive").boolValue = true;
            active.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SetReference(picker, "openButton", themeButton.GetComponent<Button>());
        }

        [MenuItem("Tools/A305/UI/Apply HUD Menu")]
        public static void ApplyHudMenu()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            LearningBuilder.PrepareHud();
            Style(HudPath, StyleHud);
            Style(PickerPath, StyleThemeLauncher);
            Scene home = EditorSceneManager.OpenScene(HomeScenePath);
            ConnectPicker(BuilderTools.EnsureGroups(home));
            BuilderTools.SaveScene(home, HomeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("오른쪽 위 메뉴(≡)에 HUD 버튼을 묶고 집의 [테마]를 연결했습니다.");
        }

        private static void Style(string path, Action<Transform> style)
        {
            if (
                !BuilderTools.UpdatePrefabIfExists(
                    path,
                    root =>
                    {
                        style(root.transform);
                        foreach (
                            ThemedElement element in root.GetComponentsInChildren<ThemedElement>(
                                true
                            )
                        )
                            element.ApplyCurrent();
                    }
                )
            )
                Debug.LogWarning("테마를 붙일 화면 프리팹이 없습니다: " + path);
        }

        /// <summary>
        /// 오른쪽 위에는 메뉴(≡) 버튼만 두고, 누르면 아래로 단어장·피드백 목록·내 정보·(집의) 테마·홈 목록을 펼친다.
        /// 알림은 알림이 있을 때만 보이는 작은 카드에 담는다.
        /// </summary>
        private static void StyleHud(Transform root)
        {
            Transform panel = root.Find("Panel");
            if (panel == null)
            {
                Debug.LogWarning("HUD 패널이 없습니다: " + root.name);
                return;
            }
            var oldRole = panel.GetComponent<ThemedElement>();
            if (oldRole != null)
                Object.DestroyImmediate(oldRole);
            panel.GetComponent<Image>().color = new Color(1, 1, 1, 0);
            panel.GetComponent<Image>().raycastTarget = false;
            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            ((RectTransform)panel).sizeDelta = new Vector2(
                MenuWidth + panelLayout.padding.left + panelLayout.padding.right,
                0
            );
            StyleMenuButton(root, "Panel/MenuBar/MenuButton", UiTone.Neutral);
            StyleMenuButton(root, "Panel/MenuBar/OpenMenuButton", UiTone.Primary);

            Transform menu = root.Find(HudMenuPath);
            if (menu != null)
            {
                // 목록 칸은 레이아웃만 있어 카드 바탕을 칠할 그림을 붙인다.
                BuilderTools.GetOrAddComponent<Image>(menu.gameObject);
                Role(menu, UiThemeRole.Card);
                menu.GetComponent<VerticalLayoutGroup>().spacing = 0;
            }
            Transform vocabulary = MenuItem(root, "VocabularyButton", UiIcon.Book, "단어장");
            MenuItem(root, "HistoryButton", UiIcon.Talk, "피드백 목록");
            MenuItem(root, "MyPageButton", UiIcon.Detail, "내 정보");
            MenuItem(root, "ThemeButton", UiIcon.Palette, "테마");
            MenuItem(root, "HomeButton", UiIcon.Home, "홈");
            MenuDivider(menu);
            StyleHistory(root);
            StyleMyPage(root);

            RectTransform card = FindOrCreateRect(panel, "MessageCard");
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = false;
            var cardLayout = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(card.gameObject);
            cardLayout.padding = new RectOffset(36, 36, 28, 28);
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandHeight = false;
            Role(card, UiThemeRole.Card);
            card.SetAsLastSibling();
            Transform message = panel.Find("Message");
            if (message == null)
                message = card.Find("Message");
            if (message != null)
            {
                message.SetParent(card, false);
                message.gameObject.SetActive(true);
                Role(message, UiThemeRole.Body);
            }
            card.gameObject.SetActive(false);
            var view = root.GetComponent<LearningHudView>();
            BuilderTools.SetReference(view, "messageBox", card.gameObject);
            if (vocabulary == null)
                return;
            // 개수 배지는 [단어장] 줄 오른쪽 끝에 둔다.
            TMP_Text count = Badge(vocabulary, "CountBadge");
            var badge = (RectTransform)count.transform.parent;
            Place(badge, new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(34, 34));
            BuilderTools.SetReference(view, "vocabularyCountText", count);
        }

        // 메뉴(≡) 버튼. 평소 버튼은 기본 색, 펼친 동안 같은 자리에 보이는 버튼은 강조 색이다.
        private static void StyleMenuButton(Transform root, string path, UiTone tone)
        {
            Transform button = IconButton(root, path, UiIcon.Menu, tone, caption: false);
            if (button == null)
                return;
            var size = button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = size.preferredHeight = MenuButtonSize;
        }

        /// <summary>
        /// 메뉴 목록의 한 줄. 바탕 없이 왼쪽 아이콘과 이름만 두고, 누르거나 쓸 수 없을 때는 이름 색으로 알린다.
        /// 예전 알약·아이콘 버튼 모양이 남아 있으면 지운다. 버튼과 기능 연결은 그대로 둔다.
        /// </summary>
        private static Transform MenuItem(Transform root, string name, UiIcon icon, string text)
        {
            Transform button = root.Find(HudMenuPath + "/" + name);
            if (button == null)
            {
                Debug.LogWarning("메뉴 줄로 바꿀 버튼이 없습니다: " + name);
                return null;
            }
            foreach (Shadow shadow in button.GetComponents<Shadow>())
                Object.DestroyImmediate(shadow);
            foreach (string oldPart in new[] { "Face", "Outline" })
            {
                Transform part = button.Find(oldPart);
                if (part != null)
                    Object.DestroyImmediate(part.gameObject);
            }
            var hitArea = button.GetComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = new Color(1, 1, 1, 0);
            hitArea.raycastTarget = true;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.minWidth = size.preferredWidth = -1;
            size.flexibleWidth = 1;
            size.preferredHeight = MenuRowHeight;

            RectTransform iconRect = FindOrCreateRect(button, "Icon");
            Place(iconRect, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(40, 40));
            BuilderTools.GetOrAddComponent<Image>(iconRect.gameObject).raycastTarget = false;
            var label = button.Find("Label").GetComponent<TMP_Text>();
            label.gameObject.SetActive(true);
            label.text = text;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(60, 0);
            labelRect.offsetMax = new Vector2(-44, 0);
            label.fontSize = 26;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();

            var clickable = button.GetComponent<Button>();
            clickable.transition = Selectable.Transition.ColorTint;
            clickable.targetGraphic = label;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.disabledColor = new Color(1, 1, 1, 0.4f);
            clickable.colors = colors;
            Role(button, UiThemeRole.MenuItem, UiTone.Neutral, icon);
            return button;
        }

        // [홈] 위의 얇은 구분선. 목록 버튼 사이에 두어 장소 이동을 다른 기능과 나눈다.
        private static void MenuDivider(Transform menu)
        {
            Transform home = menu != null ? menu.Find("HomeButton") : null;
            if (home == null)
                return;
            RectTransform line = FindOrCreateRect(menu, "Divider");
            // [홈]은 목록 끝이고 구분선은 그 바로 위다.
            line.SetAsLastSibling();
            home.SetAsLastSibling();
            var image = BuilderTools.GetOrAddComponent<Image>(line.gameObject);
            image.color = new Color(0, 0, 0, 0.1f);
            image.raycastTarget = false;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(line.gameObject);
            size.preferredHeight = size.minHeight = 2;
            size.flexibleWidth = 1;
        }

        /// <summary>피드백 목록 창을 단어장 화면처럼 칠한다. 닫기는 오른쪽 위 X, [피드백 보기]는 강조 알약 버튼이다.</summary>
        private static void StyleHistory(Transform root)
        {
            if (root.Find("History") == null)
                return;
            Role(root, "History", UiThemeRole.Panel);
            Role(root, "History/Title", UiThemeRole.Title);
            Role(root, "History/Hint", UiThemeRole.Muted);
            Role(root, "History/List", UiThemeRole.Body);
            Role(root, "History/Status", UiThemeRole.Muted);
            CompactArrow(root, "History/Buttons/PreviousButton", UiIcon.Previous);
            CompactArrow(root, "History/Buttons/NextButton", UiIcon.Next);
            ActionPill(root, "History/Buttons/OpenButton", UiIcon.Detail, UiTone.Primary, 220);
            CornerClose(root, "History");
            Roomy(root, "History");
        }

        /// <summary>마이페이지 창과 로그아웃 확인 창을 칠한다. 닫기는 오른쪽 위 X, 확인 동작은 강조 알약 버튼이다.</summary>
        private static void StyleMyPage(Transform root)
        {
            if (root.Find("MyPage") == null)
                return;
            Role(root, "MyPage/Window", UiThemeRole.Panel);
            Role(root, "MyPage/Window/Title", UiThemeRole.Title);
            Role(root, "MyPage/Window/Profile", UiThemeRole.Body);
            ThemedInput(root, "MyPage/Window/NicknameRow/NicknameInput");
            Role(root, "MyPage/Window/Status", UiThemeRole.Muted);
            Role(root, "MyPage/Window/LookSensitivity/Title", UiThemeRole.Title);
            Role(root, "MyPage/Window/LookSensitivity/Help", UiThemeRole.Muted);
            Role(root, "MyPage/Window/LookSensitivity/Row/Slow", UiThemeRole.Caption);
            Role(root, "MyPage/Window/LookSensitivity/Row/Fast", UiThemeRole.Caption);
            Role(root, "MyPage/Window/LookSensitivity/Row/Value", UiThemeRole.Accent);
            ActionPill(
                root,
                "MyPage/Window/NicknameRow/NicknameButton",
                UiIcon.None,
                UiTone.Neutral,
                210
            );
            ActionPill(
                root,
                "MyPage/Window/Buttons/LogoutButton",
                UiIcon.Exit,
                UiTone.Neutral,
                210
            );
            CornerClose(root, "MyPage/Window");
            Roomy(root, "MyPage/Window");
            // 여백을 넓힌 만큼 창을 넓혀 시점 감도 줄의 [빠르게]와 현재 배율이 겹치지 않게 한다.
            var window = (RectTransform)root.Find("MyPage/Window");
            window.sizeDelta = new Vector2(640, window.sizeDelta.y);
            Role(root, "MyPage/LogoutDialog", UiThemeRole.Panel);
            Role(root, "MyPage/LogoutDialog/Title", UiThemeRole.Title);
            Role(root, "MyPage/LogoutDialog/Body", UiThemeRole.Body);
            Role(root, "MyPage/LogoutDialog/Status", UiThemeRole.Muted);
            ActionPill(
                root,
                "MyPage/LogoutDialog/Buttons/CancelButton",
                UiIcon.None,
                UiTone.Neutral,
                180
            );
            ActionPill(
                root,
                "MyPage/LogoutDialog/Buttons/ConfirmButton",
                UiIcon.Exit,
                UiTone.Primary,
                180
            );
            Roomy(root, "MyPage/LogoutDialog");
            if (root.Find("MyPage/Window/Consents") != null)
            {
                Role(root, "MyPage/Window/ConsentsLabel", UiThemeRole.Caption);
                ConsentRow(root, "MyPage/Window/Consents/ConsentRowTemplate");
            }
            if (root.Find("MyPage/WithdrawDialog") == null)
                return;
            ActionPill(
                root,
                "MyPage/Window/Buttons/WithdrawButton",
                UiIcon.None,
                UiTone.Neutral,
                210
            );
            Role(root, "MyPage/WithdrawDialog", UiThemeRole.Panel);
            Role(root, "MyPage/WithdrawDialog/Title", UiThemeRole.Title);
            Role(root, "MyPage/WithdrawDialog/Body", UiThemeRole.Body);
            // 지우는 내용을 알리는 긴 문장이라 창 안에서 줄을 바꾼다.
            root.Find("MyPage/WithdrawDialog/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            Role(root, "MyPage/WithdrawDialog/Status", UiThemeRole.Muted);
            ActionPill(
                root,
                "MyPage/WithdrawDialog/Buttons/CancelButton",
                UiIcon.None,
                UiTone.Neutral,
                180
            );
            // 되돌릴 수 없는 동작은 강조색 대신 코랄(Secondary)로 구분한다.
            ActionPill(
                root,
                "MyPage/WithdrawDialog/Buttons/ConfirmButton",
                UiIcon.Delete,
                UiTone.Secondary,
                180
            );
            Roomy(root, "MyPage/WithdrawDialog");
        }

        /// <summary>약관·선택 동의 한 줄: 동그란 체크, 본문 글자, 작은 [보기] 알약 버튼.</summary>
        private static void ConsentRow(Transform root, string rowPath)
        {
            Role(root, rowPath + "/Toggle", UiThemeRole.Input);
            Role(root, rowPath + "/Label", UiThemeRole.Body);
            ActionPill(root, rowPath + "/ViewButton", UiIcon.None, UiTone.Neutral, 110);
        }

        /// <summary>NPC가 말을 걸 때의 제안 창을 다른 창과 같은 바탕과 알약 버튼으로 칠하고, [말 걸기]·[택시 세우기]는 버튼만 둔다.</summary>
        private static void StyleOffer(Transform root)
        {
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Hint", UiThemeRole.Muted);
            Role(root, "Panel/Greeting", UiThemeRole.Title);
            ActionPill(root, "Panel/Buttons/AcceptButton", UiIcon.Talk, UiTone.Primary, 220);
            ActionPill(root, "Panel/Buttons/DeclineButton", UiIcon.None, UiTone.Neutral, 220);
            Roomy(root, "Panel");
            // [말 걸기]·[택시 세우기]는 바탕 상자 없이 버튼 하나와 키 안내만 보인다.
            Transform prompt = root.Find("TalkPrompt");
            var promptTheme = prompt.GetComponent<ThemedElement>();
            if (promptTheme != null)
                Object.DestroyImmediate(promptTheme);
            var promptImage = prompt.GetComponent<Image>();
            promptImage.sprite = null;
            promptImage.color = Color.clear;
            promptImage.raycastTarget = false;
            prompt.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            // 바탕이 없으므로 밝은 인도·회색 도로 어디서나 보이게 코랄(Secondary)과 그림자를 준다.
            ActionPill(root, "TalkPrompt/Buttons/TalkButton", UiIcon.Talk, UiTone.Secondary, 260);
            var lift = BuilderTools.GetOrAddComponent<Shadow>(
                root.Find("TalkPrompt/Buttons/TalkButton").gameObject
            );
            lift.effectColor = new Color(0, 0, 0, .28f);
            lift.effectDistance = new Vector2(0, -4);
            Role(root, "TalkPrompt/Buttons/KeyHint", UiThemeRole.Title);
            if (root.Find("Panel/Remaining") != null)
            {
                Role(root, "Panel/Remaining", UiThemeRole.Muted);
                // 바탕이 없는 [말 걸기] 위에서도 읽히도록 진한 글자로 둔다.
                Role(root, "TalkPrompt/Remaining", UiThemeRole.Title);
            }
        }

        /// <summary>
        /// 로그인·캐릭터·가입·언어·난이도·레벨 테스트·연결 안내 창을 기본 테마의 창과 알약 버튼으로 칠한다.
        /// 고르는 캐릭터 버튼과 카드는 카드 모양으로 두고, 실행 중 고른 것만 강조색 쪽으로 칠한다.
        /// </summary>
        private static void StyleEntry(Transform root)
        {
            foreach (
                string panel in new[]
                {
                    "LoginPanel",
                    "GuestPanel",
                    "SignupPanel",
                    "LanguagePanel",
                    "LevelSettingPanel",
                    "LevelTestPanel",
                    "ConnectionPanel",
                }
            )
            {
                Role(root, panel, UiThemeRole.Panel);
                Role(root, panel + "/Title", UiThemeRole.Title);
                Roomy(root, panel);
            }

            Role(root, "LoginPanel/Subtitle", UiThemeRole.Muted);
            Role(root, "LoginPanel/Notice", UiThemeRole.Muted);
            ActionPill(root, "LoginPanel/GoogleButton", UiIcon.None, UiTone.Primary, 280);
            ActionPill(root, "LoginPanel/GuestButton", UiIcon.None, UiTone.Neutral, 280);
            ActionPill(root, "LoginPanel/DevLoginButton", UiIcon.None, UiTone.Neutral, 280);

            // 캐릭터 버튼은 위의 제목·설명과 같은 왼쪽 줄에서 시작한다.
            foreach (string row in new[] { "GuestPanel/AvatarRow", "SignupPanel/AvatarRow" })
                root.Find(row).GetComponent<HorizontalLayoutGroup>().childAlignment =
                    TextAnchor.MiddleLeft;
            QuietButton(root, "GuestPanel/AvatarRow/FemaleButton", "여성");
            QuietButton(root, "GuestPanel/AvatarRow/MaleButton", "남성");
            Role(root, "GuestPanel/Note", UiThemeRole.Muted);
            if (root.Find("GuestPanel/VoiceConsentRow") != null)
            {
                ConsentRow(root, "GuestPanel/VoiceConsentRow");
                Role(root, "GuestPanel/VoiceConsentNote", UiThemeRole.Muted);
            }
            ActionPill(root, "GuestPanel/StartButton", UiIcon.Play, UiTone.Primary, 280);
            ActionPill(root, "GuestPanel/BackButton", UiIcon.Previous, UiTone.Neutral, 280);

            Role(root, "SignupPanel/NicknameLabel", UiThemeRole.Caption);
            ThemedInput(root, "SignupPanel/NicknameRow/NicknameInput");
            ActionPill(
                root,
                "SignupPanel/NicknameRow/CheckNicknameButton",
                UiIcon.None,
                UiTone.Neutral,
                180
            );
            Role(root, "SignupPanel/NicknameHint", UiThemeRole.Muted);
            Role(root, "SignupPanel/AvatarLabel", UiThemeRole.Caption);
            QuietButton(root, "SignupPanel/AvatarRow/FemaleButton", "여성");
            QuietButton(root, "SignupPanel/AvatarRow/MaleButton", "남성");
            Role(root, "SignupPanel/ConsentsLabel", UiThemeRole.Caption);
            ConsentRow(root, "SignupPanel/Consents/ConsentRowTemplate");
            if (root.Find("SignupPanel/VoiceConsentNote") != null)
                Role(root, "SignupPanel/VoiceConsentNote", UiThemeRole.Muted);
            ActionPill(root, "SignupPanel/SubmitButton", UiIcon.Check, UiTone.Primary, 280);

            foreach (string cards in new[] { "LanguagePanel/Cards", "LevelSettingPanel/Cards" })
            {
                Role(root, cards + "/CardTemplate", UiThemeRole.Card);
                Role(root, cards + "/CardTemplate/Title", UiThemeRole.Title);
                Role(root, cards + "/CardTemplate/Description", UiThemeRole.Muted);
                Role(root, cards + "/CardTemplate/Badge", UiThemeRole.Accent);
            }
            ActionPill(root, "LevelSettingPanel/ConfirmButton", UiIcon.Check, UiTone.Primary, 280);

            Role(root, "LevelTestPanel/Body", UiThemeRole.Body);
            Role(root, "LevelTestPanel/SkipNote", UiThemeRole.Muted);
            ActionPill(root, "LevelTestPanel/StartButton", UiIcon.Play, UiTone.Primary, 400);
            ActionPill(root, "LevelTestPanel/SkipButton", UiIcon.Skip, UiTone.Neutral, 400);

            Role(root, "ConnectionPanel/Body", UiThemeRole.Body);
            ActionPill(root, "ConnectionPanel/RetryButton", UiIcon.Retry, UiTone.Primary, 220);
        }

        /// <summary>
        /// 창 아래의 동작 버튼을 알약 버튼으로 칠한다. 창마다 주요 동작 하나만 강조색(Primary)을 쓰고 나머지는 기본색으로 둔다. 예전에 아이콘 버튼으로 칠했던 바탕(Face)은 지우고,
        /// 한 줄에 놓인 버튼들이 같은 높이가 되도록 맞춘다. 같은 창에 함께 놓이는 버튼은 같은 폭을 준다. 너비를 주지 않으면 줄의 너비를 따른다.
        /// </summary>
        private static void ActionPill(
            Transform root,
            string path,
            UiIcon icon,
            UiTone tone,
            float width = 0
        )
        {
            Transform button = root.Find(path);
            if (button == null)
            {
                Debug.LogWarning($"알약 버튼으로 바꿀 버튼이 없습니다: {root.name}/{path}");
                return;
            }
            Transform face = button.Find("Face");
            if (face != null)
                Object.DestroyImmediate(face.gameObject);
            button.Find("Label").gameObject.SetActive(true);
            PillButton(root, path, icon, tone);
            // 통통한 알약: 높이를 키우고 둥근 끝이 맞도록 9칸 나누기 모서리를 넓히며, 아이콘과 글자를 크게 한다.
            button.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            button.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            // 아이콘과 이름을 한 묶음으로 버튼 가운데에 둔다. 테두리(Outline)는 버튼 전체를 덮도록 배치에서 뺀다.
            var group = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(button.gameObject);
            group.childAlignment = TextAnchor.MiddleCenter;
            group.spacing = 8;
            group.padding = new RectOffset(18, 18, 0, 0);
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            BuilderTools
                .GetOrAddComponent<LayoutElement>(button.Find("Outline").gameObject)
                .ignoreLayout = true;
            var iconRect = (RectTransform)button.Find("Icon");
            iconRect.sizeDelta = new Vector2(28, 28);
            var iconSize = BuilderTools.GetOrAddComponent<LayoutElement>(iconRect.gameObject);
            iconSize.preferredWidth = iconSize.minWidth = 28;
            iconSize.ignoreLayout = icon == UiIcon.None;
            var label = button.Find("Label").GetComponent<TMP_Text>();
            ((RectTransform)label.transform).sizeDelta = new Vector2(0, 50);
            label.alignment = TextAlignmentOptions.Midline;
            label.fontStyle = FontStyles.Bold;
            // 이름이 긴 버튼(NPC 이름이 들어간 [말 걸기] 등)은 폭을 넘지 않게 글자만 조금 줄인다.
            label.enableAutoSizing = true;
            label.fontSizeMin = 18;
            label.fontSizeMax = 26;
            // 높이를 정하지 않는 줄에서도 같은 크기가 되도록 레이아웃 값과 직접 크기를 함께 맞춘다.
            var size = button.GetComponent<LayoutElement>();
            var rect = (RectTransform)button;
            size.preferredHeight = size.minHeight = ActionPillHeight;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, ActionPillHeight);
            if (width > 0)
            {
                size.preferredWidth = size.minWidth = width;
                rect.sizeDelta = new Vector2(width, ActionPillHeight);
            }
        }

        /// <summary>
        /// 승인된 대화창·퀘스트 수첩(S15P31A305-336)과 같은 안쪽 여백과 줄 간격을 준다. 창은 내용 높이에 맞춰 늘어나므로
        /// 글자와 버튼이 테두리에 붙지 않는다. 창 안의 버튼 줄도 버튼 사이를 띄운다. 글자와 줄은 창 너비를 채우고,
        /// 줄 밖에 세로로 쌓인 버튼은 창 너비로 늘이지 않고 정한 폭으로 가운데에 둔다.
        /// </summary>
        private static void Roomy(Transform root, string panelPath)
        {
            Transform panel = root.Find(panelPath);
            var column = panel.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 40, 40);
            column.spacing = 20;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childForceExpandWidth = false;
            foreach (Transform child in panel)
            {
                var row = child.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                    row.spacing = 16;
                if (child.GetComponent<Button>() == null)
                    BuilderTools.GetOrAddComponent<LayoutElement>(child.gameObject).flexibleWidth =
                        1;
            }
        }

        /// <summary>단어장 메모 입력칸(S15P31A305-381)처럼 안내 글자를 기울이지 않은 흐린 글자로 두고 글자 안쪽 여백을 준다.</summary>
        private static void ThemedInput(Transform root, string path)
        {
            Transform target = root.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"테마를 붙일 입력칸이 없습니다: {root.name}/{path}");
                return;
            }
            var field = target.GetComponent<TMP_InputField>();
            Role(target, UiThemeRole.Input);
            field.textComponent.margin = new Vector4(16, 8, 16, 8);
            field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            Role(field.textComponent, UiThemeRole.Body);
            var placeholder = (TMP_Text)field.placeholder;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.margin = field.textComponent.margin;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            Role(placeholder, UiThemeRole.Muted);
        }

        /// <summary>닫기를 단어장·테마 고르기 창처럼 창 오른쪽 위의 X 아이콘으로 옮긴다. 다시 실행해도 같은 자리에 둔다.</summary>
        private static void CornerClose(Transform root, string panelPath)
        {
            Transform panel = root.Find(panelPath);
            Transform button = panel.Find("CloseButton");
            if (button == null)
                button = panel.Find("Buttons/CloseButton");
            if (button == null)
            {
                Debug.LogWarning($"닫기 버튼이 없습니다: {root.name}/{panelPath}");
                return;
            }
            button.SetParent(panel, false);
            BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject).ignoreLayout = true;
            IconButton(panel, "CloseButton", UiIcon.Close, UiTone.Neutral, caption: false);
            Place((RectTransform)button, Vector2.one, new Vector2(-22, -22), new Vector2(52, 52));
        }

        private static void StyleFeedback(Transform root)
        {
            IconButton(root, "Panel/TopCloseButton", UiIcon.Close, UiTone.Neutral, caption: false);
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Title", UiThemeRole.Title);
            Role(root, "Panel/Summary", UiThemeRole.Muted);
            Role(root, "Panel/PageCard", UiThemeRole.Card);
            Role(root, "Panel/PageCard/Body", UiThemeRole.Body);
            Role(root, "Panel/PageTitle", UiThemeRole.Title);
            Role(root, "Panel/Navigation/PageNumber", UiThemeRole.Caption);
            Role(root, "Panel/Notice", UiThemeRole.Muted);
            root.Find("Panel").GetComponent<VerticalLayoutGroup>().padding = new RectOffset(
                48,
                48,
                40,
                36
            );
            QuietButton(root, "Panel/Sections/SummaryButton", "요약", selectedTint: true);
            QuietButton(root, "Panel/Sections/CorrectionsButton", "문장", selectedTint: true);
            QuietButton(root, "Panel/Sections/ConversationButton", "회화", selectedTint: true);
            QuietButton(root, "Panel/Sections/ExpressionsButton", "표현", selectedTint: true);
            CompactArrow(root, "Panel/Navigation/PreviousButton", UiIcon.Previous);
            CompactArrow(root, "Panel/Navigation/NextButton", UiIcon.Next);
            QuietButton(root, "Panel/Buttons/SaveButton", "담기");
            QuietButton(root, "Panel/Buttons/DetailButton", "자세히");
            QuietButton(root, "Panel/Buttons/RegenerateButton", "다시 만들기");
            QuietButton(root, "Panel/Buttons/TranscriptButton", "대화 기록");
            QuietButton(root, "Panel/Buttons/VocabularyButton", "단어장");
        }

        private static void StyleVocabulary(Transform root)
        {
            VocabularyCardsBuilder.ApplyVocabulary(root.gameObject);
        }

        private static void StyleMission(Transform root)
        {
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Header", UiThemeRole.Title);
            Role(root, "Panel/Message", UiThemeRole.Muted);
            Role(root, "Panel/Rows/RowTemplate/Title", UiThemeRole.Title);
            Role(root, "Panel/Rows/RowTemplate/Description", UiThemeRole.Muted);
            Role(root, "Panel/Rows/RowTemplate/Status", UiThemeRole.Accent);
            PillButton(
                root,
                "Panel/Rows/RowTemplate/Buttons/StartButton",
                UiIcon.Play,
                UiTone.Primary
            );
        }

        private static void StyleFirstDay(Transform root)
        {
            IconButton(root, "Content/MapButton", UiIcon.Map, UiTone.Primary, caption: false);
            Role(root, "Content/CurrentGoal", UiThemeRole.Panel);
            Role(root, "Content/CurrentGoal/Situation", UiThemeRole.Title);
            Role(root, "Content/CurrentGoal/Direction", UiThemeRole.Body);
            Role(root, "Content/Journal", UiThemeRole.Panel);
            root.Find("Content/Journal").GetComponent<VerticalLayoutGroup>().padding =
                new RectOffset(48, 48, 36, 40);
            Role(root, "Content/Journal/Title", UiThemeRole.Title);
            Role(root, "Content/Journal/Goal", UiThemeRole.Body);
            Role(root, "Content/Journal/Legend/PlayerLabel", UiThemeRole.Caption);
            Role(root, "Content/Journal/Legend/DestinationLabel", UiThemeRole.Caption);
            Role(root, "Content/Journal/Viewport/JourneyContent/Journey", UiThemeRole.Body);
            Role(root, "Content/Journal/Viewport/JourneyContent/Belongings", UiThemeRole.Muted);
            Role(root, "Content/Journal/Message", UiThemeRole.Accent);
            FirstDayMissionBuilder.EnsureActionRow(root.Find("Content/Journal"));
            PillButton(root, "Content/Journal/Actions/StartButton", UiIcon.Play, UiTone.Primary);
            PillButton(root, "Content/Journal/Actions/RestButton", UiIcon.Rest, UiTone.Secondary);
            PillButton(root, "Content/Journal/Actions/RetryButton", UiIcon.Retry, UiTone.Neutral);
            FirstDayMissionBuilder.EnsureActionRow(root.Find("Content/Journal"));
            CompactLabel(root, "Content/Journal/Actions/RetryButton/Label", "재시도");
            IconButton(
                root,
                "Content/Journal/CloseButton",
                UiIcon.Close,
                UiTone.Neutral,
                caption: false
            );
        }

        private static void CompactLabel(Transform root, string path, string caption)
        {
            Transform target = root.Find(path);
            TMP_Text label = target != null ? target.GetComponent<TMP_Text>() : null;
            if (label != null)
                label.text = caption;
        }

        // 읽는 화면의 조작은 같은 크기의 차분한 글자 버튼으로 통일한다.
        private static void QuietButton(
            Transform root,
            string path,
            string caption,
            bool selectedTint = false
        )
        {
            Transform target = root.Find(path);
            if (target == null)
                return;
            foreach (string name in new[] { "Face", "Outline", "Icon" })
            {
                Transform decoration = target.Find(name);
                if (decoration != null)
                    Object.DestroyImmediate(decoration.gameObject);
            }
            Role(target, UiThemeRole.Card);
            Image image = target.GetComponent<Image>();
            image.raycastTarget = true;
            image.pixelsPerUnitMultiplier = 1.7f;
            var button = target.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.95f, .97f, .92f);
            colors.pressedColor = new Color(.82f, .90f, .79f);
            colors.disabledColor = selectedTint
                ? new Color(.78f, .88f, .73f)
                : new Color(1, 1, 1, .4f);
            button.colors = colors;
            TMP_Text label = target.Find("Label").GetComponent<TMP_Text>();
            label.gameObject.SetActive(true);
            Stretch((RectTransform)label.transform, 16);
            label.text = caption;
            label.fontSize = 24;
            label.enableAutoSizing = false;
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Role(label.transform, UiThemeRole.Title);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject);
            size.minWidth = 0;
            size.preferredHeight = 64;
            size.minHeight = 64;
            if (!selectedTint)
                size.preferredWidth = 156;
        }

        private static void CompactArrow(Transform root, string path, UiIcon icon)
        {
            Transform target = root.Find(path);
            Transform oldOutline = target.Find("Outline");
            if (oldOutline != null)
                Object.DestroyImmediate(oldOutline.gameObject);
            IconButton(root, path, icon, UiTone.Neutral, caption: false);
            var size = target.GetComponent<LayoutElement>();
            size.preferredHeight = 56;
            size.preferredWidth = size.minWidth = 56;
        }

        private static void StyleDialogue(Transform root)
        {
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Title", UiThemeRole.Title);
            Role(root, "Panel/Notice", UiThemeRole.Muted);
            Role(root, "Panel/Conversation/History/Viewport/Lines", UiThemeRole.Body);
            Role(root, "HelpPanel", UiThemeRole.Panel);
            Role(root, "HelpPanel/Title", UiThemeRole.Title);
            Role(root, "HelpPanel/Viewport/Content/Guide", UiThemeRole.Muted);
            Role(root, "HelpPanel/Viewport/Content/WordsCard", UiThemeRole.Card);
            Role(root, "HelpPanel/Viewport/Content/WordsCard/Title", UiThemeRole.Title);
            Role(root, "HelpPanel/Viewport/Content/WordsCard/SuggestWords", UiThemeRole.Body);
            Role(root, "HelpPanel/Viewport/Content/HintCard", UiThemeRole.Card);
            Role(root, "HelpPanel/Viewport/Content/HintCard/Title", UiThemeRole.Title);
            Role(root, "HelpPanel/Viewport/Content/HintCard/Body", UiThemeRole.Body);
            CompactLabel(root, "HelpPanel/Viewport/Content/WordsCard/Title", "단어");
            CompactLabel(root, "HelpPanel/Viewport/Content/HintCard/Title", "문장");
            CompactLabel(root, "HelpPanel/Viewport/Content/HintCard/UseHintButton/Label", "넣기");
            CompactLabel(root, "Panel/Conversation/ToolRow/MeaningButton/Label", "뜻");
            CompactLabel(root, "Panel/Conversation/ToolRow/ReplayButton/Label", "듣기");
            PillButton(
                root,
                "HelpPanel/Viewport/Content/HintCard/UseHintButton",
                UiIcon.Example,
                UiTone.Secondary
            );
            Role(root, "Panel/Conversation/Status", UiThemeRole.Accent);
            Role(root, "Panel/Conversation/InputRow/Input", UiThemeRole.Input);
            IconButton(
                root,
                "Panel/Conversation/InputRow/SendButton",
                UiIcon.Send,
                UiTone.Primary,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/ToolRow/ExampleButton",
                UiIcon.Example,
                UiTone.Accent,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/ToolRow/MeaningButton",
                UiIcon.Meaning,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/ToolRow/SaveButton",
                UiIcon.Save,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/InputRow/TalkButton",
                UiIcon.Mic,
                UiTone.Secondary,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/ToolRow/ReplayButton",
                UiIcon.Replay,
                UiTone.Positive,
                caption: true
            );
            IconButton(
                root,
                "Panel/Conversation/ToolRow/HintButton",
                UiIcon.Hint,
                UiTone.Positive,
                caption: true
            );
            IconButton(
                root,
                "Panel/Buttons/RetryButton",
                UiIcon.Retry,
                UiTone.Neutral,
                caption: true
            );
            IconButton(root, "Panel/Buttons/EndButton", UiIcon.Exit, UiTone.Neutral, caption: true);
            A305.EditorTools.Dialogue.DialogueDockBuilder.Apply(root.gameObject);
            StyleDialogueWaiting(root);
        }

        private static void StyleDialogueWaiting(Transform root)
        {
            Role(root, "Panel/Conversation/History/Wait", UiThemeRole.Card);
            Role(root, "Panel/Conversation/History/Wait/Row/Label", UiThemeRole.Title);
            Role(root, "FeedbackWait/Card", UiThemeRole.Panel);
            Role(root, "FeedbackWait/Card/Title", UiThemeRole.Title);
            Role(root, "FeedbackWait/Card/Body", UiThemeRole.Muted);
        }

        private static void StyleTutorial(Transform root)
        {
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Title", UiThemeRole.Title);
            Role(root, "Panel/Body", UiThemeRole.Body);
            Role(root, "Panel/Task", UiThemeRole.Accent);
            IconButton(
                root,
                "Panel/Buttons/SkipButton",
                UiIcon.Skip,
                UiTone.Neutral,
                caption: true
            );
            PillButton(root, "Panel/Buttons/ContinueButton", UiIcon.Play, UiTone.Positive);
        }

        private static void StyleTooltip(Transform root)
        {
            VocabularyCardsBuilder.ApplyTooltip(root);
        }

        // 테마 고르기: 집에서만 보이는 팔레트 버튼과 미리보기 카드 3장.
        private static void SetUpPicker(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, 118, true);
            var view = BuilderTools.GetOrAddComponent<ThemePickerView>(root);
            if (root.transform.Find("OpenButton") == null)
                BuildPicker(root.transform);
            StyleThemeLauncher(root.transform);
            BuilderTools.SetReference(
                view,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    "Assets/Data/Api/ApiSettings.asset",
                    MenuPath
                )
            );
            BuilderTools.SetReference(
                view,
                "openButton",
                root.transform.Find("OpenButton").GetComponent<Button>()
            );
            Transform window = root.transform.Find("Window");
            BuilderTools.SetReference(view, "window", window.gameObject);
            BuilderTools.SetReference(
                view,
                "closeButton",
                window.Find("Panel/Header/CloseButton").GetComponent<Button>()
            );
            BuilderTools.SetReference(
                view,
                "message",
                window.Find("Panel/Message").GetComponent<TMP_Text>()
            );
            var fields = new SerializedObject(view);
            SerializedProperty options = fields.FindProperty("options");
            options.arraySize = Specs.Length;
            for (int i = 0; i < Specs.Length; i++)
            {
                Transform option = window.Find("Panel/Options/" + Specs[i].Id);
                SerializedProperty entry = options.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("theme").enumValueIndex = (int)Specs[i].Id;
                entry.FindPropertyRelative("button").objectReferenceValue =
                    option.GetComponent<Button>();
                entry.FindPropertyRelative("selectedMark").objectReferenceValue = option
                    .Find("Selected")
                    .gameObject;
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (ThemedElement element in root.GetComponentsInChildren<ThemedElement>(true))
                element.ApplyCurrent();
            window.gameObject.SetActive(false);
        }

        /// <summary>
        /// 테마 고르기는 HUD 메뉴의 [테마]로 연다(집 씬에서 ConnectPicker가 잇는다). 예전 오른쪽 위 [테마] 버튼은 숨긴다.
        /// </summary>
        private static void StyleThemeLauncher(Transform root)
        {
            Transform button = root.Find("OpenButton");
            if (button != null)
                button.gameObject.SetActive(false);
        }

        private static void BuildPicker(Transform root)
        {
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            // 예전 열기 버튼. 집 씬에서는 HUD 메뉴의 [테마]로 열어 이 버튼은 숨긴다.
            RectTransform open = Button(root, "OpenButton");
            UiBuilderTools.FindOrCreateText(open, "Label", bold, 15, Color.black, "테마");
            IconButton(root, "OpenButton", UiIcon.Palette, UiTone.Neutral, caption: true);

            RectTransform window = Rect("Window", root);
            Stretch(window);
            window.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.14f, 0.12f, 0.4f);
            RectTransform panel = Rect("Panel", window);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 560));
            panel.gameObject.AddComponent<Image>();
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(40, 40, 32, 36);
            column.spacing = 14;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandHeight = false;
            Role(panel, UiThemeRole.Panel);

            RectTransform header = UiBuilderTools.FindOrCreateButtonRow(panel, "Header");
            header.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                header,
                "Title",
                bold,
                32,
                Color.black,
                "화면 테마 고르기"
            );
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Role(title, UiThemeRole.Title);
            Button(header, "CloseButton");
            IconButton(header, "CloseButton", UiIcon.Close, UiTone.Neutral, caption: false);

            TMP_Text note = UiBuilderTools.FindOrCreateText(
                panel,
                "Description",
                regular,
                19,
                Color.black,
                "고른 테마는 계정에 저장돼서 다른 기기에서도 그대로 보여요."
            );
            Role(note, UiThemeRole.Muted);

            RectTransform row = UiBuilderTools.FindOrCreateButtonRow(panel, "Options");
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.spacing = 22;
            foreach (ThemeSpec spec in Specs)
                Option(row, spec, bold, regular);

            TMP_Text message = UiBuilderTools.FindOrCreateText(
                panel,
                "Message",
                regular,
                18,
                Color.black,
                ""
            );
            Role(message, UiThemeRole.Accent);
        }

        private static void Option(
            Transform row,
            ThemeSpec spec,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform option = Button(row, spec.Id.ToString());
            var size = option.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 292;
            size.preferredHeight = 330;
            var column = option.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(18, 18, 18, 18);
            column.spacing = 8;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandHeight = false;
            Role(option, UiThemeRole.Card);

            // 미리보기는 고른 테마와 상관없이 이 테마로만 칠한다.
            RectTransform preview = Rect("Preview", option);
            preview.gameObject.AddComponent<Image>().raycastTarget = false;
            preview.gameObject.AddComponent<LayoutElement>().preferredHeight = 170;
            Role(preview, UiThemeRole.Panel, fixedTheme: spec.Id);
            RectTransform sample = Button(preview, "SampleButton");
            Object.DestroyImmediate(sample.GetComponent<Button>());
            Place(sample, new Vector2(0, 1), new Vector2(28, -30), new Vector2(68, 68));
            IconButton(
                preview,
                "SampleButton",
                UiIcon.Map,
                UiTone.Primary,
                caption: false,
                fixedTheme: spec.Id
            );
            RectTransform card = Rect("SampleCard", preview);
            Place(card, new Vector2(1, 1), new Vector2(-26, -26), new Vector2(150, 96));
            card.gameObject.AddComponent<Image>().raycastTarget = false;
            Role(card, UiThemeRole.Card, fixedTheme: spec.Id);
            TMP_Text sampleTitle = UiBuilderTools.FindOrCreateText(
                card,
                "Title",
                bold,
                22,
                Color.black,
                "우산을 사요"
            );
            Place(
                (RectTransform)sampleTitle.transform,
                new Vector2(0, 1),
                new Vector2(16, -16),
                new Vector2(128, 30)
            );
            Role(sampleTitle, UiThemeRole.Title, fixedTheme: spec.Id);
            TMP_Text sampleBody = UiBuilderTools.FindOrCreateText(
                card,
                "Body",
                regular,
                15,
                Color.black,
                "드럭스토어로 가요."
            );
            Place(
                (RectTransform)sampleBody.transform,
                new Vector2(0, 1),
                new Vector2(16, -50),
                new Vector2(128, 26)
            );
            Role(sampleBody, UiThemeRole.Body, fixedTheme: spec.Id);

            TMP_Text name = UiBuilderTools.FindOrCreateText(
                option,
                "Name",
                bold,
                28,
                Color.black,
                spec.DisplayName
            );
            Role(name, UiThemeRole.Title, fixedTheme: spec.Id);
            TMP_Text blurb = UiBuilderTools.FindOrCreateText(
                option,
                "Blurb",
                regular,
                17,
                Color.black,
                spec.Description
            );
            Role(blurb, UiThemeRole.Muted);

            RectTransform selected = Rect("Selected", option);
            selected.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(selected, new Vector2(1, 0), new Vector2(-18, 18), new Vector2(44, 44));
            selected.gameObject.AddComponent<Image>().raycastTarget = false;
            Role(selected, UiThemeRole.Badge);
            RectTransform check = Rect("Icon", selected);
            Stretch(check, 8);
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.sprite = IconSprite(UiIcon.Check, outlined: false);
            checkImage.raycastTarget = false;
        }

        // ---- 프리팹 꾸미기 ----

        private static Transform Role(
            Transform root,
            string path,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral
        )
        {
            Transform target = root.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"테마를 붙일 요소가 없습니다: {root.name}/{path}");
                return null;
            }
            Role(target, role, tone);
            return target;
        }

        private static void Role(
            Component target,
            UiThemeRole role,
            UiTone tone = UiTone.Neutral,
            UiIcon icon = UiIcon.None,
            UiThemeId? fixedTheme = null
        )
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            var fields = new SerializedObject(element);
            fields.FindProperty("catalog").objectReferenceValue = catalog;
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.FindProperty("tone").enumValueIndex = (int)tone;
            fields.FindProperty("icon").enumValueIndex = (int)icon;
            fields.FindProperty("fixedTheme").boolValue = fixedTheme.HasValue;
            fields.FindProperty("previewTheme").enumValueIndex = (int)(
                fixedTheme ?? UiThemeId.Jelly
            );
            fields.ApplyModifiedPropertiesWithoutUndo();
            // 창 그림에는 그림자·두께감이 바깥쪽에 들어 있어 내용을 그만큼 안으로 들인다.
            var column = target.GetComponent<VerticalLayoutGroup>();
            if (
                (role == UiThemeRole.Panel || role == UiThemeRole.Card)
                && column != null
                && !fixedTheme.HasValue
            )
                column.padding = new RectOffset(36, 36, 30, 38);
        }

        /// <summary>
        /// 글자 버튼을 아이콘 버튼으로 바꾼다. 버튼 자체는 누르는 영역만 남기고, 위쪽 Face에 바탕·테두리·아이콘을,
        /// 아래에 작은 이름(Label)을 둔다. 이름을 바꾸는 화면 코드는 그대로 동작한다.
        /// </summary>
        private static Transform IconButton(
            Transform root,
            string path,
            UiIcon icon,
            UiTone tone,
            bool caption,
            UiThemeId? fixedTheme = null
        )
        {
            Transform button = root.Find(path);
            if (button == null)
            {
                Debug.LogWarning($"아이콘 버튼으로 바꿀 버튼이 없습니다: {root.name}/{path}");
                return null;
            }
            foreach (Shadow shadow in button.GetComponents<Shadow>())
                Object.DestroyImmediate(shadow);
            var hitArea = button.GetComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = new Color(1, 1, 1, 0);
            hitArea.raycastTarget = true;
            if (button.parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null)
            {
                var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
                size.preferredWidth = caption ? 96 : 72;
                size.preferredHeight = caption ? 94 : 72;
                size.minWidth = size.preferredWidth;
            }

            Transform oldIcon = button.Find("Icon");
            if (oldIcon != null)
                Object.DestroyImmediate(oldIcon.gameObject);
            RectTransform face = FindOrCreateRect(button, "Face");
            face.SetAsFirstSibling();
            if (caption)
                Place(face, new Vector2(0.5f, 1), Vector2.zero, new Vector2(66, 66));
            else
                Stretch(face);
            var faceImage = BuilderTools.GetOrAddComponent<Image>(face.gameObject);
            faceImage.raycastTarget = false;
            // 미리보기용 버튼은 누를 수 없게 Button을 빼 두었다.
            var clickable = button.GetComponent<Button>();
            if (clickable != null)
                clickable.targetGraphic = faceImage;
            RectTransform outline = FindOrCreateRect(face, "Outline");
            Stretch(outline);
            BuilderTools.GetOrAddComponent<Image>(outline.gameObject).raycastTarget = false;
            RectTransform iconRect = FindOrCreateRect(face, "Icon");
            iconRect.anchorMin = new Vector2(0.19f, 0.19f);
            iconRect.anchorMax = new Vector2(0.81f, 0.81f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            BuilderTools.GetOrAddComponent<Image>(iconRect.gameObject).raycastTarget = false;

            Transform labelTransform = button.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.gameObject.SetActive(caption);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = new Vector2(0, 0);
                labelRect.anchorMax = new Vector2(1, 0);
                labelRect.pivot = new Vector2(0.5f, 0);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(8, 24);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = 11;
                label.fontSizeMax = 15;
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
            }
            Role(button, UiThemeRole.IconButton, tone, icon, fixedTheme);
            return button;
        }

        /// <summary>긴 이름이 필요한 버튼은 아이콘을 왼쪽에 둔 알약 모양 버튼으로 바꾼다.</summary>
        private static void PillButton(Transform root, string path, UiIcon icon, UiTone tone)
        {
            Transform button = root.Find(path);
            if (button == null)
            {
                Debug.LogWarning($"알약 버튼으로 바꿀 버튼이 없습니다: {root.name}/{path}");
                return;
            }
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.preferredHeight = 58;
            var clickable = button.GetComponent<Button>();
            if (clickable != null)
                clickable.targetGraphic = button.GetComponent<Image>();
            // 알약 그림의 둥근 끝이 58px 높이 안에 들어가도록 9칸 나누기 모서리를 줄여 그린다.
            button.GetComponent<Image>().pixelsPerUnitMultiplier = 1.7f;
            RectTransform outline = FindOrCreateRect(button, "Outline");
            Stretch(outline);
            outline.SetAsFirstSibling();
            var outlineImage = BuilderTools.GetOrAddComponent<Image>(outline.gameObject);
            outlineImage.raycastTarget = false;
            outlineImage.pixelsPerUnitMultiplier = 1.7f;
            RectTransform iconRect = FindOrCreateRect(button, "Icon");
            Place(iconRect, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(22, 22));
            iconRect.pivot = new Vector2(0, 0.5f);
            BuilderTools.GetOrAddComponent<Image>(iconRect.gameObject).raycastTarget = false;
            Transform labelTransform = button.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(16, 12);
                labelRect.offsetMax = new Vector2(-16, -12);
                label.fontSize = 22;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.margin = Vector4.zero;
                label.alignment = TextAlignmentOptions.Midline;
                label.transform.SetAsLastSibling();
            }
            Role(button, UiThemeRole.PillButton, tone, icon);
        }

        /// <summary>아이콘 버튼 오른쪽 위의 숫자 배지. 숫자 글자를 돌려준다.</summary>
        private static TMP_Text Badge(Transform face, string name)
        {
            RectTransform badge = FindOrCreateRect(face, name);
            Place(badge, new Vector2(1, 1), new Vector2(8, 8), new Vector2(32, 32));
            badge.pivot = new Vector2(1, 1);
            BuilderTools.GetOrAddComponent<Image>(badge.gameObject).raycastTarget = false;
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                badge,
                "Label",
                UiBuilderTools.BoldFont(),
                16,
                Color.white,
                "0"
            );
            Stretch((RectTransform)label.transform);
            label.alignment = TextAlignmentOptions.Center;
            Role(badge, UiThemeRole.Badge);
            badge.gameObject.SetActive(false);
            return label;
        }

        // ---- 그림·글꼴·데이터 ----

        private static UiIconSet IconSet(string name, bool outlined)
        {
            var set = BuilderTools.LoadOrCreateAsset<UiIconSet>($"{DataFolder}/{name}.asset");
            UiIcon[] icons = (UiIcon[])Enum.GetValues(typeof(UiIcon));
            var fields = new SerializedObject(set);
            SerializedProperty sprites = fields.FindProperty("sprites");
            sprites.arraySize = icons.Length;
            foreach (UiIcon icon in icons)
                sprites.GetArrayElementAtIndex((int)icon).objectReferenceValue =
                    icon == UiIcon.None ? null : IconSprite(icon, outlined);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        private static Sprite IconSprite(UiIcon icon, bool outlined)
        {
            string path = outlined
                ? $"{IconFolder}/Outlined/{icon}.png"
                : $"{IconFolder}/{icon}.png";
            return SpriteIfMissing(
                path,
                () =>
                {
                    Texture2D plain = UiIconArt.Paint(icon);
                    if (!outlined)
                        return plain;
                    Texture2D bordered = UiArtCanvas.WithOutline(
                        plain,
                        4.5f,
                        UiThemeArt.Hex("#2D3A5A")
                    );
                    Object.DestroyImmediate(plain);
                    return bordered;
                },
                Vector4.zero
            );
        }

        private static Sprite ThemeSprite(UiThemeId theme, UiThemeArt.Part part)
        {
            UiThemeArt.Art? art = UiThemeArt.Paint(theme, part);
            if (art == null)
                return null;
            string folder = $"{ArtFolder}/{theme}";
            BuilderTools.EnsureFolder(folder);
            return SpriteIfMissing(
                $"{folder}/{part}.png",
                () => art.Value.Texture,
                art.Value.Border
            );
        }

        /// <summary>그림 파일이 없을 때만 그려 저장한다. 디자인한 그림으로 바꾸면 그대로 둔다.</summary>
        private static Sprite SpriteIfMissing(string path, Func<Texture2D> paint, Vector4 border)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;
            Texture2D texture = paint();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static UiThemeDefinition Definition(ThemeSpec spec, UiIconSet icons)
        {
            string path = $"{DataFolder}/{spec.Id}.asset";
            var definition = BuilderTools.LoadOrCreateAsset<UiThemeDefinition>(path);
            var fields = new SerializedObject(definition);
            // 수치와 색은 아직 정하지 않은(이름이 빈) 에셋에만 넣고, 이후에는 Inspector에서 고친 값을 유지한다.
            if (string.IsNullOrEmpty(definition.DisplayName))
            {
                fields.FindProperty("id").enumValueIndex = (int)spec.Id;
                fields.FindProperty("displayName").stringValue = spec.DisplayName;
                fields.FindProperty("description").stringValue = spec.Description;
                fields.FindProperty("ink").colorValue = UiThemeArt.Hex(spec.Ink);
                fields.FindProperty("mutedInk").colorValue = UiThemeArt.Hex(spec.MutedInk);
                fields.FindProperty("accentInk").colorValue = UiThemeArt.Hex(spec.AccentInk);
                fields.FindProperty("panelTint").colorValue = spec.PanelTint;
                fields.FindProperty("cardTint").colorValue = spec.CardTint;
                fields.FindProperty("badgeColor").colorValue = UiThemeArt.Hex(spec.Badge);
                SetColors(fields.FindProperty("toneColors"), spec.Tones);
                SetColors(fields.FindProperty("toneInks"), spec.ToneInks);
            }
            TMP_FontAsset serviceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                ServiceFontBuilder.FontPath
            );
            fields.FindProperty("titleFont").objectReferenceValue =
                serviceFont != null ? serviceFont : TitleFont(spec.FontPath);
            fields.FindProperty("panelSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Panel
            );
            fields.FindProperty("cardSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Card
            );
            fields.FindProperty("inputSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Input
            );
            fields.FindProperty("buttonSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Button
            );
            fields.FindProperty("buttonOutlineSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.ButtonOutline
            );
            fields.FindProperty("pillSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Pill
            );
            fields.FindProperty("pillOutlineSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.PillOutline
            );
            fields.FindProperty("badgeSprite").objectReferenceValue = ThemeSprite(
                spec.Id,
                UiThemeArt.Part.Badge
            );
            fields.FindProperty("icons").objectReferenceValue = icons;
            // 지도 색은 나중에 더한 값이라, 이미 만든 테마에도 비어 있을 때만 넣는다.
            if (definition.TownMap.IsEmpty)
                SetTownMapColors(fields.FindProperty("townMap"), spec.Map);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetTownMapColors(SerializedProperty map, string[] hexes)
        {
            string[] names =
            {
                "ground",
                "pavement",
                "road",
                "roadLine",
                "label",
                "destination",
                "home",
                "cafe",
                "hotel",
                "shop",
            };
            for (int i = 0; i < names.Length; i++)
                map.FindPropertyRelative(names[i]).colorValue = UiThemeArt.Hex(hexes[i]);
        }

        private static void SetColors(SerializedProperty list, string[] hexes)
        {
            list.arraySize = hexes.Length;
            for (int i = 0; i < hexes.Length; i++)
                list.GetArrayElementAtIndex(i).colorValue = UiThemeArt.Hex(hexes[i]);
        }

        /// <summary>
        /// 제목용 동적 글꼴 에셋. 실행 중에 쓰는 글자만 채운다. 이 글꼴에 없는 글자는 Pretendard Bold로 대신 그린다.
        /// </summary>
        private static TMP_FontAsset TitleFont(string sourcePath)
        {
            string assetPath =
                $"{FontFolder}/{Path.GetFileNameWithoutExtension(sourcePath)} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;
            var source = BuilderTools.LoadRequiredAsset<Font>(sourcePath, "테마 글꼴 파일 복사");
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true
            );
            font.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(font, assetPath);
            font.atlasTextures[0].name = $"{font.name} Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = $"{font.name} Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.fallbackFontAssetTable = new List<TMP_FontAsset> { UiBuilderTools.BoldFont() };
            var fields = new SerializedObject(font);
            fields.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            // 아틀라스·재질 하위 에셋을 바로 저장해야 다음 단계에서 글꼴을 쓸 수 있다.
            AssetDatabase.SaveAssets();
            return font;
        }

        // ---- 배치 도구 ----

        private static RectTransform Button(Transform parent, string name)
        {
            RectTransform rect = FindOrCreateRect(parent, name);
            var image = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            BuilderTools.GetOrAddComponent<Button>(rect.gameObject).targetGraphic = image;
            return rect;
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name)
        {
            var existing = (RectTransform)parent.Find(name);
            return existing != null ? existing : Rect(name, parent);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size
        )
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private sealed class ThemeSpec
        {
            public ThemeSpec(
                UiThemeId id,
                string displayName,
                string description,
                string fontPath,
                string ink,
                string mutedInk,
                string accentInk,
                Color panelTint,
                Color cardTint,
                string[] tones,
                string[] toneInks,
                string badge,
                bool outlinedIcons,
                string[] map
            )
            {
                Id = id;
                DisplayName = displayName;
                Description = description;
                FontPath = fontPath;
                Ink = ink;
                MutedInk = mutedInk;
                AccentInk = accentInk;
                PanelTint = panelTint;
                CardTint = cardTint;
                Tones = tones;
                ToneInks = toneInks;
                Badge = badge;
                OutlinedIcons = outlinedIcons;
                Map = map;
            }

            public UiThemeId Id { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public string FontPath { get; }
            public string Ink { get; }
            public string MutedInk { get; }
            public string AccentInk { get; }
            public Color PanelTint { get; }
            public Color CardTint { get; }
            public string[] Tones { get; }
            public string[] ToneInks { get; }
            public string Badge { get; }
            public bool OutlinedIcons { get; }

            /// <summary>지도 색. 바탕·인도·도로·점선·이름·갈 곳·집·카페·호텔·드럭스토어 순서.</summary>
            public string[] Map { get; }
        }
    }
}
