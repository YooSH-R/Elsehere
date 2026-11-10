using System.Collections.Generic;
using System.Linq;
using A305.Api;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Onboarding;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BrowserImeInput = WebGLSupport.WebGLInput;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 입구 화면(01~05) 프리팹과 설정 에셋, 확인용 Sandbox 씬, 배포용 EntryScene을 만든다.
    /// 화면은 없는 요소만 만들고 이미 있는 요소의 디자인은 유지한다. EntryScene은 Scene List의 첫 씬으로 둔다.
    /// </summary>
    public static class OnboardingBuilder
    {
        public const string MenuPath = "Tools/A305/Onboarding/Build";
        public const string EntryScenePath = "Assets/Scenes/Production/EntryScene.unity";

        // 음성 동의 안내는 [말하기] 버튼 이름을 따르므로, 이미 있는 칸도 Builder가 매번 이 글자로 맞춘다.
        private const string GuestVoiceNote =
            "음성 처리에 동의하면 [말하기]로 말할 수 있어요. 동의하지 않아도 글자로 대화할 수 있어요.";
        private const string SignupVoiceNote =
            "음성 처리에 동의하면 [말하기]로 말할 수 있어요. 동의하지 않아도 글자로 대화할 수 있고, 나중에 오른쪽 위 메뉴(≡) › 내 정보 › 선택 동의에서 바꿀 수 있어요.";
        private const string ScreensPrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";
        private const string OnboardingSettingsPath =
            "Assets/Data/Onboarding/OnboardingSettings.asset";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox/OnboardingSandbox.unity";
        private const string ImeCheckScenePath =
            "Assets/Scenes/Sandbox/OnboardingImeCheckSandbox.unity";

        private const float PanelWidth = 820f;
        private const int NicknameCharacterLimit = 16;

        private static readonly Color BackgroundColor = new Color(0.06f, 0.07f, 0.1f, 1f);
        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color HintColor = new Color(0.62f, 0.8f, 1f);
        private static readonly Color BadgeColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color CardColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color SecondaryButtonColor = new Color(0.3f, 0.32f, 0.38f, 1f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            BuildSandbox();
            PrepareEntryScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"입구 화면을 준비했습니다: {ScreensPrefabPath}, {EntryScenePath}");
        }

        /// <summary>
        /// 한글 입력(IME) 확인용 Web 빌드. 가입 화면은 Google 가입 대기 세션이 있어야 열리므로, 닉네임 입력창과 같은 설정의 입력창 하나만
        /// 둔 Sandbox 씬을 Builds/ImeCheck로 빌드한다. 브라우저에서 열어 한글을 입력해 확인한다.
        /// </summary>
        [MenuItem("Tools/A305/Onboarding/Build Ime Check Web")]
        public static void BuildImeCheckWeb()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            BuilderTools.EnsureEventSystem(groups);
            EnsureCamera(groups);
            var canvas = new GameObject("ImeCheckCanvas");
            canvas.transform.SetParent(groups.UI, false);
            UiBuilderTools.EnsureOverlayCanvas(canvas, 0, receivesClicks: true);
            RectTransform panel = CenterPanel(canvas.transform, "Panel");
            UiBuilderTools.FindOrCreateText(
                panel,
                "Guide",
                UiBuilderTools.BoldFont(),
                28f,
                Color.white,
                "입력창을 누르고 한글을 입력해 보세요."
            );
            CreateNicknameInput(panel, UiBuilderTools.RegularFont());
            BuilderTools.SaveScene(groups.Scene, ImeCheckScenePath);

            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { ImeCheckScenePath },
                "Builds/ImeCheck",
                BuildTarget.WebGL,
                BuildOptions.None
            );
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException(
                    $"한글 입력 확인 빌드 실패: {report.summary.result}"
                );
            }
        }

        /// <summary>
        /// Scene List 앞에 둘 씬 목록 맨 앞에 EntryScene을 붙인다. EntryScene이 아직 없으면 그대로 돌려준다.
        /// 다른 Builder가 Scene List 순서를 다시 맞출 때도 입구 화면에서 시작하게 하기 위함이다.
        /// </summary>
        public static IReadOnlyCollection<string> WithEntrySceneFirst(
            IEnumerable<string> leadingScenePaths
        )
        {
            List<string> paths = leadingScenePaths.Where(path => path != EntryScenePath).ToList();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath) != null)
            {
                paths.Insert(0, EntryScenePath);
            }

            return paths;
        }

        private static void PrepareAssets()
        {
            BuilderTools.LoadOrCreateAsset<ApiSettings>(ApiSettingsPath);
            BuilderTools.LoadOrCreateAsset<OnboardingSettings>(OnboardingSettingsPath);
            BuilderTools.UpdateOrCreatePrefab(ScreensPrefabPath, "EntryScreens", SetUpScreens);
        }

        // Sandbox 씬은 확인용이라 다시 만들 때 덮어쓴다.
        private static void BuildSandbox()
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            ConnectToScene(groups);
            BuilderTools.SaveScene(groups.Scene, SandboxScenePath);
        }

        // 배포용 씬은 없을 때만 만들고, 있으면 열어서 연결만 맞춘다.
        private static void PrepareEntryScene()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath) != null;
            Scene scene = exists
                ? EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConnectToScene(BuilderTools.EnsureGroups(scene));
            BuilderTools.SaveScene(scene, EntryScenePath);
            BuilderTools.PutFirstInSceneList(WithEntrySceneFirst(new string[0]));
        }

        private static void ConnectToScene(SceneGroups groups)
        {
            BuilderTools.EnsureEventSystem(groups);
            EnsureCamera(groups);
            if (BuilderTools.FindSingleInScene<OnboardingScreen>(groups.Scene) == null)
            {
                BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(ScreensPrefabPath, MenuPath),
                    groups.UI,
                    Vector3.zero,
                    Quaternion.identity
                );
            }
        }

        /// <summary>입구 씬 카메라의 배경색을 화면 배경과 맞춰 저장한다. 유리 창은 이 카메라 화면을 흐리게 비춘다.</summary>
        public static void PaintCamera()
        {
            Scene scene = EditorSceneManager.OpenScene(EntryScenePath);
            EnsureCamera(BuilderTools.EnsureGroups(scene));
            BuilderTools.SaveScene(scene, EntryScenePath);
        }

        // 입구 화면에는 공간이 없어 화면 UI 뒤를 채울 카메라만 둔다. 유리 창이 이 화면을 비추므로 배경색을 화면 배경(테마 배경막)과 같게 칠한다.
        private static void EnsureCamera(SceneGroups groups)
        {
            var camera = BuilderTools.FindSingleInScene<Camera>(groups.Scene);
            if (camera == null)
            {
                camera = BuilderTools
                    .CreateEmpty(
                        "EntryCamera",
                        groups.Environment,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .gameObject.AddComponent<Camera>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackdropColor();
        }

        // ThemedElement의 배경막(Backdrop)과 같은 색: 디자인 주 색을 흰색 쪽으로 옅게 섞는다.
        private static Color BackdropColor()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(
                "Assets/Data/Theme/UiThemeCatalog.asset"
            );
            UiThemeDefinition design = catalog != null ? catalog.Get(UiThemeId.Jelly) : null;
            return design != null
                ? Color.Lerp(design.ToneColor(UiTone.Primary), Color.white, .8f)
                : BackgroundColor;
        }

        private static void SetUpScreens(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, 0, receivesClicks: true);
            var inputGroup = BuilderTools.GetOrAddComponent<CanvasGroup>(root);
            var screen = BuilderTools.GetOrAddComponent<OnboardingScreen>(root);
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            EnsureBackground(root.transform);
            bool isNewMessage = root.transform.Find("Message") == null;
            TMP_Text message = UiBuilderTools.FindOrCreateText(
                root.transform,
                "Message",
                regular,
                22f,
                BadgeColor
            );
            if (isNewMessage)
            {
                AnchorBottom((RectTransform)message.transform);
            }

            BuilderTools.SetReference(
                screen,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(ApiSettingsPath, MenuPath)
            );
            BuilderTools.SetReference(
                screen,
                "settings",
                BuilderTools.LoadRequiredAsset<OnboardingSettings>(OnboardingSettingsPath, MenuPath)
            );
            BuilderTools.SetReference(screen, "inputGroup", inputGroup);
            BuilderTools.SetReference(screen, "messageText", message);
            BuilderTools.SetReference(screen, "loginPanel", SetUpLoginPanel(root, bold, regular));
            BuilderTools.SetReference(screen, "guestPanel", SetUpGuestPanel(root, bold, regular));
            // 약관 단계가 가입 정보 화면에 있던 약관 줄·음성 안내를 옮겨 가므로 가입 정보 화면보다 먼저 맞춘다.
            BuilderTools.SetReference(screen, "termsPanel", SetUpTermsPanel(root, bold, regular));
            BuilderTools.SetReference(screen, "signupPanel", SetUpSignupPanel(root, bold, regular));
            BuilderTools.SetReference(
                screen,
                "languagePanel",
                SetUpLanguagePanel(root, bold, regular)
            );
            BuilderTools.SetReference(screen, "levelTestPanel", SetUpLevelTestPanel(root));
            BuilderTools.SetReference(
                screen,
                "levelSettingPanel",
                SetUpLevelSettingPanel(root, bold, regular)
            );
            BuilderTools.SetReference(
                screen,
                "connectionPanel",
                SetUpConnectionPanel(root, bold, regular)
            );
            BuilderTools.SetReference(
                screen,
                "guestReturnPanel",
                SetUpGuestReturnPanel(root, bold, regular)
            );
            BuilderTools.RequireReferences(screen);
        }

        /// <summary>
        /// 입구 화면 프리팹에 게임 전 멈춤 화면(게스트 다시 접속·이미 가입한 계정 안내)만 더하고 테마와 시안 창을 입힌다. 입구 전체 Builder를 다시 돌리지
        /// 않아도 된다.
        /// </summary>
        [MenuItem("Tools/A305/Onboarding/Apply Guest Return Panel")]
        public static void ApplyGuestReturnPanel()
        {
            BuilderTools.UpdatePrefabIfExists(
                ScreensPrefabPath,
                root =>
                {
                    var screen = root.GetComponent<OnboardingScreen>();
                    BuilderTools.SetReference(
                        screen,
                        "guestReturnPanel",
                        SetUpGuestReturnPanel(
                            root,
                            UiBuilderTools.BoldFont(),
                            UiBuilderTools.RegularFont()
                        )
                    );
                    BuilderTools.RequireReferences(screen);
                }
            );
            A305.EditorTools.Theme.UiThemeBuilder.ApplyGuestReturnTheme();
            AssetDatabase.SaveAssets();
        }

        private static LoginPanelView SetUpLoginPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "LoginPanel");
            var view = BuilderTools.GetOrAddComponent<LoginPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 48f, Color.white, "Elsehere");
            UiBuilderTools.FindOrCreateText(
                panel,
                "Subtitle",
                regular,
                24f,
                SubTextColor,
                "3D 마을에서 NPC와 현지 언어로 대화해요."
            );
            Button google = UiBuilderTools.FindOrCreateButton(
                panel,
                "GoogleButton",
                "Google로 계속하기",
                bold,
                PanelWidth
            );
            Button guest = FindOrCreateSecondaryButton(
                panel,
                "GuestButton",
                "게스트로 시작하기",
                regular,
                PanelWidth
            );
            // 이미 만든 화면에 새로 붙이면 맨 아래에 생기므로 Google 버튼 바로 아래로 옮긴다.
            guest.transform.SetSiblingIndex(google.transform.GetSiblingIndex() + 1);
            Button devLogin = FindOrCreateSecondaryButton(
                panel,
                "DevLoginButton",
                "개발용 로그인",
                regular,
                PanelWidth
            );
            TMP_Text notice = UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                regular,
                20f,
                BadgeColor
            );
            // 로그인하기 전에도 약관을 볼 수 있게 창 맨 아래에 링크를 둔다. 모양은 UiThemeBuilder가 차분한 글자 버튼으로 칠한다.
            RectTransform legalLinks = UiBuilderTools.FindOrCreateButtonRow(panel, "LegalLinks");
            Button terms = UiBuilderTools.FindOrCreateButton(
                legalLinks,
                "TermsButton",
                "이용약관",
                regular
            );
            Button privacy = UiBuilderTools.FindOrCreateButton(
                legalLinks,
                "PrivacyButton",
                "개인정보 처리방침",
                regular
            );

            BuilderTools.SetReference(view, "googleButton", google);
            BuilderTools.SetReference(view, "guestButton", guest);
            BuilderTools.SetReference(view, "devLoginButton", devLogin);
            BuilderTools.SetReference(view, "noticeText", notice);
            BuilderTools.SetReference(view, "termsButton", terms);
            BuilderTools.SetReference(view, "privacyButton", privacy);
            BuilderTools.RequireReferences(view);
            return view;
        }

        // 게스트는 닉네임과 가입 약관 없이 캐릭터와 음성 처리 동의(선택)만 고른다(AUTH-07).
        private static GuestPanelView SetUpGuestPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "GuestPanel");
            var view = BuilderTools.GetOrAddComponent<GuestPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "캐릭터를 골라 주세요"
            );
            RectTransform avatarRow = UiBuilderTools.FindOrCreateButtonRow(panel, "AvatarRow");
            Button female = UiBuilderTools.FindOrCreateButton(
                avatarRow,
                "FemaleButton",
                "여성",
                regular
            );
            Button male = UiBuilderTools.FindOrCreateButton(
                avatarRow,
                "MaleButton",
                "남성",
                regular
            );
            TMP_Text note = UiBuilderTools.FindOrCreateText(
                panel,
                "Note",
                regular,
                20f,
                SubTextColor,
                GuestPanelText.Note(null)
            );
            ConsentRowView voiceConsent = SetUpConsentRow(panel, "VoiceConsentRow", regular);
            TMP_Text voiceNote = UiBuilderTools.FindOrCreateText(
                panel,
                "VoiceConsentNote",
                regular,
                20f,
                SubTextColor,
                GuestVoiceNote
            );
            voiceNote.text = GuestVoiceNote;
            // 이미 만든 패널에 새로 붙는 줄은 맨 아래에 생기므로 안내 문구 바로 뒤로 옮긴다.
            voiceConsent.transform.SetSiblingIndex(note.transform.GetSiblingIndex() + 1);
            voiceNote.transform.SetSiblingIndex(voiceConsent.transform.GetSiblingIndex() + 1);
            Button start = UiBuilderTools.FindOrCreateButton(
                panel,
                "StartButton",
                "시작하기",
                bold,
                PanelWidth
            );
            Button back = FindOrCreateSecondaryButton(
                panel,
                "BackButton",
                "뒤로",
                regular,
                PanelWidth
            );

            BuilderTools.SetReference(view, "femaleButton", female);
            BuilderTools.SetReference(view, "maleButton", male);
            BuilderTools.SetReference(view, "noteText", note);
            BuilderTools.SetReference(view, "voiceConsentRow", voiceConsent);
            BuilderTools.SetReference(view, "startButton", start);
            BuilderTools.SetReference(view, "backButton", back);
            BuilderTools.RequireReferences(view);
            return view;
        }

        private static SignupPanelView SetUpSignupPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "SignupPanel");
            var view = BuilderTools.GetOrAddComponent<SignupPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "가입 정보 입력"
            );

            UiBuilderTools.FindOrCreateText(
                panel,
                "NicknameLabel",
                bold,
                22f,
                Color.white,
                "닉네임"
            );
            RectTransform nicknameRow = UiBuilderTools.FindOrCreateButtonRow(panel, "NicknameRow");
            TMP_InputField nickname = CreateNicknameInput(nicknameRow, regular);
            Button checkNickname = UiBuilderTools.FindOrCreateButton(
                nicknameRow,
                "CheckNicknameButton",
                "중복 확인",
                regular,
                180f
            );
            TMP_Text hint = UiBuilderTools.FindOrCreateText(
                panel,
                "NicknameHint",
                regular,
                20f,
                HintColor
            );

            UiBuilderTools.FindOrCreateText(panel, "AvatarLabel", bold, 22f, Color.white, "캐릭터");
            RectTransform avatarRow = UiBuilderTools.FindOrCreateButtonRow(panel, "AvatarRow");
            Button female = UiBuilderTools.FindOrCreateButton(
                avatarRow,
                "FemaleButton",
                "여성",
                regular
            );
            Button male = UiBuilderTools.FindOrCreateButton(
                avatarRow,
                "MaleButton",
                "남성",
                regular
            );

            // 약관은 앞 단계(TermsPanel)에서 받는다. 예전에 이 화면에 두었던 약관 제목은 지운다.
            Transform consentsLabel = panel.Find("ConsentsLabel");
            if (consentsLabel != null)
                Object.DestroyImmediate(consentsLabel.gameObject);

            Button back = FindOrCreateSecondaryButton(
                panel,
                "BackButton",
                "뒤로",
                regular,
                PanelWidth
            );
            Button submit = UiBuilderTools.FindOrCreateButton(
                panel,
                "SubmitButton",
                "가입하기",
                bold,
                PanelWidth
            );
            back.transform.SetSiblingIndex(submit.transform.GetSiblingIndex());

            BuilderTools.SetReference(view, "nicknameInput", nickname);
            BuilderTools.SetReference(view, "checkNicknameButton", checkNickname);
            BuilderTools.SetReference(view, "nicknameHintText", hint);
            BuilderTools.SetReference(view, "femaleButton", female);
            BuilderTools.SetReference(view, "maleButton", male);
            BuilderTools.SetReference(view, "backButton", back);
            BuilderTools.SetReference(view, "submitButton", submit);
            BuilderTools.RequireReferences(view);
            return view;
        }

        /// <summary>
        /// 가입의 첫 단계인 약관 동의 화면. [전체 동의], 약관 줄 템플릿, 음성 처리 안내, [다음]을 둔다. 예전에 가입 정보 화면에 있던 약관 줄과
        /// 음성 안내가 있으면 디자인을 유지한 채 이 화면으로 옮긴다. 없는 요소만 만든다.
        /// </summary>
        private static TermsPanelView SetUpTermsPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "TermsPanel");
            var view = BuilderTools.GetOrAddComponent<TermsPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 36f, Color.white, "약관 동의");
            ConsentRowView allAgree = SetUpConsentRow(panel, "AllAgreeRow", bold);
            // 실행 중에는 TermsPanelView가 같은 문구로 맞추지만, 프리팹에서도 [전체 동의] 줄로 보이게 둔다.
            allAgree.transform.Find("Label").GetComponent<TMP_Text>().text =
                TermsPanelView.AllAgreeLabel;
            allAgree.ViewButton.gameObject.SetActive(false);
            foreach (string moved in new[] { "Consents", "VoiceConsentNote" })
            {
                Transform old = root.transform.Find("SignupPanel/" + moved);
                if (old != null && panel.Find(moved) == null)
                    old.SetParent(panel, false);
            }

            RectTransform consents = UiBuilderTools.FindOrCreateColumn(panel, "Consents");
            ConsentRowView consentTemplate = SetUpConsentTemplate(consents, regular);
            // 음성 처리는 선택 동의라, 동의해야 [말하기]를 쓸 수 있다는 것과 나중에 바꾸는 곳을 약관 바로 아래에 알린다.
            TMP_Text voiceNote = UiBuilderTools.FindOrCreateText(
                panel,
                "VoiceConsentNote",
                regular,
                20f,
                SubTextColor,
                SignupVoiceNote
            );
            voiceNote.text = SignupVoiceNote;
            voiceNote.transform.SetSiblingIndex(consents.GetSiblingIndex() + 1);
            Button next = UiBuilderTools.FindOrCreateButton(
                panel,
                "NextButton",
                "다음",
                bold,
                PanelWidth
            );
            next.transform.SetAsLastSibling();

            BuilderTools.SetReference(view, "allAgreeRow", allAgree);
            BuilderTools.SetReference(view, "consentsParent", consents);
            BuilderTools.SetReference(view, "consentTemplate", consentTemplate);
            BuilderTools.SetReference(view, "nextButton", next);
            BuilderTools.RequireReferences(view);
            return view;
        }

        /// <summary>
        /// 입구 화면 프리팹에서 가입을 약관 동의 → 가입 정보 두 단계로 나누고(S15P31A305-596) 테마·시안 배치를 다시 입힌다.
        /// 입구 전체 Builder를 다시 돌리지 않아도 된다.
        /// </summary>
        [MenuItem("Tools/A305/Onboarding/Apply Terms Step")]
        public static void ApplyTermsStep()
        {
            BuilderTools.UpdatePrefabIfExists(
                ScreensPrefabPath,
                root =>
                {
                    TMP_FontAsset bold = UiBuilderTools.BoldFont();
                    TMP_FontAsset regular = UiBuilderTools.RegularFont();
                    var screen = root.GetComponent<OnboardingScreen>();
                    BuilderTools.SetReference(
                        screen,
                        "termsPanel",
                        SetUpTermsPanel(root, bold, regular)
                    );
                    BuilderTools.SetReference(
                        screen,
                        "signupPanel",
                        SetUpSignupPanel(root, bold, regular)
                    );
                    BuilderTools.RequireReferences(screen);
                }
            );
            A305.EditorTools.Theme.UiThemeBuilder.ApplyTermsStepTheme();
            AssetDatabase.SaveAssets();
        }

        private static TMP_InputField CreateNicknameInput(Transform parent, TMP_FontAsset regular)
        {
            TMP_InputField nickname = UiBuilderTools.FindOrCreateInputField(
                parent,
                "NicknameInput",
                regular,
                "NPC가 부를 이름"
            );
            nickname.characterLimit = NicknameCharacterLimit;
            // Web 빌드의 입력창은 한글 조합(IME)을 받지 못해 브라우저 입력창을 겹쳐 받는다.
            BuilderTools.GetOrAddComponent<BrowserImeInput>(nickname.gameObject);
            return nickname;
        }

        // 약관 수는 서버 데이터에 따라 달라지므로 한 줄을 템플릿으로 두고 실행 중에 복제한다.
        private static ConsentRowView SetUpConsentTemplate(
            Transform consents,
            TMP_FontAsset regular
        )
        {
            return SetUpConsentRow(consents, "ConsentRowTemplate", regular);
        }

        /// <summary>동의 한 줄(체크·제목·[보기]). 가입·게스트 화면과 마이페이지가 함께 쓴다.</summary>
        internal static ConsentRowView SetUpConsentRow(
            Transform parent,
            string rowName,
            TMP_FontAsset regular
        )
        {
            bool isNew = parent.Find(rowName) == null;
            RectTransform template = UiBuilderTools.FindOrCreateButtonRow(parent, rowName);
            if (isNew)
            {
                template.GetComponent<HorizontalLayoutGroup>().childAlignment =
                    TextAnchor.MiddleLeft;
            }

            var row = BuilderTools.GetOrAddComponent<ConsentRowView>(template.gameObject);
            Toggle toggle = FindOrCreateToggle(template, "Toggle");
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                template,
                "Label",
                regular,
                22f,
                Color.white
            );
            if (isNew)
            {
                BuilderTools.GetOrAddComponent<LayoutElement>(label.gameObject).flexibleWidth = 1f;
            }

            Button view = FindOrCreateSecondaryButton(
                template,
                "ViewButton",
                "보기",
                regular,
                100f
            );

            BuilderTools.SetReference(row, "toggle", toggle);
            BuilderTools.SetReference(row, "labelText", label);
            BuilderTools.SetReference(row, "viewButton", view);
            BuilderTools.RequireReferences(row);
            return row;
        }

        private static LanguagePanelView SetUpLanguagePanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "LanguagePanel");
            var view = BuilderTools.GetOrAddComponent<LanguagePanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "배울 언어를 골라 주세요"
            );
            RectTransform cards = UiBuilderTools.FindOrCreateColumn(panel, "Cards");

            BuilderTools.SetReference(view, "cardsParent", cards);
            BuilderTools.SetReference(
                view,
                "cardTemplate",
                SetUpCardTemplate(cards, bold, regular)
            );
            BuilderTools.RequireReferences(view);
            return view;
        }

        // 화면 04는 안내·문항·결과를 한 창에서 바꿔 보여 기능 구성과 시안 배치를 함께 만든다(LevelTestScreenBuilder).
        private static LevelTestPanelView SetUpLevelTestPanel(GameObject root)
        {
            RectTransform panel = CenterPanel(root.transform, "LevelTestPanel");
            return LevelTestScreenBuilder.Apply(panel);
        }

        // 접속 상태를 확인하지 못했을 때의 화면. 실패한 이유는 화면 아래 안내 문구가 보여 준다.
        private static ConnectionPanelView SetUpConnectionPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "ConnectionPanel");
            var view = BuilderTools.GetOrAddComponent<ConnectionPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "접속하지 못했어요"
            );
            UiBuilderTools.FindOrCreateText(
                panel,
                "Body",
                regular,
                24f,
                SubTextColor,
                "인터넷 연결을 확인한 뒤 다시 시도해 주세요."
            );
            Button retry = UiBuilderTools.FindOrCreateButton(
                panel,
                "RetryButton",
                "다시 시도",
                bold,
                PanelWidth
            );

            BuilderTools.SetReference(view, "retryButton", retry);
            BuilderTools.RequireReferences(view);
            return view;
        }

        // 문구는 화면이 열릴 때 GuestReturnPanelView가 화면 종류에 맞춰 바꾼다. 여기 문구는 게스트 다시 접속 화면이다.
        private static GuestReturnPanelView SetUpGuestReturnPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "GuestReturnPanel");
            var view = BuilderTools.GetOrAddComponent<GuestReturnPanelView>(panel.gameObject);
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                GuestReturnText.GuestTitle
            );
            TMP_Text body = UiBuilderTools.FindOrCreateText(
                panel,
                "Body",
                regular,
                22f,
                SubTextColor,
                GuestReturnText.GuestBody
            );
            TMP_Text notice = UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                regular,
                20f,
                BadgeColor
            );
            Button signup = UiBuilderTools.FindOrCreateButton(
                panel,
                "SignupButton",
                GuestReturnText.Signup,
                bold,
                PanelWidth
            );
            Button continueButton = FindOrCreateSecondaryButton(
                panel,
                "ContinueButton",
                GuestReturnText.ContinueAsGuest,
                regular,
                PanelWidth
            );

            BuilderTools.SetReference(view, "titleText", title);
            BuilderTools.SetReference(view, "bodyText", body);
            BuilderTools.SetReference(view, "noticeText", notice);
            BuilderTools.SetReference(view, "signupButton", signup);
            BuilderTools.SetReference(view, "continueButton", continueButton);
            BuilderTools.RequireReferences(view);
            // 게임에 들어가기 전에만 보이므로, Play 전 프리팹 화면에서 다른 창과 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
            return view;
        }

        private static LevelSettingPanelView SetUpLevelSettingPanel(
            GameObject root,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform panel = CenterPanel(root.transform, "LevelSettingPanel");
            var view = BuilderTools.GetOrAddComponent<LevelSettingPanelView>(panel.gameObject);
            UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                36f,
                Color.white,
                "대화 난이도를 설정하세요"
            );
            RectTransform cards = UiBuilderTools.FindOrCreateColumn(panel, "Cards");
            ChoiceCardView cardTemplate = SetUpCardTemplate(cards, bold, regular);
            Button confirm = UiBuilderTools.FindOrCreateButton(
                panel,
                "ConfirmButton",
                "설정 완료",
                bold,
                PanelWidth
            );

            BuilderTools.SetReference(view, "cardsParent", cards);
            BuilderTools.SetReference(view, "cardTemplate", cardTemplate);
            BuilderTools.SetReference(view, "confirmButton", confirm);
            BuilderTools.RequireReferences(view);
            return view;
        }

        // 언어·난이도 카드. 개수가 서버 데이터에 따라 달라지므로 템플릿으로 두고 실행 중에 복제한다.
        private static ChoiceCardView SetUpCardTemplate(
            Transform cards,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            bool isNew = cards.Find("CardTemplate") == null;
            RectTransform template = UiBuilderTools.FindOrCreateColumn(cards, "CardTemplate");
            var background = BuilderTools.GetOrAddComponent<Image>(template.gameObject);
            if (isNew)
            {
                template.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(
                    24,
                    24,
                    16,
                    16
                );
                background.color = CardColor;
            }

            var button = BuilderTools.GetOrAddComponent<Button>(template.gameObject);
            button.targetGraphic = background;
            var card = BuilderTools.GetOrAddComponent<ChoiceCardView>(template.gameObject);

            TMP_Text title = UiBuilderTools.FindOrCreateText(
                template,
                "Title",
                bold,
                28f,
                Color.white
            );
            TMP_Text description = UiBuilderTools.FindOrCreateText(
                template,
                "Description",
                regular,
                20f,
                SubTextColor
            );
            TMP_Text badge = UiBuilderTools.FindOrCreateText(
                template,
                "Badge",
                bold,
                18f,
                BadgeColor
            );

            BuilderTools.SetReference(card, "button", button);
            BuilderTools.SetReference(card, "background", background);
            BuilderTools.SetReference(card, "titleText", title);
            BuilderTools.SetReference(card, "descriptionText", description);
            BuilderTools.SetReference(card, "badgeText", badge);
            BuilderTools.RequireReferences(card);
            return card;
        }

        private static RectTransform CenterPanel(Transform root, string panelName)
        {
            return UiBuilderTools.FindOrCreateAnchoredPanel(
                root,
                panelName,
                new Vector2(0.5f, 0.5f),
                PanelWidth + 80f,
                Vector2.zero
            );
        }

        private static void EnsureBackground(Transform root)
        {
            if (root.Find("Background") != null)
            {
                return;
            }

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)background.transform;
            rect.SetParent(root, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = BackgroundColor;
        }

        private static void AnchorBottom(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(PanelWidth, 40f);
            rect.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        }

        // 보조 동작 버튼은 처음 만들 때만 회색으로 칠한다. 이미 있으면 바꾼 디자인을 유지한다.
        private static Button FindOrCreateSecondaryButton(
            Transform parent,
            string buttonName,
            string label,
            TMP_FontAsset font,
            float preferredWidth
        )
        {
            bool isNew = parent.Find(buttonName) == null;
            Button button = UiBuilderTools.FindOrCreateButton(
                parent,
                buttonName,
                label,
                font,
                preferredWidth
            );
            if (isNew)
            {
                ((Image)button.targetGraphic).color = SecondaryButtonColor;
            }

            return button;
        }

        private static Toggle FindOrCreateToggle(Transform parent, string toggleName)
        {
            Transform existing = parent.Find(toggleName);
            if (existing != null && existing.TryGetComponent(out Toggle existingToggle))
            {
                return existingToggle;
            }

            var toggleObject = new GameObject(toggleName, typeof(RectTransform), typeof(Image));
            toggleObject.transform.SetParent(parent, false);
            var box = toggleObject.GetComponent<Image>();
            box.color = Color.white;
            var size = toggleObject.AddComponent<LayoutElement>();
            size.preferredWidth = 36f;
            size.preferredHeight = 36f;

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            var checkmarkRect = (RectTransform)checkmark.transform;
            checkmarkRect.SetParent(toggleObject.transform, false);
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            checkmark.GetComponent<Image>().color = new Color(0.24f, 0.42f, 0.85f, 1f);

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = false;
            return toggle;
        }
    }
}
