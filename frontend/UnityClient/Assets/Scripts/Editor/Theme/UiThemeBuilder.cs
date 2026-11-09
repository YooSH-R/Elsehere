using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
using A305.EditorTools.Learning;
using A305.EditorTools.Mission;
using A305.EditorTools.Npc;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Npc;
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
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string OfferPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string EntryPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string HomeScenePath = "Assets/Scenes/Production/HomeScene.unity";
        private const string FontFolder = "Assets/Art/UI/Fonts";
        private const string HudMenuPath = "Panel/Menu";

        // 오른쪽 위 메뉴(≡) 버튼과 펼친 목록의 크기
        private const float MenuWidth = 300;
        private const float MenuRowHeight = 64;

        // 창의 동작 버튼은 글자가 잘 보이도록 통통한 알약으로 둔다.
        private const float ActionPillHeight = 70;

        // 알약 버튼 왼쪽 끝의 아이콘 자리
        private const float PillIconLeft = 20;
        private const float PillIconSize = 22;

        private static UiThemeCatalog catalog;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            StyleFeaturePrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("화면 디자인을 준비하고 기능 화면에 테마 역할을 붙였습니다.");
        }

        /// <summary>아이콘과 화면 디자인(글래스 팝) 데이터를 준비한다. 씬은 열지 않는다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.EnsureFolder(IconFolder);
            BuilderTools.EnsureFolder(DataFolder);
            IconSet("Icons", outlined: false);
            catalog = GlassPopBuilder.PrepareDesign();
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
            Style(OfferPath, StyleOffer);
            Style(EntryPath, StyleEntry);
        }

        /// <summary>
        /// HUD에 로그인 끊김 안내 창만 덧붙이고 칠한다. 휴대폰 메뉴·퀘스트 카드처럼 다른 Builder가 마무리한 HUD 디자인은
        /// 다시 만들지 않는다. HUD 전체를 다시 만드는 <see cref="ApplyHudMenu"/>도 같은 안내 창을 만든다.
        /// </summary>
        [MenuItem("Tools/A305/UI/Add Session Expired Notice")]
        public static void ApplySessionExpired()
        {
            BuilderTools.ThrowIfPlaying();
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                root =>
                {
                    LearningBuilder.AddSessionExpired(root);
                    StyleSessionExpired(root.transform);
                    foreach (
                        ThemedElement element in root
                            .transform.Find("SessionExpired")
                            .GetComponentsInChildren<ThemedElement>(true)
                    )
                        element.ApplyCurrent();
                }
            );
            AssetDatabase.SaveAssets();
            Debug.Log("HUD에 로그인 끊김 안내 창을 덧붙였습니다.");
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
            Style("Assets/Prefabs/Mission/FirstDayMission.prefab", StyleFirstDay);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 휴대폰 [복습장] 옆에 [이웃] 버튼을 두고 [이웃] 창(S15P31A305-537)을 만든다.
        /// HUD 전체를 다시 칠하면(Apply HUD Menu) 나중에 바꾼 디자인이 되돌아가므로 이 부분만 만든다.
        /// </summary>
        [MenuItem("Tools/A305/UI/Add Neighbors App")]
        public static void ApplyNeighborsApp()
        {
            BuilderTools.ThrowIfPlaying();
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(
                HudPath,
                root =>
                {
                    NeighborsListBuilder.Add(root);
                    StyleNeighbors(root);
                }
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>대화창의 로딩(대사 칸 로딩, 피드백 로딩 창)만 테마로 칠한다. 대화창의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyDialogueWaitingTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style("Assets/Prefabs/Dialogue/DialoguePanel.prefab", StyleDialogueWaiting);
        }

        /// <summary>대화창의 음성 처리 동의 창만 테마로 칠한다. 대화창의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyVoiceConsentDialogTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style("Assets/Prefabs/Dialogue/DialoguePanel.prefab", StyleVoiceConsentDialog);
        }

        /// <summary>대화 제안 화면의 게스트 가입 안내 창만 테마로 칠한다. 제안 화면의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyGuestSignupDialogTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(OfferPath, StyleGuestSignupDialog);
        }

        /// <summary>대화 제안 화면의 NPC 머리 위 이름표만 테마로 칠한다. 제안 화면의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyNpcNameTagTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(OfferPath, StyleNpcNameTags);
        }

        /// <summary>입구의 게임 전 멈춤 화면(게스트 다시 접속·이미 가입한 계정)만 테마로 칠한다. 입구 화면의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyGuestReturnTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(
                EntryPath,
                root =>
                {
                    StyleGuestReturn(root);
                    A305.EditorTools.Onboarding.EntryScreensBuilder.ApplyGuestReturn(root);
                }
            );
        }

        /// <summary>입구 화면의 배경색·로그인 창 정렬과 약관 링크·캐릭터 체크(S15P31A305-417)만 칠한다. 입구 화면의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyEntryGuidance()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(EntryPath, StyleEntryGuidance);
            AssetDatabase.SaveAssets();
        }

        /// <summary>피드백 요약 탭의 표현 줄만 테마로 칠한다. 피드백 화면의 다른 요소는 건드리지 않는다.</summary>
        public static void ApplyFeedbackSituationTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(
                "Assets/Prefabs/Learning/FeedbackScreen.prefab",
                root =>
                {
                    StyleFeedbackSituations(root);
                    FeedbackScreenBuilder.Apply(root);
                }
            );
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
        /// 화면 디자인은 하나만 쓴다. 예전 테마 고르기 화면이 씬에 남아 있으면 지운다.
        /// </summary>
        public static void RemovePicker(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    // 프리팹을 지운 뒤에는 "ThemePicker (Missing Prefab ...)" 이름으로 남는다.
                    if (child != null && child.name.StartsWith("ThemePicker"))
                        Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        [MenuItem("Tools/A305/UI/Apply HUD Menu")]
        public static void ApplyHudMenu()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            LearningBuilder.PrepareHud();
            Style(HudPath, StyleHud);
            Scene home = EditorSceneManager.OpenScene(HomeScenePath);
            RemovePicker(home);
            BuilderTools.SaveScene(home, HomeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("오른쪽 위 메뉴(≡)에 HUD 버튼을 묶었습니다.");
        }

        private static void Style(string path, Action<Transform> style)
        {
            if (
                !BuilderTools.UpdatePrefabIfExists(
                    path,
                    root =>
                    {
                        style(root.transform);
                        MatchInputRows(root.transform);
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
        /// 오른쪽 위에는 휴대폰 버튼만 두고, 누르면 삼성폰 모양 메뉴(단어장·피드백 목록·마을 지도·집으로·내 정보 앱)를 펼친다.
        /// 폰 화면은 <see cref="PhoneMenuBuilder"/>가 만든다. 알림은 알림이 있을 때만 보이는 작은 카드에 담는다.
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

            Transform vocabulary = root.Find(HudMenuPath + "/VocabularyButton");
            StyleHistory(root);
            StyleMyPage(root);
            StyleControlsGuide(root);
            StyleSessionExpired(root);
            StyleSurveyPrompt(root);
            StyleNeighbors(root);

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
            if (vocabulary != null)
                BuilderTools.SetReference(
                    view,
                    "vocabularyCountText",
                    Badge(vocabulary, "CountBadge")
                );
            // 개수 배지는 폰 화면의 [단어장] 앱 오른쪽 위에 둔다.
            PhoneMenuBuilder.Apply(root.gameObject);
        }

        // 메뉴(≡) 버튼. 평소 버튼은 기본 색, 펼친 동안 같은 자리에 보이는 버튼은 강조 색이다.

        /// <summary>피드백 목록 창은 테마 유리 판 위에 확정 시안대로 다시 놓는다(FeedbackListBuilder).</summary>
        private static void StyleHistory(Transform root)
        {
            if (root.Find("History") == null)
                return;
            Role(root, "History", UiThemeRole.Panel);
            A305.EditorTools.Learning.FeedbackListBuilder.Apply(root);
        }

        /// <summary>[이웃] 창은 [피드백 목록]과 같은 테마 유리 판 위에 같은 자리로 놓는다(NeighborsListBuilder).</summary>
        private static void StyleNeighbors(Transform root)
        {
            if (root.Find("Neighbors") == null)
                return;
            Role(root, "Neighbors", UiThemeRole.Panel);
            NeighborsListBuilder.Apply(root);
            ActionPill(root, "Neighbors/RetryButton", UiIcon.Retry, UiTone.Neutral, 200);
            ActionPill(root, "Neighbors/Card/BackButton", UiIcon.None, UiTone.Neutral, 200);
        }

        /// <summary>오른쪽 아래 조작법 카드. 알림 카드처럼 작은 카드 바탕에 제목·조작·안내를 두고, 닫기는 오른쪽 위 X다.</summary>
        private static void StyleControlsGuide(Transform root)
        {
            if (root.Find("ControlsGuide") == null)
                return;
            Role(root, "ControlsGuide", UiThemeRole.Card);
            Role(root, "ControlsGuide/Title", UiThemeRole.Title);
            Role(root, "ControlsGuide/Body", UiThemeRole.Body);
            Role(root, "ControlsGuide/Hint", UiThemeRole.Muted);
            CornerClose(root, "ControlsGuide");
            Transform card = root.Find("ControlsGuide");
            var column = card.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(28, 28, 22, 24);
            column.spacing = 10;
            // 제목 줄을 오른쪽 위 X만큼 높여 첫 조작 줄이 X 아래에서 시작하게 한다.
            Transform title = card.Find("Title");
            BuilderTools.GetOrAddComponent<LayoutElement>(title.gameObject).minHeight = 52;
            title.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
        }

        /// <summary>로그인이 풀렸을 때의 안내 창. 다른 창과 같은 창 모양에 강조 알약 버튼 [처음 화면으로] 하나만 둔다.</summary>
        private static void StyleSessionExpired(Transform root)
        {
            if (root.Find("SessionExpired/Window") == null)
                return;
            Role(root, "SessionExpired/Window", UiThemeRole.Panel);
            Role(root, "SessionExpired/Window/Title", UiThemeRole.Title);
            Role(root, "SessionExpired/Window/Body", UiThemeRole.Body);
            // 무엇이 일어났고 무엇을 하면 되는지 알리는 긴 문장이라 창 안에서 줄을 바꾼다.
            root.Find("SessionExpired/Window/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            ActionPill(
                root,
                "SessionExpired/Window/Buttons/EntryButton",
                UiIcon.Home,
                UiTone.Primary,
                260
            );
            Roomy(root, "SessionExpired/Window");
        }

        // 다른 창과 같은 창 바탕에 제목·안내, 강조색 [설문 하러 가기], 오른쪽 위 공용 X.
        private static void StyleSurveyPrompt(Transform root)
        {
            if (root.Find("SurveyPrompt/Card") == null)
                return;
            Role(root, "SurveyPrompt/Card", UiThemeRole.Panel);
            Role(root, "SurveyPrompt/Card/Title", UiThemeRole.Title);
            Role(root, "SurveyPrompt/Card/Body", UiThemeRole.Body);
            root.Find("SurveyPrompt/Card/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            ActionPill(
                root,
                "SurveyPrompt/Card/Buttons/OpenButton",
                UiIcon.None,
                UiTone.Primary,
                240
            );
            CornerClose(root, "SurveyPrompt/Card");
            Roomy(root, "SurveyPrompt/Card");
        }

        /// <summary>
        /// HUD 프리팹에 화면 위 가운데의 설문 카드만 덧붙이고 칠한다. HUD의 다른 구성은 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/A305/UI/Add Survey Prompt")]
        public static void ApplySurveyPrompt()
        {
            BuilderTools.ThrowIfPlaying();
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            BuilderTools.UpdatePrefabIfExists(
                HudPath,
                root =>
                {
                    LearningBuilder.AddSurveyPrompt(root);
                    StyleSurveyPrompt(root.transform);
                    foreach (
                        ThemedElement element in root
                            .transform.Find("SurveyPrompt")
                            .GetComponentsInChildren<ThemedElement>(true)
                    )
                        element.ApplyCurrent();
                }
            );
            AssetDatabase.SaveAssets();
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
            ConsentCheck(root.Find(rowPath + "/Toggle"));
            Role(root, rowPath + "/Label", UiThemeRole.Body);
            ActionPill(root, rowPath + "/ViewButton", UiIcon.None, UiTone.Neutral, 110);
        }

        /// <summary>
        /// 가입 두 단계(약관 동의·가입 정보, S15P31A305-596)만 칠하고 시안 창으로 배치한 뒤 동의 체크를 맞춘다. 이미 시안 창이 된 다른 입구 화면은
        /// 건드리지 않는다(입구 전체 테마는 시안 배치 전의 세로 줄 배치를 전제로 한다).
        /// </summary>
        public static void ApplyTermsStepTheme()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style(
                EntryPath,
                root =>
                {
                    StyleTermsStep(root);
                    // 시안 배치가 체크 동그라미(Box)를 줄 가운데에 두므로, 새로 만든 [전체 동의] 줄에 먼저 체크 모양을 입힌다.
                    ConsentCheck(root.Find("TermsPanel/AllAgreeRow/Toggle"));
                    A305.EditorTools.Onboarding.EntryScreensBuilder.ApplyTermsStep(root);
                }
            );
            ApplyConsentChecks();
        }

        // 약관 줄·[전체 동의]·[다음]·[뒤로]는 시안 배치(EntryScreensBuilder)가 그림과 글자 모양을 모두 입힌다.
        // 가입 정보 창에서 옮겨 온 약관 줄은 이미 시안 모양이라 테마 버튼 모양을 다시 입히지 않는다.
        private static void StyleTermsStep(Transform root)
        {
            Role(root, "TermsPanel", UiThemeRole.Panel);
            Role(root, "TermsPanel/Title", UiThemeRole.Title);
            Role(root, "TermsPanel/VoiceConsentNote", UiThemeRole.Muted);
        }

        /// <summary>약관·선택 동의 줄의 체크만 시안 모양으로 바꾼다. 입구 화면과 내 정보 창의 다른 요소는 건드리지 않는다.</summary>
        [MenuItem("Tools/A305/UI/Apply Consent Checks")]
        public static void ApplyConsentChecks()
        {
            BuilderTools.ThrowIfPlaying();
            Style(
                EntryPath,
                root =>
                {
                    ConsentCheck(root.Find("TermsPanel/Consents/ConsentRowTemplate/Toggle"));
                    ConsentCheck(root.Find("TermsPanel/AllAgreeRow/Toggle"));
                    ConsentCheck(root.Find("GuestPanel/VoiceConsentRow/Toggle"));
                }
            );
            Style(
                HudPath,
                root => ConsentCheck(root.Find("MyPage/Window/Consents/ConsentRowTemplate/Toggle"))
            );
            AssetDatabase.SaveAssets();
        }

        // 시안 .check 동그라미(28px)와 그 둘레 그림자 자리(12px). 시안 1px = 캔버스 1.2
        private const float ConsentCheckSize = (28 + 2 * 12) * 1.2f;

        /// <summary>
        /// 동의 체크를 시안 .check처럼 빈 동그라미(꺼짐)와 흰 체크가 든 초록 동그라미(켜짐)로 그린다.
        /// 줄 높이에 늘어나 타원이 되지 않게, 체크 칸은 누르는 자리만 두고 가운데에 정해진 크기의 그림을 얹는다.
        /// </summary>
        private static void ConsentCheck(Transform cell)
        {
            var toggle = cell.GetComponent<Toggle>();
            var theme = cell.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            var area = cell.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(cell.gameObject);
            size.minWidth = size.preferredWidth = 28 * 1.2f;
            size.flexibleWidth = 0;
            Transform oldMark = cell.Find("Checkmark");
            if (oldMark != null)
                Object.DestroyImmediate(oldMark.gameObject);

            var box = (RectTransform)cell.Find("Box");
            if (box == null)
            {
                box = new GameObject(
                    "Box",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<RectTransform>();
                box.SetParent(cell, false);
            }
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = Vector2.one * ConsentCheckSize;
            var off = box.GetComponent<Image>();
            off.sprite = OnboardingArt("ConsentCheckOff");
            off.color = Color.white;

            var mark = (RectTransform)box.Find("Check");
            if (mark == null)
            {
                mark = new GameObject(
                    "Check",
                    typeof(RectTransform),
                    typeof(Image)
                ).GetComponent<RectTransform>();
                mark.SetParent(box, false);
            }
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = Vector2.one;
            mark.offsetMin = mark.offsetMax = Vector2.zero;
            var on = mark.GetComponent<Image>();
            on.sprite = OnboardingArt("ConsentCheckOn");
            on.color = Color.white;
            on.raycastTarget = false;

            toggle.targetGraphic = off;
            toggle.graphic = on;
            toggle.transition = Selectable.Transition.None;
        }

        // 시안 HTML을 렌더링한 입구 화면 그림을 크기를 줄이지 않고 가져온다.
        private static Sprite OnboardingArt(string name)
        {
            string path = $"Assets/Art/UI/Onboarding/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            OfferShortcutsBuilder.SetUp(root.gameObject);
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
            if (root.Find("Panel/Remaining") != null)
                Role(root, "Panel/Remaining", UiThemeRole.Muted);
            StyleTalkPromptHints(root);
            StyleGuestSignupDialog(root);
            StyleNpcNameTags(root);
        }

        [MenuItem("Tools/A305/UI/Update Talk Prompt Hints")]
        public static void UpdateTalkPromptHints()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                OfferPath,
                root => StyleTalkPromptHints(root.transform)
            );
            AssetDatabase.SaveAssets();
        }

        private static void StyleTalkPromptHints(Transform root)
        {
            Transform prompt = root.Find("TalkPrompt");
            var column = prompt.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.childForceExpandWidth = false;
            column.spacing = 12;

            var view = root.GetComponent<PasserbyOfferView>();
            var fields = new SerializedObject(view);
            var remaining = (TMP_Text)
                fields.FindProperty("talkRemainingLabel").objectReferenceValue;
            RectTransform badge = FindOrCreateRect(prompt, "RemainingBadge");
            badge.SetSiblingIndex(0);
            remaining.transform.SetParent(badge, false);
            StyleRemainingBadge(badge, remaining);
            badge.gameObject.SetActive(remaining.gameObject.activeSelf);
            BuilderTools.SetReference(view, "talkRemainingBadge", badge.gameObject);

            Transform row = prompt.Find("Buttons");
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            var rowSize = BuilderTools.GetOrAddComponent<LayoutElement>(row.gameObject);
            rowSize.preferredWidth = 332;
            // 단축키는 버튼 문구에 붙인다. 이전에 별도로 두었던 키 안내는 지운다.
            foreach (string name in new[] { "KeyHint", "KeyHintBadge" })
            {
                Transform old = row.Find(name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            var talk = (RectTransform)row.Find("TalkButton");
            var talkSize = talk.GetComponent<LayoutElement>();
            talkSize.minWidth = talkSize.preferredWidth = 332;
            talk.sizeDelta = new Vector2(332, talk.sizeDelta.y);

            RectTransform key = StyleInteractionKeyHint(talk);
            BuilderTools.SetReference(view, "talkKeyHint", key);
        }

        internal static RectTransform StyleInteractionKeyHint(Transform button)
        {
            RectTransform key = FindOrCreateRect(button, "KeyHint");
            key.SetAsFirstSibling();
            key.sizeDelta = new Vector2(32, 32);
            BuilderTools.GetOrAddComponent<LayoutElement>(key.gameObject).ignoreLayout = true;
            var keyBorder = BuilderTools.GetOrAddComponent<Image>(key.gameObject);
            keyBorder.sprite = TownMapBuilder.RoundedSprite();
            keyBorder.type = Image.Type.Sliced;
            keyBorder.color = Color.white;
            keyBorder.raycastTarget = false;
            // 아래 여백을 더 두어 실제 키보드 키처럼 얇은 두께가 보이게 한다.
            RectTransform face = FindOrCreateRect(key, "Face");
            Stretch(face);
            face.offsetMin = new Vector2(2, 4);
            face.offsetMax = new Vector2(-2, -2);
            var keyFace = BuilderTools.GetOrAddComponent<Image>(face.gameObject);
            keyFace.sprite = TownMapBuilder.RoundedSprite();
            keyFace.type = Image.Type.Sliced;
            keyFace.color = new Color32(190, 90, 70, 255);
            keyFace.raycastTarget = false;
            return key;
        }

        // 안내는 바닥 무늬와 테마에 관계없이 읽혀야 하므로 불투명 바탕과 고정 대비를 쓴다.
        private static void StyleRemainingBadge(RectTransform badge, TMP_Text label)
        {
            var theme = label.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            Color ink = new Color32(40, 56, 80, 255);
            var background = BuilderTools.GetOrAddComponent<Image>(badge.gameObject);
            background.sprite = TownMapBuilder.RoundedSprite();
            background.type = Image.Type.Sliced;
            background.color = ink;
            background.raycastTarget = false;
            var outline = BuilderTools.GetOrAddComponent<Outline>(badge.gameObject);
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2, -2);
            var layout = BuilderTools.GetOrAddComponent<LayoutElement>(badge.gameObject);
            layout.minWidth = layout.preferredWidth = 300;
            layout.minHeight = layout.preferredHeight = 44;
            layout.flexibleWidth = 0;
            badge.sizeDelta = new Vector2(300, 44);
            label.font = UiBuilderTools.BoldFont();
            label.fontSize = 22;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = Vector4.zero;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14, 6);
            label.rectTransform.offsetMax = new Vector2(-14, -6);
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
                    "GuestPanel",
                    "TermsPanel",
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

            // 로그인 메인은 확정 시안(A안) 그림으로 따로 만든다. 개발용 로그인만 알약 버튼으로 칠한다.
            ActionPill(root, "LoginPanel/DevLoginButton", UiIcon.None, UiTone.Neutral, 480);

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
            ActionPill(root, "SignupPanel/BackButton", UiIcon.Previous, UiTone.Neutral, 280);
            ActionPill(root, "SignupPanel/SubmitButton", UiIcon.Check, UiTone.Primary, 280);

            // 가입의 첫 단계(S15P31A305-596): [전체 동의]·약관 줄·음성 안내·[다음]
            ConsentRow(root, "TermsPanel/AllAgreeRow");
            ConsentRow(root, "TermsPanel/Consents/ConsentRowTemplate");
            Role(root, "TermsPanel/VoiceConsentNote", UiThemeRole.Muted);
            ActionPill(root, "TermsPanel/NextButton", UiIcon.None, UiTone.Primary, 280);

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
            StyleGuestReturn(root);
            StyleEntryGuidance(root);
            A305.EditorTools.Onboarding.LoginScreenBuilder.Apply(root);
            A305.EditorTools.Onboarding.EntryScreensBuilder.Apply(root);
        }

        /// <summary>
        /// 입구 화면 배경을 테마의 엷은 강조색으로 깐다. 로그인 메인의 글자·약관 링크는 <see cref="A305.EditorTools.Onboarding.LoginScreenBuilder"/>가 맡는다.
        /// 고르는 캐릭터 버튼에는 고른 것에만 보이는 체크 배지를 단다(S15P31A305-417).
        /// </summary>
        private static void StyleEntryGuidance(Transform root)
        {
            Role(root, "Background", UiThemeRole.Backdrop);
            foreach (
                string avatar in new[]
                {
                    "GuestPanel/AvatarRow/FemaleButton",
                    "GuestPanel/AvatarRow/MaleButton",
                    "SignupPanel/AvatarRow/FemaleButton",
                    "SignupPanel/AvatarRow/MaleButton",
                }
            )
                SelectedCheck(root.Find(avatar));
        }

        // 고른 캐릭터 버튼 오른쪽 위의 체크 배지. 처음에는 숨기고, 화면 코드(AvatarButtons)가 고른 버튼에만 켠다.
        private static void SelectedCheck(Transform button)
        {
            if (button.Find("Check") != null)
                return;
            RectTransform check = Rect("Check", button);
            check.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(check, Vector2.one, new Vector2(10, 10), new Vector2(36, 36));
            check.gameObject.AddComponent<Image>().raycastTarget = false;
            Role(check, UiThemeRole.Badge);
            RectTransform icon = Rect("Icon", check);
            Stretch(icon, 7);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = IconSprite(UiIcon.Check, outlined: false);
            iconImage.raycastTarget = false;
            check.gameObject.SetActive(false);
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
        /// <summary>
        /// 같은 줄에 놓인 입력칸과 버튼은 높이를 하나로 맞춘다. 입력칸 높이를 같은 줄에서 가장 높은 버튼에 맞춘다.
        /// </summary>
        private static void MatchInputRows(Transform root)
        {
            foreach (ThemedElement element in root.GetComponentsInChildren<ThemedElement>(true))
            {
                if (element.Role != UiThemeRole.Input || element.transform.parent == null)
                    continue;
                float height = 0f;
                foreach (Transform sibling in element.transform.parent)
                {
                    if (sibling == element.transform || sibling.GetComponent<Button>() == null)
                        continue;
                    var layout = sibling.GetComponent<LayoutElement>();
                    float buttonHeight =
                        layout != null && layout.preferredHeight > 0
                            ? layout.preferredHeight
                            : ((RectTransform)sibling).rect.height;
                    height = Mathf.Max(height, buttonHeight);
                }
                if (height <= 0f)
                    continue;
                var size = BuilderTools.GetOrAddComponent<LayoutElement>(element.gameObject);
                size.minHeight = size.preferredHeight = height;
                var rect = (RectTransform)element.transform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

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

        /// <summary>닫기를 창 오른쪽 위의 공용 X(<see cref="UiCloseButton"/>)로 옮긴다. 다시 실행해도 같은 자리에 둔다.</summary>
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
            UiCloseButton.Corner(button, UiCloseButton.WindowSize);
        }

        private static void StyleFeedback(Transform root)
        {
            UiCloseButton.Corner(root.Find("Panel/TopCloseButton"), UiCloseButton.WindowSize);
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
            string savePath =
                root.Find("Panel/PageCard/BodyTile/SaveButton") != null
                    ? "Panel/PageCard/BodyTile/SaveButton"
                    : "Panel/Buttons/SaveButton";
            QuietButton(root, savePath, "담기");
            QuietButton(root, "Panel/Buttons/DetailButton", "자세히");
            QuietButton(root, "Panel/Buttons/RegenerateButton", "다시 만들기");
            QuietButton(root, "Panel/Buttons/TranscriptButton", "대화 기록");
            QuietButton(root, "Panel/Buttons/VocabularyButton", "단어장");
            StyleFeedbackSituations(root);
            // 머리줄·탭·표현 칸·본문 칸·아래 버튼은 확정 시안 그림으로 다시 놓는다.
            FeedbackScreenBuilder.Apply(root);
        }

        private static void StyleFeedbackSituations(Transform root)
        {
            const string rows = "Panel/PageCard/Situations";
            Role(root, rows + "/Header", UiThemeRole.Accent);
            Role(root, rows + "/SituationRowTemplate", UiThemeRole.Input);
            Role(root, rows + "/SituationRowTemplate/Texts/Phrase", UiThemeRole.Body);
            Role(root, rows + "/SituationRowTemplate/Texts/Usage", UiThemeRole.Muted);
            PillButton(
                root,
                rows + "/SituationRowTemplate/SaveButton",
                UiIcon.Save,
                UiTone.Primary
            );
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
            // 퀘스트 카드와 오른쪽 위 시계·코인 칩은 퀘스트 카드 Builder가 칠한다.
            A305.EditorTools.Mission.QuestCardBuilder.Apply(root.gameObject);
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
            UiCloseButton.Corner(
                root.Find("Content/Journal/CloseButton"),
                UiCloseButton.WindowSize
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

        // 대화창 바탕·이름·대사·입력 줄·도구 버튼은 상자 없는 그라데이션 대화창 Builder가 칠한다. 여기서는 도움 카드만 칠한다.
        private static void StyleDialogue(Transform root)
        {
            Role(root, "HelpPanel", UiThemeRole.Panel);
            Role(root, "HelpPanel/Title", UiThemeRole.Title);
            Role(root, "HelpPanel/Viewport/Content/WordsCard", UiThemeRole.Card);
            Role(root, "HelpPanel/Viewport/Content/WordsCard/Title", UiThemeRole.Title);
            Role(root, "HelpPanel/Viewport/Content/WordsCard/SuggestWords", UiThemeRole.Body);
            A305.EditorTools.Dialogue.DialogueDockBuilder.Apply(root.gameObject);
            StyleDialogueWaiting(root);
            StyleVoiceConsentDialog(root);
        }

        private static void StyleDialogueWaiting(Transform root)
        {
            // 대답 기다림은 대사 자리에 바탕 없이 흰 글씨로 보인다.
            Role(root, "Panel/Conversation/History/Wait/Row/Label", UiThemeRole.TitleOnDark);
            Role(root, "FeedbackWait/Card", UiThemeRole.Panel);
            Role(root, "FeedbackWait/Card/Title", UiThemeRole.Title);
            Role(root, "FeedbackWait/Card/Body", UiThemeRole.Muted);
        }

        // HUD의 시계·코인과 같은 둥근 유리 칩에 이름을 제목 글꼴로 쓴다. 직원 역할 칩은 Builder가 정한 옅은 파랑 그대로 둔다.
        private static void StyleNpcNameTags(Transform root)
        {
            if (root.Find("NameTags/TagTemplate") == null)
                return;
            Role(root, "NameTags/TagTemplate", UiThemeRole.Chip);
            Role(root, "NameTags/TagTemplate/Name", UiThemeRole.Title);
        }

        // 음성 처리 동의 창과 같은 모양. 주요 동작인 [Google로 가입하기]만 강조색으로 둔다.
        private static void StyleGuestSignupDialog(Transform root)
        {
            if (root.Find("GuestSignup/Dialog") == null)
                return;
            Role(root, "GuestSignup/Dialog", UiThemeRole.Panel);
            Role(root, "GuestSignup/Dialog/Title", UiThemeRole.Title);
            Role(root, "GuestSignup/Dialog/Body", UiThemeRole.Body);
            // 가입하면 무엇이 이어지는지 알리는 긴 문장이라 창 안에서 줄을 바꾼다.
            root.Find("GuestSignup/Dialog/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            ActionPill(
                root,
                "GuestSignup/Dialog/Buttons/CloseButton",
                UiIcon.None,
                UiTone.Neutral,
                140
            );
            ActionPill(
                root,
                "GuestSignup/Dialog/Buttons/SignupButton",
                UiIcon.None,
                UiTone.Primary,
                240
            );
            Roomy(root, "GuestSignup/Dialog");
        }

        // 다른 입구 창처럼 기본 테마 창으로 칠하고, 주요 동작인 가입만 강조색으로 둔다. 창 그림과 자리는 EntryScreensBuilder가 시안 창으로 바꾼다.
        private static void StyleGuestReturn(Transform root)
        {
            if (root.Find("GuestReturnPanel") == null)
                return;
            Role(root, "GuestReturnPanel", UiThemeRole.Panel);
            Role(root, "GuestReturnPanel/Title", UiThemeRole.Title);
            Role(root, "GuestReturnPanel/Body", UiThemeRole.Body);
            root.Find("GuestReturnPanel/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            Role(root, "GuestReturnPanel/Notice", UiThemeRole.Muted);
            ActionPill(root, "GuestReturnPanel/SignupButton", UiIcon.None, UiTone.Primary);
            ActionPill(root, "GuestReturnPanel/ContinueButton", UiIcon.None, UiTone.Neutral);
        }

        // 내 정보의 로그아웃·탈퇴 확인 창과 같은 모양. 주요 동작인 [동의하고 켜기]만 강조색으로 둔다.
        private static void StyleVoiceConsentDialog(Transform root)
        {
            if (root.Find("VoiceConsent/Dialog") == null)
                return;
            Role(root, "VoiceConsent/Dialog", UiThemeRole.Panel);
            Role(root, "VoiceConsent/Dialog/Title", UiThemeRole.Title);
            Role(root, "VoiceConsent/Dialog/Body", UiThemeRole.Body);
            // 음성을 어디에 쓰는지 알리는 긴 문장이라 창 안에서 줄을 바꾼다.
            root.Find("VoiceConsent/Dialog/Body").GetComponent<TMP_Text>().textWrappingMode =
                TextWrappingModes.Normal;
            Role(root, "VoiceConsent/Dialog/Status", UiThemeRole.Muted);
            ActionPill(
                root,
                "VoiceConsent/Dialog/Buttons/DocumentButton",
                UiIcon.Detail,
                UiTone.Neutral,
                170
            );
            ActionPill(
                root,
                "VoiceConsent/Dialog/Buttons/CloseButton",
                UiIcon.None,
                UiTone.Neutral,
                140
            );
            ActionPill(
                root,
                "VoiceConsent/Dialog/Buttons/AgreeButton",
                UiIcon.Mic,
                UiTone.Primary,
                210
            );
            Roomy(root, "VoiceConsent/Dialog");
        }

        /// <summary>튜토리얼 안내 카드에만 테마를 다시 입힌다. 카드 구성을 바꾼 튜토리얼 Builder가 부른다.</summary>
        public static void StyleTutorialGuide()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style("Assets/Prefabs/Tutorial/TutorialGuide.prefab", StyleTutorial);
            Style("Assets/Prefabs/Tutorial/CoachMark.prefab", StyleCoachMark);
        }

        // 코치 마크 말풍선의 [다음]은 안내 카드의 주 버튼과 같은 모양으로 둔다.
        private static void StyleCoachMark(Transform root)
        {
            Role(root, "Caption", UiThemeRole.Card);
            Role(root, "Caption/Text", UiThemeRole.Body);
            PillButton(root, "Caption/NextButton", UiIcon.None, UiTone.Primary);
        }

        /// <summary>코치 마크만 갱신할 때 쓰는 테마 연결.</summary>
        public static void StyleTutorialCoachMark()
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            Style("Assets/Prefabs/Tutorial/CoachMark.prefab", StyleCoachMark);
        }

        // 한 줄 안내 카드: 진행 점은 흐리게, 할 일 한 줄은 제목처럼 진하게. 건너뛰기 확인도 같은 카드 안에 그린다.
        private static void StyleTutorial(Transform root)
        {
            Role(root, "Panel", UiThemeRole.Panel);
            Role(root, "Panel/Content/Header/Title", UiThemeRole.Muted);
            Role(root, "Panel/Content/Task", UiThemeRole.Title);
            PillButton(root, "Panel/Content/Header/SkipButton", UiIcon.Skip, UiTone.Neutral);
            PillButton(root, "Panel/Content/Buttons/ContinueButton", UiIcon.Play, UiTone.Positive);
            Role(root, "Panel/SkipConfirm/Question", UiThemeRole.Title);
            PillButton(
                root,
                "Panel/SkipConfirm/ConfirmButtons/KeepGoingButton",
                UiIcon.Play,
                UiTone.Positive
            );
            PillButton(
                root,
                "Panel/SkipConfirm/ConfirmButtons/ConfirmSkipButton",
                UiIcon.Skip,
                UiTone.Neutral
            );
        }

        private static void StyleTooltip(Transform root)
        {
            VocabularyCardsBuilder.ApplyTooltip(root);
        }

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
        /// <summary>복습장의 시작 버튼도 서비스 공용 알약 버튼을 사용한다.</summary>
        public static void StyleReviewButton(Transform root, string path, UiTone tone)
        {
            catalog = BuilderTools.LoadRequiredAsset<UiThemeCatalog>(CatalogPath, MenuPath);
            PillButton(root, path, UiIcon.None, tone);
        }

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
            Place(
                iconRect,
                new Vector2(0, 0.5f),
                new Vector2(PillIconLeft, 0),
                new Vector2(PillIconSize, PillIconSize)
            );
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
                        new Color32(0x2D, 0x3A, 0x5A, 0xFF)
                    );
                    Object.DestroyImmediate(plain);
                    return bordered;
                },
                Vector4.zero
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
    }
}
