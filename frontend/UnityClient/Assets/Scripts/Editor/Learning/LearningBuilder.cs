using System;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
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
        };

        // 다른 화면 위에 열리도록 대화 화면(120)보다 위에 둔다. 단어장은 피드백에서도 열 수 있어 그 위에 둔다.
        private const int FeedbackSortingOrder = 130;
        private const int VocabularySortingOrder = 135;
        private const int HudSortingOrder = 95;

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

        /// <summary>오른쪽 위 메뉴(≡)와 메뉴 버튼, 지난 대화·내 정보 창이 있는 HUD 프리팹의 기능 구성을 맞춘다.</summary>
        public static void PrepareHud() =>
            BuilderTools.UpdateOrCreatePrefab(HudPrefabPath, "LearningHud", SetUpHud);

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
            Transform bodyParent = panel.Find("PageCard");
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
            Button saveButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "SaveButton",
                "표현 저장",
                bold,
                180f
            );
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

            // 피드백을 받지 못했을 때만 [표현 저장]·[상세 보기] 대신 보인다. 새로 붙이면 [단어장 보기] 앞에 둔다.
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
            BuilderTools.SetReference(view, "saveButton", saveButton);
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
            BuilderTools.RequireReferences(view);

            // 대화가 끝난 뒤에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        private static void SetUpVocabularyScreen(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, VocabularySortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<VocabularyView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "Panel",
                new Vector2(0.5f, 0.5f),
                900f,
                Vector2.zero
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                32f,
                Color.white,
                "조금씩 모아둔, 나의 표현들"
            );
            UiBuilderTools.FindOrCreateText(
                panel,
                "Hint",
                regular,
                20f,
                SubTextColor,
                "뜻을 떠올려 본 뒤 [외웠어요]로 표시해요. 자동 채점이나 점수는 없어요."
            );
            TMP_Text list = UiBuilderTools.FindOrCreateText(
                panel,
                "List",
                regular,
                24f,
                Color.white
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button previousButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "PreviousButton",
                "이전",
                bold,
                150f
            );
            Button nextButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "NextButton",
                "다음",
                bold,
                150f
            );
            Button learnedButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "LearnedButton",
                "외웠어요",
                bold,
                150f
            );
            Button deleteButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "DeleteButton",
                "삭제",
                bold,
                150f
            );
            Button closeButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "CloseButton",
                "닫기",
                bold,
                150f
            );

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "listText", list);
            BuilderTools.SetReference(view, "previousButton", previousButton);
            BuilderTools.SetReference(view, "nextButton", nextButton);
            BuilderTools.SetReference(view, "learnedButton", learnedButton);
            BuilderTools.SetReference(
                view,
                "learnedButtonLabel",
                learnedButton.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(view, "deleteButton", deleteButton);
            BuilderTools.SetReference(view, "closeButton", closeButton);

            RectTransform memoRow = UiBuilderTools.FindOrCreateButtonRow(panel, "MemoRow");
            TMP_InputField memoInput = UiBuilderTools.FindOrCreateInputField(
                memoRow,
                "MemoInput",
                regular,
                "고른 단어에 메모를 남겨 보세요 (200자까지)"
            );
            Button memoSaveButton = UiBuilderTools.FindOrCreateButton(
                memoRow,
                "MemoSaveButton",
                "메모 저장",
                bold,
                180f
            );
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                panel,
                "Status",
                regular,
                20f,
                SubTextColor
            );
            BuilderTools.SetReference(view, "memoInput", memoInput);
            BuilderTools.SetReference(view, "memoSaveButton", memoSaveButton);
            BuilderTools.SetReference(view, "statusText", status);
            BuilderTools.RequireReferences(view);

            panel.gameObject.SetActive(false);
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
            SetUpHistory(root, view, menu, bold, regular);
            BuilderTools.RequireReferences(view);

            SetUpMyPage(root, menu, bold, regular);
            // 테마 고르기는 집에만 있어, 집 씬의 HUD에서만 켜고 테마 고르기 화면과 잇는다.
            UiBuilderTools
                .FindOrCreateButton(menu, "ThemeButton", "테마", bold, 200f)
                .gameObject.SetActive(false);
            OrderMenu(
                menu,
                "VocabularyButton",
                "HistoryButton",
                "MyPageButton",
                "ThemeButton",
                "Divider",
                "HomeButton"
            );
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
        /// 메뉴의 [지난 대화]와 지난 대화 목록 창. 없는 요소만 만들고 디자인은 유지한다.
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
                "지난 대화",
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
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 32f, Color.white, "지난 대화");
            UiBuilderTools.FindOrCreateText(
                panel,
                "Hint",
                regular,
                20f,
                SubTextColor,
                "대화를 고르고 [피드백 보기]를 누르면 그때 받은 피드백과 대화 기록을 다시 볼 수 있어요."
            );
            BuilderTools.SetReference(
                view,
                "listText",
                UiBuilderTools.FindOrCreateText(panel, "List", regular, 22f, Color.white)
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            BuilderTools.SetReference(
                view,
                "previousButton",
                UiBuilderTools.FindOrCreateButton(buttons, "PreviousButton", "이전", bold, 120f)
            );
            BuilderTools.SetReference(
                view,
                "nextButton",
                UiBuilderTools.FindOrCreateButton(buttons, "NextButton", "다음", bold, 120f)
            );
            BuilderTools.SetReference(
                view,
                "openButton",
                UiBuilderTools.FindOrCreateButton(buttons, "OpenButton", "피드백 보기", bold, 180f)
            );
            BuilderTools.SetReference(
                view,
                "closeButton",
                UiBuilderTools.FindOrCreateButton(buttons, "CloseButton", "닫기", bold, 120f)
            );
            BuilderTools.SetReference(
                view,
                "statusText",
                UiBuilderTools.FindOrCreateText(panel, "Status", regular, 20f, SubTextColor)
            );
            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.RequireReferences(view);
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
            SetUpLookSensitivity(view, profile, bold, regular);
            RectTransform profileButtons = UiBuilderTools.FindOrCreateButtonRow(profile, "Buttons");
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
                UiBuilderTools.FindOrCreateButton(profileButtons, "CloseButton", "닫기", bold, 140f)
            );
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
            BuilderTools.RequireReferences(view);

            logout.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
        }

        // 닉네임 안내 아래, 버튼 줄 위에 "시점 감도" 제목·설명과 [느리게] 슬라이더 [빠르게] 현재값 줄을 둔다.
        // 이미 만든 창에 추가할 때도 같은 자리에 넣는다. 글자 색은 UiThemeBuilder가 테마에 맞춘다.
        private static void SetUpLookSensitivity(
            MyPageView view,
            RectTransform profile,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform group = UiBuilderTools.FindOrCreateColumn(profile, "LookSensitivity");
            UiBuilderTools.FindOrCreateText(group, "Title", bold, 26f, Color.white, "시점 감도");
            UiBuilderTools.FindOrCreateText(
                group,
                "Help",
                regular,
                20f,
                SubTextColor,
                "마우스·화면을 끌어 시점을 돌리는 빠르기예요.\n이 기기에만 저장돼요."
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
                LookSensitivity.Min,
                LookSensitivity.Max,
                LookSensitivity.Default
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
                "1.0배"
            );
            value.alignment = TextAlignmentOptions.Right;
            FixWidth(value, 96f);

            Transform status = profile.Find("Status");
            group.SetSiblingIndex(status.GetSiblingIndex() + 1);
            BuilderTools.SetReference(view, "lookSensitivitySlider", slider);
            BuilderTools.SetReference(view, "lookSensitivityText", value);
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
