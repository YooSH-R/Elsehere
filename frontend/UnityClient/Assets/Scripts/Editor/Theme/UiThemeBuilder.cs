using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
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
        private const string HomeScenePath = "Assets/Scenes/Production/HomeScene.unity";
        private const string FontFolder = "Assets/Art/UI/Fonts";

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
                // 지도: 바탕·인도·도로·점선·이름·갈 곳, 그리고 집·카페·호텔·편의점 칸
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

        /// <summary>대화·피드백·단어장·미션·튜토리얼·단어 카드 화면 프리팹에 테마 역할을 붙이고 기본 테마로 칠한다.</summary>
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
        }

        /// <summary>집 씬에 테마 고르기 화면을 둔다. 이미 있으면 그대로 둔다.</summary>
        public static void ConnectPicker(SceneGroups groups)
        {
            if (BuilderTools.FindSingleInScene<ThemePickerView>(groups.Scene) != null)
                return;
            BuilderTools
                .InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(PickerPath, MenuPath),
                    groups.UI,
                    Vector3.zero,
                    Quaternion.identity
                )
                .name = "ThemePicker";
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
        /// 오른쪽 위에는 단어장 아이콘 버튼만 두고, 알림은 알림이 있을 때만 보이는 작은 카드에 담는다.
        /// </summary>
        private static void StyleHud(Transform root)
        {
            PillButton(root, "HomeButton", UiIcon.Home, UiTone.Neutral);
            Transform panel = root.Find("Panel");
            if (panel == null)
            {
                Debug.LogWarning("단어장 HUD 패널이 없습니다: " + root.name);
                return;
            }
            var oldRole = panel.GetComponent<ThemedElement>();
            if (oldRole != null)
                Object.DestroyImmediate(oldRole);
            panel.GetComponent<Image>().color = new Color(1, 1, 1, 0);
            panel.GetComponent<Image>().raycastTarget = false;
            Transform button = root.Find("Panel/Buttons/VocabularyButton");
            Transform oldFace = button.Find("Face");
            if (oldFace != null)
                Object.DestroyImmediate(oldFace.gameObject);
            PillButton(root, "Panel/Buttons/VocabularyButton", UiIcon.Book, UiTone.Neutral);
            var vocabularySize = button.GetComponent<LayoutElement>();
            vocabularySize.minWidth = vocabularySize.preferredWidth = 180;
            vocabularySize.preferredHeight = 72;
            var vocabularyLabel = button.Find("Label").GetComponent<TMP_Text>();
            vocabularyLabel.fontSize = 24;
            vocabularyLabel.fontStyle = FontStyles.Bold;
            StyleHistory(root);
            StyleMyPage(root);
            FitPanelToButtons((RectTransform)panel);

            RectTransform card = FindOrCreateRect(panel, "MessageCard");
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = false;
            var cardLayout = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(card.gameObject);
            cardLayout.padding = new RectOffset(24, 24, 16, 22);
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandHeight = false;
            Role(card, UiThemeRole.Card);
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
            if (button == null)
                return;
            TMP_Text count = Badge(button, "CountBadge");
            BuilderTools.SetReference(view, "vocabularyCountText", count);
        }

        /// <summary>[지난 대화]를 [단어장]과 같은 알약 버튼으로 바꾸고, 지난 대화 창을 단어장 화면처럼 칠한다.</summary>
        private static void StyleHistory(Transform root)
        {
            if (!StyleHudPill(root, "HistoryButton", UiIcon.Talk, 200))
                return;
            Role(root, "History", UiThemeRole.Panel);
            Role(root, "History/Title", UiThemeRole.Title);
            Role(root, "History/Hint", UiThemeRole.Muted);
            Role(root, "History/List", UiThemeRole.Body);
            Role(root, "History/Status", UiThemeRole.Muted);
            IconButton(
                root,
                "History/Buttons/PreviousButton",
                UiIcon.Previous,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "History/Buttons/NextButton",
                UiIcon.Next,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "History/Buttons/OpenButton",
                UiIcon.Detail,
                UiTone.Primary,
                caption: true
            );
            IconButton(
                root,
                "History/Buttons/CloseButton",
                UiIcon.Close,
                UiTone.Neutral,
                caption: true
            );
        }

        /// <summary>[내 정보]를 [단어장]과 같은 알약 버튼으로 버튼 줄 끝에 두고, 마이페이지 창을 칠한다.</summary>
        private static void StyleMyPage(Transform root)
        {
            if (!StyleHudPill(root, "MyPageButton", UiIcon.Detail, 160))
                return;
            Role(root, "MyPage/Window", UiThemeRole.Panel);
            Role(root, "MyPage/Window/Title", UiThemeRole.Title);
            Role(root, "MyPage/Window/Profile", UiThemeRole.Body);
            Role(root, "MyPage/Window/NicknameRow/NicknameInput", UiThemeRole.Input);
            Role(root, "MyPage/Window/Status", UiThemeRole.Muted);
            QuietButton(root, "MyPage/Window/NicknameRow/NicknameButton", "닉네임 변경");
            QuietButton(root, "MyPage/Window/Buttons/LogoutButton", "로그아웃");
            QuietButton(root, "MyPage/Window/Buttons/CloseButton", "닫기");
            Role(root, "MyPage/LogoutDialog", UiThemeRole.Panel);
            Role(root, "MyPage/LogoutDialog/Title", UiThemeRole.Title);
            Role(root, "MyPage/LogoutDialog/Body", UiThemeRole.Body);
            Role(root, "MyPage/LogoutDialog/Status", UiThemeRole.Muted);
            QuietButton(root, "MyPage/LogoutDialog/Buttons/CancelButton", "취소");
            QuietButton(root, "MyPage/LogoutDialog/Buttons/ConfirmButton", "로그아웃");
        }

        // 오른쪽 위 버튼 줄의 버튼을 [단어장]과 같은 높이·글자의 알약 버튼으로 바꾼다. 버튼이 없으면 false.
        private static bool StyleHudPill(
            Transform root,
            string buttonName,
            UiIcon icon,
            float width
        )
        {
            string path = "Panel/Buttons/" + buttonName;
            Transform button = root.Find(path);
            if (button == null)
                return false;
            PillButton(root, path, icon, UiTone.Neutral);
            var size = button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.preferredHeight = 72;
            var label = button.Find("Label").GetComponent<TMP_Text>();
            label.fontSize = 24;
            label.fontStyle = FontStyles.Bold;
            return true;
        }

        // 버튼 줄이 잘리지 않게 오른쪽 위 패널 너비를 버튼 줄에 맞춘다.
        private static void FitPanelToButtons(RectTransform panel)
        {
            RectOffset padding = panel.GetComponent<VerticalLayoutGroup>().padding;
            var row = panel.Find("Buttons").GetComponent<HorizontalLayoutGroup>();
            panel.sizeDelta = new Vector2(
                padding.left + padding.right + RowWidth(row),
                panel.sizeDelta.y
            );
        }

        // 오른쪽 정렬한 버튼 줄에서 켜진 버튼의 너비와 간격을 더한다.
        private static float RowWidth(HorizontalLayoutGroup row)
        {
            float width = 0;
            int count = 0;
            foreach (Transform child in row.transform)
            {
                var size = child.GetComponent<LayoutElement>();
                if (!child.gameObject.activeSelf || size == null)
                    continue;
                width += size.preferredWidth;
                count++;
            }
            return width + row.spacing * Mathf.Max(0, count - 1);
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
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Title", UiThemeRole.Title);
            Role(root, "Panel/Hint", UiThemeRole.Muted);
            Role(root, "Panel/List", UiThemeRole.Body);
            IconButton(
                root,
                "Panel/Buttons/PreviousButton",
                UiIcon.Previous,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "Panel/Buttons/NextButton",
                UiIcon.Next,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "Panel/Buttons/LearnedButton",
                UiIcon.Check,
                UiTone.Positive,
                caption: true
            );
            IconButton(
                root,
                "Panel/Buttons/DeleteButton",
                UiIcon.Delete,
                UiTone.Neutral,
                caption: true
            );
            IconButton(
                root,
                "Panel/Buttons/CloseButton",
                UiIcon.Close,
                UiTone.Neutral,
                caption: true
            );
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
            PillButton(root, "Content/Journal/StartButton", UiIcon.Play, UiTone.Primary);
            PillButton(root, "Content/Journal/RestButton", UiIcon.Rest, UiTone.Secondary);
            PillButton(root, "Content/Journal/RetryButton", UiIcon.Retry, UiTone.Neutral);
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
            Stretch((RectTransform)label.transform);
            label.text = caption;
            label.fontSize = 24;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
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
            Role(root, "Panel", UiThemeRole.Card);
            Role(root, "Panel/Word", UiThemeRole.Title);
            Role(root, "Panel/Meaning", UiThemeRole.Accent);
            Role(root, "Panel/Example", UiThemeRole.Muted);
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
        /// 테마 버튼을 오른쪽 위 HUD 버튼 줄([단어장]·[내 정보]) 바로 왼쪽에 단어장 버튼과 같은 크기·높이·간격으로 둔다.
        /// 두 화면은 같은 기준 해상도의 캔버스라, HUD 칸의 위치와 안쪽 여백, 오른쪽 정렬한 버튼 줄의 너비로 자리를 계산한다.
        /// </summary>
        private static void StyleThemeLauncher(Transform root)
        {
            Transform button = root.Find("OpenButton");
            if (button == null)
                return;
            Transform oldFace = button.Find("Face");
            if (oldFace != null)
                Object.DestroyImmediate(oldFace.gameObject);
            PillButton(root, "OpenButton", UiIcon.Palette, UiTone.Neutral);
            var label = button.Find("Label").GetComponent<TMP_Text>();
            label.gameObject.SetActive(true);
            label.text = "테마";
            label.fontSize = 24;
            AlignWithVocabularyButton((RectTransform)button);
        }

        private static void AlignWithVocabularyButton(RectTransform open)
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var panel = hud != null ? (RectTransform)hud.transform.Find("Panel") : null;
            Transform button = panel != null ? panel.Find("Buttons/VocabularyButton") : null;
            var vocabulary = button != null ? button.GetComponent<LayoutElement>() : null;
            if (vocabulary == null)
            {
                Debug.LogWarning("테마 버튼을 맞출 단어장 버튼이 없습니다: " + HudPath);
                return;
            }
            RectOffset padding = panel.GetComponent<VerticalLayoutGroup>().padding;
            var row = panel.Find("Buttons").GetComponent<HorizontalLayoutGroup>();
            float spacing = row.spacing;
            float rowWidth = RowWidth(row);
            var size = new Vector2(vocabulary.preferredWidth, vocabulary.preferredHeight);
            Place(
                open,
                new Vector2(1, 1),
                new Vector2(
                    panel.anchoredPosition.x - padding.right - rowWidth - spacing,
                    panel.anchoredPosition.y - padding.top
                ),
                size
            );
        }

        private static void BuildPicker(Transform root)
        {
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            // 자리는 SetUpPicker가 단어장 버튼에 맞춘다.
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
                "편의점으로 가요."
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
            Place(iconRect, new Vector2(0, 0.5f), new Vector2(22, 2), new Vector2(30, 30));
            iconRect.pivot = new Vector2(0, 0.5f);
            BuilderTools.GetOrAddComponent<Image>(iconRect.gameObject).raycastTarget = false;
            Transform labelTransform = button.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(58, 4);
                labelRect.offsetMax = new Vector2(-20, 0);
                label.fontSize = 22;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.alignment = TextAlignmentOptions.Center;
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

            /// <summary>지도 색. 바탕·인도·도로·점선·이름·갈 곳·집·카페·호텔·편의점 순서.</summary>
            public string[] Map { get; }
        }
    }
}
