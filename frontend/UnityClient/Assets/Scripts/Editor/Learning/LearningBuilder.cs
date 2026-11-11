using System;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Onboarding;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Onboarding;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BrowserImeInput = WebGLSupport.WebGLInput;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// 피드백·단어장 화면, 오른쪽 위 메뉴(≡), 확인용 참고 표현 규칙을 준비하고 공간 씬에 학습 처리를 붙인다.
    /// 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class LearningBuilder
    {
        public const string MenuPath = "Tools/A305/Learning/Build";

        private const string FeedbackPrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string VocabularyPrefabPath =
            "Assets/Prefabs/Learning/VocabularyScreen.prefab";
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string MockExamplesPath = "Assets/Data/Learning/MockFeedbackExamples.asset";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";

        private static readonly string[] ProductionSpaceScenes =
        {
            "HomeScene",
            "TownScene",
            "CafeScene",
            "HotelScene",
            "ShopScene",
            "PoliceStationScene",
        };

        // 다른 화면 위에 열리도록 대화 화면(120)보다 위에 둔다. 단어장은 피드백에서도 열 수 있어 그 위에 둔다.
        private const int FeedbackSortingOrder = 130;
        private const int VocabularySortingOrder = 135;
        private const int HudSortingOrder = 95;

        // 로그인이 풀렸다는 안내는 대화창·단어장과 가장 위에 그리는 수첩(1000)보다도 위에 둔다.
        private const int SessionExpiredSortingOrder = 2000;

        // 설문 카드는 가입 안내 창 같은 덮개(대화 제안 화면 110)와 수첩(1000) 위, 로그인 끊김 안내 아래에 둔다.
        private const int SurveyPromptSortingOrder = 1500;

        // 화면 위 가장자리에서 카드까지의 여백
        private const float SurveyPromptTopMargin = 24f;

        private const string FeedbackListTitle = "대화 노트";

        private const float ControlsGuideWidth = 460f;

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("피드백·단어장 화면을 준비했습니다.");
        }

        /// <summary>확인용 참고 표현 규칙(없을 때만)과 화면 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.LoadOrCreateAsset<MockFeedbackExampleSource>(MockExamplesPath);
            BuilderTools.UpdateOrCreatePrefab(
                FeedbackPrefabPath,
                "FeedbackScreen",
                SetUpFeedbackScreen
            );
            BuilderTools.UpdateOrCreatePrefab(
                VocabularyPrefabPath,
                "VocabularyScreen",
                SetUpVocabularyScreen
            );
            PrepareHud();
        }

        /// <summary>오른쪽 위 메뉴(≡)와 메뉴 버튼, 대화 노트·내 정보 창이 있는 HUD 프리팹의 기능 구성을 맞춘다.</summary>
        public static void PrepareHud() =>
            BuilderTools.UpdateOrCreatePrefab(HudPrefabPath, "LearningHud", SetUpHud);

        /// <summary>HUD 프리팹 루트에 로그인 끊김 안내 창만 덧붙인다. HUD의 다른 구성은 건드리지 않는다.</summary>
        public static void AddSessionExpired(GameObject hud) =>
            SetUpSessionExpired(hud, UiBuilderTools.BoldFont(), UiBuilderTools.RegularFont());

        /// <summary>HUD 내 정보 창에 NPC 말 빠르기 줄만 덧붙인다. HUD의 다른 구성은 건드리지 않는다.</summary>
        public static void AddNpcSpeechSpeed(GameObject hud) =>
            SetUpNpcSpeechSpeed(
                hud.GetComponent<MyPageView>(),
                (RectTransform)hud.transform.Find("MyPage/Window"),
                UiBuilderTools.BoldFont(),
                UiBuilderTools.RegularFont()
            );

        /// <summary>씬에 피드백·단어장 화면과 학습 처리가 없으면 붙이고 플레이어·대화 처리와 연결한다.</summary>
        public static LearningController ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            DialogueController dialogue
        )
        {
            var feedbackView = FindOrPlaceView<FeedbackView>(groups, FeedbackPrefabPath);
            var vocabularyView = FindOrPlaceView<VocabularyView>(groups, VocabularyPrefabPath);
            var hudView = FindOrPlaceView<LearningHudView>(groups, HudPrefabPath);

            var learning = BuilderTools.FindSingleInScene<LearningController>(groups.Scene);
            if (learning == null)
            {
                learning = BuilderTools
                    .CreateEmpty("Learning", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<LearningController>();
            }

            BuilderTools.SetReference(learning, "player", player);
            BuilderTools.SetReference(learning, "dialogue", dialogue);
            BuilderTools.SetReference(
                learning,
                "exampleSource",
                BuilderTools.LoadRequiredAsset<MockFeedbackExampleSource>(
                    MockExamplesPath,
                    MenuPath
                )
            );
            BuilderTools.SetReference(learning, "feedbackView", feedbackView);
            BuilderTools.SetReference(learning, "vocabularyView", vocabularyView);
            BuilderTools.SetReference(learning, "hudView", hudView);
            ConnectToApi(learning);
            BuilderTools.RequireReferences(learning);
            return learning;
        }

        /// <summary>
        /// 단어장 화면 프리팹에 메모·안내 요소를 맞추고, 배포용 장소 씬의 학습 처리에 API 서버 설정만 연결한다.
        /// 디자인한 씬의 다른 객체는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/A305/Learning/Connect Vocabulary Server")]
        public static void ConnectVocabularyServer()
        {
            BuilderTools.ThrowIfPlaying();
            // 테마를 입힌 다른 학습 화면은 건드리지 않도록 단어장 화면 프리팹만 맞춘다.
            BuilderTools.UpdateOrCreatePrefab(
                VocabularyPrefabPath,
                "VocabularyScreen",
                SetUpVocabularyScreen
            );
            foreach (string name in ProductionSpaceScenes)
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                var learning = BuilderTools.FindSingleInScene<LearningController>(scene);
                if (learning == null)
                {
                    throw new InvalidOperationException(
                        $"{path}에 LearningController가 없습니다. 공간 기능 연결을 먼저 실행합니다."
                    );
                }

                ConnectToApi(learning);
                BuilderTools.SaveScene(scene, path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "단어장 화면을 맞추고 배포용 장소 씬 5개의 학습 처리를 단어장 서버와 연결했습니다."
            );
        }

        private static void ConnectToApi(LearningController learning)
        {
            BuilderTools.SetReference(
                learning,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    ApiSettingsPath,
                    A305.EditorTools.Onboarding.OnboardingBuilder.MenuPath
                )
            );
        }

        private static T FindOrPlaceView<T>(SceneGroups groups, string prefabPath)
            where T : Component
        {
            var view = BuilderTools.FindSingleInScene<T>(groups.Scene);
            if (view != null)
            {
                return view;
            }

            return BuilderTools
                .InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(prefabPath, MenuPath),
                    groups.UI,
                    Vector3.zero,
                    Quaternion.identity
                )
                .GetComponent<T>();
        }

        private static void SetUpFeedbackScreen(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, FeedbackSortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<FeedbackView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "Panel",
                new Vector2(0.5f, 0.5f),
                900f,
                Vector2.zero
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                32f,
                Color.white,
                "오늘도 한 걸음 가까워졌어요"
            );
            TMP_Text summary = UiBuilderTools.FindOrCreateText(
                panel,
                "Summary",
                regular,
                22f,
                SubTextColor
            );
            Transform bodyParent = panel.Find("PageCard/Content");
            if (bodyParent == null)
                bodyParent = panel.Find("PageCard");
            if (bodyParent == null)
                bodyParent = panel;
            TMP_Text body = UiBuilderTools.FindOrCreateText(
                bodyParent,
                "Body",
                regular,
                24f,
                Color.white
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            // 표현·문장마다 [담기]가 있으므로(FeedbackScreenBuilder) 아래 줄의 [표현 저장]은 두지 않는다.
            Button detailButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "DetailButton",
                "상세 보기",
                bold,
                180f
            );
            Button vocabularyButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "VocabularyButton",
                "단어장 보기",
                bold,
                180f
            );
            Button closeButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "CloseButton",
                "닫기",
                bold,
                180f
            );

            // 피드백을 받지 못했을 때만 보인다. 새로 붙이면 [단어장 보기] 앞에 둔다.
            bool isRegenerateNew = buttons.Find("RegenerateButton") == null;
            Button regenerateButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "RegenerateButton",
                "다시 만들기",
                bold,
                180f
            );
            Button transcriptButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "TranscriptButton",
                "대화 기록 보기",
                bold,
                180f
            );
            if (isRegenerateNew)
            {
                int index = vocabularyButton.transform.GetSiblingIndex();
                regenerateButton.transform.SetSiblingIndex(index);
                transcriptButton.transform.SetSiblingIndex(index + 1);
                regenerateButton.gameObject.SetActive(false);
                transcriptButton.gameObject.SetActive(false);
            }

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "titleText", title);
            BuilderTools.SetReference(view, "summaryText", summary);
            BuilderTools.SetReference(view, "bodyText", body);
            BuilderTools.SetReference(view, "detailButton", detailButton);
            BuilderTools.SetReference(
                view,
                "detailButtonLabel",
                detailButton.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(view, "vocabularyButton", vocabularyButton);
            BuilderTools.SetReference(view, "closeButton", closeButton);
            BuilderTools.SetReference(view, "regenerateButton", regenerateButton);
            BuilderTools.SetReference(view, "transcriptButton", transcriptButton);
            BuilderTools.SetReference(
                view,
                "transcriptButtonLabel",
                transcriptButton.GetComponentInChildren<TMP_Text>(true)
            );
            FeedbackPagesBuilder.SetUp(root, view, body);
            FeedbackSituationBuilder.SetUp(root, view);
            BuilderTools.RequireReferences(view);

            // 대화가 끝난 뒤에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        private static void SetUpVocabularyScreen(GameObject root)
        {
            A305.EditorTools.Theme.VocabularyCardsBuilder.ApplyVocabulary(root);
        }

        private static void SetUpHud(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, HudSortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<LearningHudView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "Panel",
                new Vector2(1f, 1f),
                360f,
                new Vector2(-24f, -24f)
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            RectTransform menu = SetUpMenu(root, panel, bold);
            Button vocabularyButton = UiBuilderTools.FindOrCreateButton(
                menu,
                "VocabularyButton",
                "단어장 0",
                bold,
                200f
            );
            TMP_Text message = FindOrCreateMessage(panel, regular);

            BuilderTools.SetReference(view, "vocabularyButton", vocabularyButton);
            BuilderTools.SetReference(
                view,
                "vocabularyButtonLabel",
                vocabularyButton.transform.Find("Label").GetComponent<TMP_Text>()
            );
            Button home = UiBuilderTools.FindOrCreateButton(menu, "HomeButton", "홈", bold, 200f);
            BuilderTools.SetReference(view, "homeButton", home);
            BuilderTools.SetReference(view, "messageText", message);
            // 대화 중에 숨기는 휴대폰 버튼 줄
            BuilderTools.SetReference(view, "phoneButtonBar", panel.Find("MenuBar").gameObject);
            SetUpHistory(root, view, menu, bold, regular);
            SetUpControlsGuide(root, view, menu, bold, regular);
            BuilderTools.RequireReferences(view);

            SetUpMyPage(root, menu, bold, regular);
            // 화면 디자인은 하나만 써서 메뉴에 [테마]를 두지 않는다.
            Transform themeButton = menu.Find("ThemeButton");
            if (themeButton != null)
                UnityEngine.Object.DestroyImmediate(themeButton.gameObject);
            OrderMenu(
                menu,
                "VocabularyButton",
                "HistoryButton",
                "MyPageButton",
                "ControlsButton",
                "Divider",
                "HomeButton"
            );
            SetUpSessionExpired(root, bold, regular);
            SetUpSurveyPrompt(root, bold, regular);
        }

        /// <summary>HUD 프리팹 루트에 설문 카드만 덧붙인다. HUD의 다른 구성은 건드리지 않는다.</summary>
        public static void AddSurveyPrompt(GameObject hud) =>
            SetUpSurveyPrompt(hud, UiBuilderTools.BoldFont(), UiBuilderTools.RegularFont());

        /// <summary>
        /// 설문을 권할 때 화면 위 가운데에서 내려오는 설문 카드. 화면을 덮지 않는 층(바탕 없음)에 따로 높은 순서의 캔버스를 두어 다른 창의 덮개 위에서도
        /// 누를 수 있게 한다. 없는 요소만 만들고 디자인은 유지한다.
        /// </summary>
        private static void SetUpSurveyPrompt(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            Transform existing = root.transform.Find("SurveyPrompt");
            RectTransform layer;
            if (existing != null)
            {
                layer = (RectTransform)existing;
            }
            else
            {
                layer = (RectTransform)
                    new GameObject("SurveyPrompt", typeof(RectTransform)).transform;
                layer.SetParent(root.transform, false);
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
            }
            layer.SetAsLastSibling();
            var canvas = BuilderTools.GetOrAddComponent<Canvas>(layer.gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = SurveyPromptSortingOrder;
            BuilderTools.GetOrAddComponent<GraphicRaycaster>(layer.gameObject);
            var view = BuilderTools.GetOrAddComponent<SurveyPromptView>(layer.gameObject);

            RectTransform card = UiBuilderTools.FindOrCreateAnchoredPanel(
                layer,
                "Card",
                new Vector2(0.5f, 1f),
                600f,
                new Vector2(0f, -SurveyPromptTopMargin)
            );
            UiBuilderTools.FindOrCreateText(
                card,
                "Title",
                bold,
                26f,
                Color.white,
                SurveyPromptView.Title
            );
            UiBuilderTools.FindOrCreateText(
                card,
                "Body",
                regular,
                20f,
                SubTextColor,
                SurveyPromptView.Body
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(card, "Buttons");
            Button open = UiBuilderTools.FindOrCreateButton(
                buttons,
                "OpenButton",
                SurveyPromptView.OpenLabel,
                bold,
                240f
            );
            Button close = UiBuilderTools.FindOrCreateButton(buttons, "CloseButton", "닫기", bold);

            BuilderTools.SetReference(view, "card", card);
            BuilderTools.SetReference(view, "openButton", open);
            BuilderTools.SetReference(view, "closeButton", close);
            BuilderTools.RequireReferences(view);
            card.gameObject.SetActive(false);
        }

        /// <summary>
        /// 로그인이 풀렸을 때의 안내 창. 다른 화면보다 위에 보이도록 덮개에 따로 높은 순서의 캔버스를 두고, HUD의 마지막 자식으로 둔다.
        /// 없는 요소만 만들고 디자인은 유지한다.
        /// </summary>
        private static void SetUpSessionExpired(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            var view = BuilderTools.GetOrAddComponent<SessionExpiredView>(root);
            RectTransform overlay = FindOrCreateOverlay(root.transform, "SessionExpired");
            overlay.SetAsLastSibling();
            var canvas = BuilderTools.GetOrAddComponent<Canvas>(overlay.gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = SessionExpiredSortingOrder;
            BuilderTools.GetOrAddComponent<GraphicRaycaster>(overlay.gameObject);

            RectTransform window = UiBuilderTools.FindOrCreateAnchoredPanel(
                overlay,
                "Window",
                new Vector2(0.5f, 0.5f),
                560f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(
                window,
                "Title",
                bold,
                32f,
                Color.white,
                SessionExpiredView.Title
            );
            UiBuilderTools.FindOrCreateText(
                window,
                "Body",
                regular,
                22f,
                Color.white,
                SessionExpiredView.Body
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(window, "Buttons");
            BuilderTools.SetReference(
                view,
                "entryButton",
                UiBuilderTools.FindOrCreateButton(
                    buttons,
                    "EntryButton",
                    SessionExpiredView.ButtonLabel,
                    bold,
                    240f
                )
            );
            BuilderTools.SetReference(view, "overlay", overlay.gameObject);
            BuilderTools.RequireReferences(view);
            overlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// 오른쪽 아래 조작법 카드와 메뉴의 [조작법]. 없는 요소만 만들고 디자인은 유지한다.
        /// 카드는 HUD 맨 뒤에 두어, 메뉴를 펼친 동안에는 바깥 영역이 덮고 대화 노트·내 정보 창은 카드 위에 보이게 한다.
        /// </summary>
        private static void SetUpControlsGuide(
            GameObject root,
            LearningHudView hud,
            RectTransform menu,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            var view = BuilderTools.GetOrAddComponent<ControlsGuideView>(root);
            BuilderTools.SetReference(
                view,
                "openButton",
                UiBuilderTools.FindOrCreateButton(
                    menu,
                    "ControlsButton",
                    ControlsGuideText.MenuLabel,
                    bold,
                    200f
                )
            );

            RectTransform card = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "ControlsGuide",
                new Vector2(1f, 0f),
                ControlsGuideWidth,
                new Vector2(-24f, 24f)
            );
            card.SetAsFirstSibling();
            UiBuilderTools.FindOrCreateText(
                card,
                "Title",
                bold,
                24f,
                Color.white,
                ControlsGuideText.MenuLabel
            );
            BuilderTools.SetReference(
                view,
                "bodyText",
                UiBuilderTools.FindOrCreateText(
                    card,
                    "Body",
                    regular,
                    20f,
                    Color.white,
                    ControlsGuideText.Body(isTouchDevice: false)
                )
            );
            BuilderTools.SetReference(
                view,
                "hintText",
                UiBuilderTools.FindOrCreateText(
                    card,
                    "Hint",
                    regular,
                    17f,
                    SubTextColor,
                    ControlsGuideText.ReopenHint
                )
            );
            // 테마 Builder가 카드 오른쪽 위 X 아이콘으로 옮긴다.
            BuilderTools.SetReference(
                view,
                "closeButton",
                UiBuilderTools.FindOrCreateButton(card, "CloseButton", "닫기", bold, 120f)
            );
            BuilderTools.SetReference(view, "card", card.gameObject);
            BuilderTools.RequireReferences(view);
            BuilderTools.SetReference(hud, "controlsGuide", view);
        }

        /// <summary>
        /// 오른쪽 위 메뉴(≡) 버튼과 펼치는 목록을 만든다. 펼친 동안 같은 자리에 강조한 메뉴 버튼과
        /// 화면을 덮는 투명한 닫기 영역을 보인다. 예전 가로 버튼 줄과 아래 가운데 홈 버튼은 연결을 유지한 채 목록으로 옮긴다.
        /// </summary>
        private static RectTransform SetUpMenu(
            GameObject root,
            RectTransform panel,
            TMP_FontAsset bold
        )
        {
            var view = BuilderTools.GetOrAddComponent<HudMenuView>(root);
            RectTransform bar = UiBuilderTools.FindOrCreateButtonRow(panel, "MenuBar");
            bar.SetAsFirstSibling();
            Button open = UiBuilderTools.FindOrCreateButton(bar, "MenuButton", "", bold, 78f);
            Button close = UiBuilderTools.FindOrCreateButton(bar, "OpenMenuButton", "", bold, 78f);
            close.gameObject.SetActive(false);
            RectTransform menu = UiBuilderTools.FindOrCreateColumn(panel, "Menu");
            menu.SetSiblingIndex(bar.GetSiblingIndex() + 1);
            // 메뉴는 접힌 채 시작한다.
            menu.gameObject.SetActive(false);
            Transform oldRow = panel.Find("Buttons");
            if (oldRow != null)
            {
                while (oldRow.childCount > 0)
                    oldRow.GetChild(0).SetParent(menu, false);
                UnityEngine.Object.DestroyImmediate(oldRow.gameObject);
            }
            Transform oldHome = root.transform.Find("HomeButton");
            if (oldHome != null)
                oldHome.SetParent(menu, false);

            BuilderTools.SetReference(view, "menuButton", open);
            BuilderTools.SetReference(view, "openMenuButton", close);
            BuilderTools.SetReference(view, "backdrop", FindOrCreateBackdrop(root.transform));
            BuilderTools.SetReference(view, "items", menu.gameObject);
            BuilderTools.RequireReferences(view);
            return menu;
        }

        // 펼친 메뉴 바깥을 누르면 접히도록 화면 전체를 덮는 투명 버튼. 다른 HUD 요소보다 뒤에 둔다.
        private static Button FindOrCreateBackdrop(Transform root)
        {
            Transform existing = root.Find("MenuBackdrop");
            var rect =
                existing != null
                    ? (RectTransform)existing
                    : new GameObject(
                        "MenuBackdrop",
                        typeof(RectTransform)
                    ).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            image.color = new Color(0, 0, 0, 0);
            var button = BuilderTools.GetOrAddComponent<Button>(rect.gameObject);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            rect.gameObject.SetActive(false);
            return button;
        }

        // 알림 글자는 테마 Builder가 알림 카드(MessageCard) 안으로 옮긴다. 다시 실행해도 하나만 두고 남은 사본은 지운다.
        private static TMP_Text FindOrCreateMessage(RectTransform panel, TMP_FontAsset regular)
        {
            Transform card = panel.Find("MessageCard");
            TMP_Text kept = null;
            foreach (Transform parent in new[] { panel, card })
            {
                if (parent == null)
                    continue;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);
                    if (child.name != "Message")
                        continue;
                    if (kept == null)
                        kept = child.GetComponent<TMP_Text>();
                    else
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            return kept != null
                ? kept
                : UiBuilderTools.FindOrCreateText(panel, "Message", regular, 20f, SubTextColor);
        }

        // 메뉴 목록을 정한 순서로 둔다. 없는 이름은 건너뛴다.
        private static void OrderMenu(Transform menu, params string[] names)
        {
            int index = 0;
            foreach (string name in names)
            {
                Transform item = menu.Find(name);
                if (item != null)
                    item.SetSiblingIndex(index++);
            }
        }

        /// <summary>
        /// 메뉴의 [대화 노트]와 지난 대화를 고르는 대화 노트 창. 창 안은 확정 시안대로 FeedbackListBuilder가 배치한다.
        /// 창은 피드백 화면(정렬 130)보다 아래인 HUD 캔버스에 있어, 목록에서 연 피드백이 창 위에 보인다.
        /// </summary>
        private static void SetUpHistory(
            GameObject root,
            LearningHudView hud,
            RectTransform menu,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            Button open = UiBuilderTools.FindOrCreateButton(
                menu,
                "HistoryButton",
                FeedbackListTitle,
                bold,
                200f
            );
            BuilderTools.SetReference(hud, "historyButton", open);

            var view = BuilderTools.GetOrAddComponent<ConversationHistoryView>(root);
            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "History",
                new Vector2(0.5f, 0.5f),
                760f,
                Vector2.zero
            );
            // 머리줄·대화 줄·쪽 넘김은 확정 시안대로 FeedbackListBuilder가 만든다.
            FeedbackListBuilder.Apply(root.transform);
            BuilderTools.SetReference(hud, "historyView", view);
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 오른쪽 위 버튼 줄 끝의 [내 정보]와 마이페이지 창(내 정보·닉네임 변경·로그아웃 확인). 없는 요소만 만들고 디자인은 유지한다.
        /// </summary>
        private static void SetUpMyPage(
            GameObject root,
            RectTransform menu,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            var view = BuilderTools.GetOrAddComponent<MyPageView>(root);
            BuilderTools.SetReference(
                view,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    ApiSettingsPath,
                    A305.EditorTools.Onboarding.OnboardingBuilder.MenuPath
                )
            );
            Button open = UiBuilderTools.FindOrCreateButton(
                menu,
                "MyPageButton",
                "내 정보",
                bold,
                160f
            );
            BuilderTools.SetReference(view, "openButton", open);

            RectTransform overlay = FindOrCreateOverlay(root.transform, "MyPage");
            BuilderTools.SetReference(view, "overlay", overlay.gameObject);

            RectTransform profile = UiBuilderTools.FindOrCreateAnchoredPanel(
                overlay,
                "Window",
                new Vector2(0.5f, 0.5f),
                600f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(profile, "Title", bold, 32f, Color.white, "내 정보");
            BuilderTools.SetReference(
                view,
                "profileText",
                UiBuilderTools.FindOrCreateText(profile, "Profile", regular, 24f, Color.white)
            );
            RectTransform nicknameRow = UiBuilderTools.FindOrCreateButtonRow(
                profile,
                "NicknameRow"
            );
            TMP_InputField nickname = UiBuilderTools.FindOrCreateInputField(
                nicknameRow,
                "NicknameInput",
                regular,
                "새 닉네임"
            );
            // WebGL에서 한글 조합 입력을 받으려면 브라우저 입력창을 연결한다(가입 화면과 같음).
            BuilderTools.GetOrAddComponent<BrowserImeInput>(nickname.gameObject);
            BuilderTools.SetReference(view, "nicknameInput", nickname);
            BuilderTools.SetReference(
                view,
                "nicknameButton",
                UiBuilderTools.FindOrCreateButton(
                    nicknameRow,
                    "NicknameButton",
                    "닉네임 변경",
                    bold,
                    180f
                )
            );
            BuilderTools.SetReference(
                view,
                "statusText",
                UiBuilderTools.FindOrCreateText(
                    profile,
                    "Status",
                    regular,
                    20f,
                    SubTextColor,
                    "한글 2~8자 또는 영문·숫자 2~16자로 정해 주세요."
                )
            );
            // 선택 동의(음성 처리·AI 개선)는 받은 목록대로 줄을 복제한다(USER-02·03). 게스트는 음성 처리만 받는다.
            TMP_Text consentsLabel = UiBuilderTools.FindOrCreateText(
                profile,
                "ConsentsLabel",
                bold,
                22f,
                Color.white,
                "선택 동의"
            );
            RectTransform consents = UiBuilderTools.FindOrCreateColumn(profile, "Consents");
            BuilderTools.SetReference(view, "consentsParent", consents);
            BuilderTools.SetReference(
                view,
                "consentTemplate",
                OnboardingBuilder.SetUpConsentRow(consents, "ConsentRowTemplate", regular)
            );
            RectTransform lookSensitivity = SetUpSliderSetting(
                view,
                profile,
                new SliderSetting(
                    "LookSensitivity",
                    "시점 감도",
                    "마우스·화면을 끌어 시점을 돌리는 빠르기예요.\n이 기기에만 저장돼요.",
                    LookSensitivity.Min,
                    LookSensitivity.Max,
                    LookSensitivity.Default,
                    "lookSensitivitySlider",
                    "lookSensitivityText"
                ),
                bold,
                regular
            );
            lookSensitivity.SetSiblingIndex(profile.Find("Status").GetSiblingIndex() + 1);
            SetUpNpcSpeechSpeed(view, profile, bold, regular);
            RectTransform profileButtons = UiBuilderTools.FindOrCreateButtonRow(profile, "Buttons");
            // 이미 만든 화면에 새로 붙는 줄은 맨 아래에 생기므로 버튼 줄 위로 옮긴다.
            // 이미 버튼 줄 위에 있는 줄까지 옮기면 제목·목록·버튼 순서가 뒤집히므로 아래에 있는 줄만 옮긴다.
            MoveAboveIfBelow(consentsLabel.transform, profileButtons);
            MoveAboveIfBelow(consents, profileButtons);
            BuilderTools.SetReference(
                view,
                "logoutButton",
                UiBuilderTools.FindOrCreateButton(
                    profileButtons,
                    "LogoutButton",
                    "로그아웃",
                    bold,
                    160f
                )
            );
            BuilderTools.SetReference(
                view,
                "closeButton",
                FindCornerOrRowButton(profile, profileButtons, "CloseButton", "닫기", bold, 140f)
            );
            // 되돌릴 수 없는 동작이라 [로그아웃]·[닫기]와 떨어진 맨 앞에 둔다.
            Button withdrawButton = UiBuilderTools.FindOrCreateButton(
                profileButtons,
                "WithdrawButton",
                "회원 탈퇴",
                bold,
                160f
            );
            withdrawButton.transform.SetAsFirstSibling();
            BuilderTools.SetReference(view, "withdrawButton", withdrawButton);
            BuilderTools.SetReference(view, "profilePanel", profile.gameObject);

            RectTransform logout = UiBuilderTools.FindOrCreateAnchoredPanel(
                overlay,
                "LogoutDialog",
                new Vector2(0.5f, 0.5f),
                520f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(
                logout,
                "Title",
                bold,
                30f,
                Color.white,
                "로그아웃할까요?"
            );
            UiBuilderTools.FindOrCreateText(
                logout,
                "Body",
                regular,
                22f,
                Color.white,
                "처음 화면으로 돌아가요. 게스트로 시작했다면 로그아웃한 뒤 같은 기록으로 다시 들어올 수 없어요."
            );
            BuilderTools.SetReference(
                view,
                "logoutStatusText",
                UiBuilderTools.FindOrCreateText(logout, "Status", regular, 20f, SubTextColor)
            );
            RectTransform logoutButtons = UiBuilderTools.FindOrCreateButtonRow(logout, "Buttons");
            BuilderTools.SetReference(
                view,
                "logoutCancelButton",
                UiBuilderTools.FindOrCreateButton(logoutButtons, "CancelButton", "취소", bold, 140f)
            );
            BuilderTools.SetReference(
                view,
                "logoutConfirmButton",
                UiBuilderTools.FindOrCreateButton(
                    logoutButtons,
                    "ConfirmButton",
                    "로그아웃",
                    bold,
                    160f
                )
            );
            BuilderTools.SetReference(view, "logoutDialog", logout.gameObject);
            RectTransform withdraw = SetUpWithdrawDialog(view, overlay, bold, regular);
            BuilderTools.RequireReferences(view);

            logout.gameObject.SetActive(false);
            withdraw.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        // 테마 Builder가 닫기를 창 오른쪽 위로 옮겼으면 그 버튼을 쓰고, 아니면 버튼 줄에서 찾거나 만든다.
        private static Button FindCornerOrRowButton(
            Transform panel,
            RectTransform row,
            string buttonName,
            string label,
            TMP_FontAsset font,
            float preferredWidth
        )
        {
            Transform corner = panel.Find(buttonName);
            return corner != null
                ? corner.GetComponent<Button>()
                : UiBuilderTools.FindOrCreateButton(row, buttonName, label, font, preferredWidth);
        }

        // [회원 탈퇴]의 확인 창(USER-05). 되돌릴 수 없으므로 지우는 내용을 알리고 한 번 더 묻는다.
        private static RectTransform SetUpWithdrawDialog(
            MyPageView view,
            RectTransform overlay,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform withdraw = UiBuilderTools.FindOrCreateAnchoredPanel(
                overlay,
                "WithdrawDialog",
                new Vector2(0.5f, 0.5f),
                520f,
                Vector2.zero
            );
            UiBuilderTools.FindOrCreateText(
                withdraw,
                "Title",
                bold,
                30f,
                Color.white,
                "회원 탈퇴할까요?"
            );
            UiBuilderTools.FindOrCreateText(
                withdraw,
                "Body",
                regular,
                22f,
                Color.white,
                "대화·피드백·NPC 기억·단어장과 이메일·닉네임이 바로 지워지고 되돌릴 수 없어요. 같은 Google 계정으로 다시 가입할 수 있어요."
            );
            BuilderTools.SetReference(
                view,
                "withdrawStatusText",
                UiBuilderTools.FindOrCreateText(withdraw, "Status", regular, 20f, SubTextColor)
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(withdraw, "Buttons");
            BuilderTools.SetReference(
                view,
                "withdrawCancelButton",
                UiBuilderTools.FindOrCreateButton(buttons, "CancelButton", "취소", bold, 140f)
            );
            BuilderTools.SetReference(
                view,
                "withdrawConfirmButton",
                UiBuilderTools.FindOrCreateButton(buttons, "ConfirmButton", "탈퇴하기", bold, 160f)
            );
            BuilderTools.SetReference(view, "withdrawDialog", withdraw.gameObject);
            return withdraw;
        }

        // row가 anchor보다 아래에 있을 때만 anchor 바로 위로 옮긴다.
        private static void MoveAboveIfBelow(Transform row, Transform anchor)
        {
            if (row.GetSiblingIndex() > anchor.GetSiblingIndex())
                row.SetSiblingIndex(anchor.GetSiblingIndex());
        }

        // 시점 감도 줄 바로 아래에 둔다. 서버 음성 합성에 쓰는 값이라 범위·기본값은 NpcSpeechSpeed를 따른다.
        private static void SetUpNpcSpeechSpeed(
            MyPageView view,
            RectTransform profile,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform group = SetUpSliderSetting(
                view,
                profile,
                new SliderSetting(
                    "NpcSpeechSpeed",
                    "NPC 말 빠르기",
                    "NPC가 영어로 말하는 빠르기예요.\n다음 대사부터 바뀌고 이 기기에만 저장돼요.",
                    NpcSpeechSpeed.Min,
                    NpcSpeechSpeed.Max,
                    NpcSpeechSpeed.Default,
                    "npcSpeechSpeedSlider",
                    "npcSpeechSpeedText"
                ),
                bold,
                regular
            );
            group.SetSiblingIndex(profile.Find("LookSensitivity").GetSiblingIndex() + 1);
        }

        private readonly struct SliderSetting
        {
            public readonly string GroupName;
            public readonly string Title;
            public readonly string Help;
            public readonly float Min;
            public readonly float Max;
            public readonly float Default;
            public readonly string SliderField;
            public readonly string ValueField;

            public SliderSetting(
                string groupName,
                string title,
                string help,
                float min,
                float max,
                float defaultValue,
                string sliderField,
                string valueField
            )
            {
                GroupName = groupName;
                Title = title;
                Help = help;
                Min = min;
                Max = max;
                Default = defaultValue;
                SliderField = sliderField;
                ValueField = valueField;
            }
        }

        // 닉네임 안내 아래, 버튼 줄 위에 제목·설명과 [느리게] 슬라이더 [빠르게] 현재값 줄을 둔다.
        // 자리는 부르는 쪽이 정한다. 글자 색은 UiThemeBuilder가 테마에 맞춘다.
        private static RectTransform SetUpSliderSetting(
            MyPageView view,
            RectTransform profile,
            SliderSetting setting,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform group = UiBuilderTools.FindOrCreateColumn(profile, setting.GroupName);
            UiBuilderTools.FindOrCreateText(group, "Title", bold, 26f, Color.white, setting.Title);
            UiBuilderTools.FindOrCreateText(
                group,
                "Help",
                regular,
                20f,
                SubTextColor,
                setting.Help
            );
            RectTransform row = UiBuilderTools.FindOrCreateButtonRow(group, "Row");
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            FixWidth(
                UiBuilderTools.FindOrCreateText(row, "Slow", regular, 20f, SubTextColor, "느리게"),
                72f
            );
            Slider slider = UiBuilderTools.FindOrCreateSlider(
                row,
                "Slider",
                setting.Min,
                setting.Max,
                setting.Default
            );
            FixWidth(
                UiBuilderTools.FindOrCreateText(row, "Fast", regular, 20f, SubTextColor, "빠르게"),
                72f
            );
            TMP_Text value = UiBuilderTools.FindOrCreateText(
                row,
                "Value",
                bold,
                30f,
                Color.white,
                $"{setting.Default:0.0}배"
            );
            value.alignment = TextAlignmentOptions.Right;
            FixWidth(value, 96f);

            BuilderTools.SetReference(view, setting.SliderField, slider);
            BuilderTools.SetReference(view, setting.ValueField, value);
            return group;
        }

        private static void FixWidth(TMP_Text text, float width)
        {
            BuilderTools.GetOrAddComponent<LayoutElement>(text.gameObject).preferredWidth = width;
        }

        // 화면 전체를 덮는 어두운 바탕. 마이페이지가 열려 있는 동안 뒤의 HUD 버튼과 사물이 눌리지 않게 클릭을 받는다.
        private static RectTransform FindOrCreateOverlay(Transform root, string overlayName)
        {
            Transform existing = root.Find(overlayName);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            var overlay = new GameObject(overlayName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            return rect;
        }
    }
}
