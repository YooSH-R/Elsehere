using System;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Tutorial
{
    /// <summary>
    /// 튜토리얼 안내 카드·코치 마크 프리팹과 안내 문구를 준비하고, 공간 씬에 튜토리얼 진행 처리를 붙인다.
    /// 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class TutorialBuilder
    {
        public const string MenuPath = "Tools/A305/Tutorial/Build";

        private const string ApiSettingsPath = "Assets/Data/Api/ApiSettings.asset";

        private static readonly string[] ProductionSpaceScenes =
        {
            "HomeScene",
            "TownScene",
            "CafeScene",
            "HotelScene",
            "ShopScene",
        };

        private const string SandboxFolder = "Assets/Scenes/Sandbox";
        private const string GuidePrefabPath = "Assets/Prefabs/Tutorial/TutorialGuide.prefab";
        private const string CoachMarkPrefabPath = "Assets/Prefabs/Tutorial/CoachMark.prefab";
        private const string BadgeSpritePath = "Assets/Art/UI/Theme/Jelly/Badge.png";
        private const string CardSpritePath = "Assets/Art/UI/Theme/Jelly/Card.png";
        private const string ArrowSpritePath = "Assets/Art/UI/Theme/Icons/Next.png";
        private const string GuideDefinitionPath = "Assets/Data/Tutorial/TutorialGuide.asset";

        // 미션 창(105)·대화 제안(110) 위에 두지만, 제안 화면이 떠 있거나 대화 중이면 카드를 숨긴다.
        private const int GuideSortingOrder = 115;

        // 코치 마크는 밝힐 버튼의 캔버스 바로 위로 그리는 순서를 바꾼다. 처음 값은 안내 카드 바로 위다.
        private const int CoachMarkSortingOrder = GuideSortingOrder + 1;
        private const float FrameThickness = 5f;
        private const float SkipButtonWidth = 180f;

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);

        // Linear 색 공간에서 섞이므로 눈으로 절반쯤 어두워 보이게 진하게 둔다.
        private static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color HighlightColor = new Color(1f, 0.84f, 0.29f);
        private static readonly Color CaptionColor = new Color(1f, 0.97f, 0.92f);
        private static readonly Color CaptionTextColor = new Color(0.23f, 0.18f, 0.16f);

        // 밝힌 곳 옆에 두는 설명 말풍선 너비. 대화창 왼쪽 도움 카드 정도라 옆자리에 들어간다.
        private const float CaptionWidth = 440f;

        // 잠긴 문 안내 띠: 연한 주황 바탕에 진한 주황 글자
        private static readonly Color NoticeColor = new Color(0.99f, 0.88f, 0.8f);
        private static readonly Color NoticeTextColor = new Color(0.66f, 0.27f, 0.11f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"튜토리얼 안내 카드를 준비했습니다: {GuidePrefabPath}");
        }

        /// <summary>안내 문구(없을 때만)와 안내 카드 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            AlignGuideNpc(
                BuilderTools.LoadOrCreateAsset<TutorialGuideDefinition>(GuideDefinitionPath)
            );
            BuilderTools.UpdateOrCreatePrefab(GuidePrefabPath, "TutorialGuide", SetUpGuide);
            BuilderTools.UpdateOrCreatePrefab(CoachMarkPrefabPath, "CoachMark", SetUpCoachMark);
        }

        /// <summary>
        /// 튜토리얼 단계가 바뀌어 안내 문구를 새로 맞춰야 할 때, 단계별 안내와 코치 마크 설명을 코드의 기본 문구로 되돌린다.
        /// 평소 Build는 고친 문구를 그대로 둔다.
        /// </summary>
        [MenuItem("Tools/A305/Tutorial/Reset Guide Texts")]
        public static void ResetGuideTexts()
        {
            BuilderTools.ThrowIfPlaying();
            var guide = BuilderTools.LoadOrCreateAsset<TutorialGuideDefinition>(
                GuideDefinitionPath
            );
            var defaults = ScriptableObject.CreateInstance<TutorialGuideDefinition>();
            var fields = new SerializedObject(guide);
            var defaultFields = new SerializedObject(defaults);
            foreach (
                string name in new[]
                {
                    "stepGuides",
                    "talkOfferCaption",
                    "dialogueLinesCaption",
                    "dialogueToolsCaption",
                    "guidedLineCaption",
                    "suggestedSentenceCaption",
                    "suggestedWordsCaption",
                    "guidedLineMissCaption",
                    "dialogueEndCaption",
                    "travelLockedMessage",
                    "vocabularyCaption",
                }
            )
            {
                fields.CopyFromSerializedProperty(defaultFields.FindProperty(name));
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(guide);
            AssetDatabase.SaveAssets();
            Debug.Log($"튜토리얼 안내 문구를 기본 문구로 맞췄습니다: {GuideDefinitionPath}");
        }

        /// <summary>
        /// 튜토리얼 진행이 있는 장소 씬(배포용 5개와 공간 기능을 붙인 Sandbox 씬)에 코치 마크와 단계 신호를 받는 화면
        /// (대화·메뉴·피드백·지도)을 연결한다. 디자인한 씬의 다른 객체는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/A305/Tutorial/Connect Coach Marks")]
        public static void ConnectCoachMarks()
        {
            BuilderTools.ThrowIfPlaying();
            PrepareAssets();
            UiThemeBuilder.StyleTutorialGuide();
            foreach (string name in ProductionSpaceScenes)
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                if (!ConnectCoachMarks(path))
                {
                    throw new InvalidOperationException(
                        $"{path}에 TutorialDirector가 없습니다. 공간 기능 연결을 먼저 실행합니다."
                    );
                }
            }

            // Sandbox 씬은 공간 기능을 붙인 씬만 연결해, 연결이 빠진 튜토리얼 진행이 오류를 내지 않게 한다.
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SandboxFolder }))
            {
                ConnectCoachMarks(AssetDatabase.GUIDToAssetPath(guid));
            }

            Debug.Log("튜토리얼 진행이 있는 장소 씬에 코치 마크와 단계 신호 화면을 연결했습니다.");
        }

        // 씬에 튜토리얼 진행이 있으면 연결하고 저장한다. 없으면 false.
        private static bool ConnectCoachMarks(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            var tutorial = BuilderTools.FindSingleInScene<TutorialDirector>(scene);
            if (tutorial == null)
            {
                return false;
            }

            ConnectFlowViews(tutorial, BuilderTools.EnsureGroups(scene));
            // 서버 설정과 첫날 미션 화면은 서버 없이 씬만 확인하는 Sandbox에 없을 수 있다.
            BuilderTools.RequireReferences(tutorial, "apiSettings", "firstDayMission");
            BuilderTools.SaveScene(scene, path);
            return true;
        }

        // 길을 묻는 NPC(마을 행인 Ben)와 그동안 쓰는 대본 키는 코드와 장면이 함께 정하는 연결값이라 코드의 기본값으로 맞춘다.
        // 안내 문구는 고친 그대로 둔다.
        private static void AlignGuideNpc(TutorialGuideDefinition guide)
        {
            var defaults = ScriptableObject.CreateInstance<TutorialGuideDefinition>();
            var fields = new SerializedObject(guide);
            fields.FindProperty("tutorialNpcCode").stringValue = defaults.TutorialNpcCode;
            fields.FindProperty("tutorialScriptKey").stringValue = defaults.TutorialScriptKey;
            fields.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(defaults);
        }

        /// <summary>
        /// 씬에 안내 카드와 튜토리얼 진행 처리가 없으면 붙이고, 플레이어·사물 툴팁·대화·피드백·미션·씬에 놓인 행인과 연결한다.
        /// </summary>
        public static void ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            DialogueController dialogue,
            LearningController learning,
            MissionController mission
        )
        {
            var guideView = BuilderTools.FindSingleInScene<TutorialGuideView>(groups.Scene);
            if (guideView == null)
            {
                guideView = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(GuidePrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<TutorialGuideView>();
            }

            var tutorial = BuilderTools.FindSingleInScene<TutorialDirector>(groups.Scene);
            if (tutorial == null)
            {
                tutorial = BuilderTools
                    .CreateEmpty("Tutorial", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<TutorialDirector>();
            }

            BuilderTools.SetReference(
                tutorial,
                "guide",
                BuilderTools.LoadRequiredAsset<TutorialGuideDefinition>(
                    GuideDefinitionPath,
                    MenuPath
                )
            );
            BuilderTools.SetReference(tutorial, "guideView", guideView);
            BuilderTools.SetReference(tutorial, "player", player);
            BuilderTools.SetReference(
                tutorial,
                "wordTooltip",
                BuilderTools.FindSingleInScene<WordTooltipView>(groups.Scene)
            );
            BuilderTools.SetReference(tutorial, "dialogue", dialogue);
            BuilderTools.SetReference(
                tutorial,
                "offerView",
                BuilderTools.FindSingleInScene<PasserbyOfferView>(groups.Scene)
            );
            BuilderTools.SetReference(tutorial, "learning", learning);
            BuilderTools.SetReference(tutorial, "mission", mission);
            SetPassersby(tutorial, BuilderTools.FindAllInScene<Passerby>(groups.Scene));
            ConnectFlowViews(tutorial, groups);
            ConnectToApi(tutorial);
            BuilderTools.RequireReferences(tutorial);
        }

        // 코치 마크가 없으면 붙이고, 단계 신호를 받거나 코치 마크가 밝힐 화면을 연결한다.
        private static void ConnectFlowViews(TutorialDirector tutorial, SceneGroups groups)
        {
            var coachMark = BuilderTools.FindSingleInScene<CoachMarkView>(groups.Scene);
            if (coachMark == null)
            {
                coachMark = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(CoachMarkPrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<CoachMarkView>();
            }

            BuilderTools.SetReference(tutorial, "coachMark", coachMark);
            BuilderTools.SetReference(
                tutorial,
                "dialoguePanel",
                BuilderTools.FindSingleInScene<DialoguePanelView>(groups.Scene)
            );
            BuilderTools.SetReference(
                tutorial,
                "hud",
                BuilderTools.FindSingleInScene<LearningHudView>(groups.Scene)
            );
            BuilderTools.SetReference(
                tutorial,
                "hudMenu",
                BuilderTools.FindSingleInScene<HudMenuView>(groups.Scene)
            );
            BuilderTools.SetReference(
                tutorial,
                "feedbackView",
                BuilderTools.FindSingleInScene<FeedbackView>(groups.Scene)
            );
            BuilderTools.SetReference(
                tutorial,
                "firstDayMission",
                BuilderTools.FindSingleInScene<FirstDayMissionView>(groups.Scene)
            );
        }

        /// <summary>
        /// 배포용 장소 씬의 튜토리얼 진행에 API 서버 설정만 연결한다. 디자인한 씬의 다른 객체는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/A305/Tutorial/Connect Tutorial Saving")]
        public static void ConnectTutorialSaving()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (string name in ProductionSpaceScenes)
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                var tutorial = BuilderTools.FindSingleInScene<TutorialDirector>(scene);
                if (tutorial == null)
                {
                    throw new InvalidOperationException(
                        $"{path}에 TutorialDirector가 없습니다. 공간 기능 연결을 먼저 실행합니다."
                    );
                }

                ConnectToApi(tutorial);
                BuilderTools.SaveScene(scene, path);
            }

            Debug.Log("배포용 장소 씬 5개의 튜토리얼 진행에 끝낸 기록 저장을 연결했습니다.");
        }

        private static void ConnectToApi(TutorialDirector tutorial)
        {
            BuilderTools.SetReference(
                tutorial,
                "apiSettings",
                BuilderTools.LoadRequiredAsset<ApiSettings>(
                    ApiSettingsPath,
                    A305.EditorTools.Onboarding.OnboardingBuilder.MenuPath
                )
            );
        }

        private static void SetPassersby(TutorialDirector tutorial, Passerby[] passersby)
        {
            var fields = new SerializedObject(tutorial);
            SerializedProperty list = fields.FindProperty("passersby");
            list.arraySize = passersby.Length;
            for (int i = 0; i < passersby.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = passersby[i];
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetUpGuide(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, GuideSortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<TutorialGuideView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "Panel",
                new Vector2(0.5f, 1f),
                680f,
                new Vector2(0f, -24f)
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();

            // 한 줄 안내 카드: 윗줄(진행 점·단계 이름·[건너뛰기]), 할 일 한 줄, 필요할 때만 주 버튼.
            // 예전의 긴 설명(Body)과 버튼 줄의 큰 [튜토리얼 건너뛰기]는 지운다.
            RemoveChild(panel, "Body");
            RemoveChild(panel.Find("Buttons"), "SkipButton");
            RectTransform content = UiBuilderTools.FindOrCreateColumn(panel, "Content");
            content.SetAsFirstSibling();
            RectTransform header = UiBuilderTools.FindOrCreateButtonRow(content, "Header");
            MoveInto(panel, "Title", header);
            MoveInto(panel, "Task", content);
            MoveInto(panel, "Buttons", content);
            header.SetAsFirstSibling();

            TMP_Text title = UiBuilderTools.FindOrCreateText(
                header,
                "Title",
                regular,
                18f,
                SubTextColor
            );
            title.fontSize = 18f;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            BuilderTools.GetOrAddComponent<LayoutElement>(title.gameObject).flexibleWidth = 1f;
            title.transform.SetAsFirstSibling();
            Button skipButton = UiBuilderTools.FindOrCreateButton(
                header,
                "SkipButton",
                "건너뛰기",
                bold,
                SkipButtonWidth
            );
            // 테마의 알약 버튼은 글자를 아이콘 오른쪽에 두므로, 아이콘과 네 글자가 함께 들어가는 폭으로 맞춘다.
            skipButton.GetComponent<LayoutElement>().preferredWidth = SkipButtonWidth;

            TMP_Text task = UiBuilderTools.FindOrCreateText(
                content,
                "Task",
                bold,
                26f,
                Color.white
            );
            task.fontSize = 26f;
            task.alignment = TextAlignmentOptions.MidlineLeft;
            task.transform.SetSiblingIndex(1);
            RectTransform notice = SetUpNotice(content, bold);
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(content, "Buttons");
            Button continueButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "ContinueButton",
                "시작하기",
                bold,
                240f
            );

            // [건너뛰기]를 누르면 위 묶음 대신 보이는 확인.
            RectTransform confirm = UiBuilderTools.FindOrCreateColumn(panel, "SkipConfirm");
            confirm.SetAsLastSibling();
            TMP_Text question = UiBuilderTools.FindOrCreateText(
                confirm,
                "Question",
                bold,
                24f,
                Color.white,
                "튜토리얼을 건너뛸까요?\n<size=80%>건너뛰면 기본 안내를 다시 볼 수 없어요.</size>"
            );
            question.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform confirmButtons = UiBuilderTools.FindOrCreateButtonRow(
                confirm,
                "ConfirmButtons"
            );
            Button keepGoingButton = UiBuilderTools.FindOrCreateButton(
                confirmButtons,
                "KeepGoingButton",
                "계속하기",
                bold,
                200f
            );
            Button confirmSkipButton = UiBuilderTools.FindOrCreateButton(
                confirmButtons,
                "ConfirmSkipButton",
                "건너뛰기",
                bold,
                200f
            );
            confirm.gameObject.SetActive(false);

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "titleText", title);
            BuilderTools.SetReference(view, "taskText", task);
            BuilderTools.SetReference(view, "continueButton", continueButton);
            BuilderTools.SetReference(
                view,
                "continueButtonLabel",
                continueButton.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(view, "skipButton", skipButton);
            BuilderTools.SetReference(view, "content", content.gameObject);
            BuilderTools.SetReference(view, "skipConfirm", confirm.gameObject);
            BuilderTools.SetReference(view, "keepGoingButton", keepGoingButton);
            BuilderTools.SetReference(view, "confirmSkipButton", confirmSkipButton);
            BuilderTools.SetReference(view, "notice", notice.gameObject);
            BuilderTools.SetReference(
                view,
                "noticeText",
                notice.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.RequireReferences(view);

            // 단계에 맞는 안내가 있을 때만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        // 잠긴 문 안내처럼 할 일 바로 아래 잠깐 보이는 주의 띠. 카드 글자와 섞이지 않게 테마와 상관없이 주의 색 바탕에 굵게 쓴다.
        private static RectTransform SetUpNotice(Transform content, TMP_FontAsset bold)
        {
            // 예전에는 글자 하나만 두었으므로 띠로 다시 만든다.
            Transform oldText = content.Find("Notice");
            if (oldText != null && oldText.GetComponent<TMP_Text>() != null)
            {
                UnityEngine.Object.DestroyImmediate(oldText.gameObject);
            }

            Image strip = FindOrCreateImage(content, "Notice");
            strip.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardSpritePath);
            strip.type = Image.Type.Sliced;
            strip.color = NoticeColor;
            strip.raycastTarget = false;
            var layout = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(strip.gameObject);
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text label = UiBuilderTools.FindOrCreateText(
                strip.transform,
                "Label",
                bold,
                22f,
                NoticeTextColor
            );
            label.fontSize = 22f;
            label.color = NoticeTextColor;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;

            RectTransform notice = strip.rectTransform;
            notice.SetSiblingIndex(2);
            notice.gameObject.SetActive(false);
            return notice;
        }

        // 구멍 둘레를 덮는 판 넷, 구멍 테두리, 설명 말풍선, 장면 속 대상을 가리키는 화살표 배지.
        private static void SetUpCoachMark(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, CoachMarkSortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<CoachMarkView>(root);

            string[] shadeNames = { "ShadeBelow", "ShadeAbove", "ShadeLeft", "ShadeRight" };
            var fields = new SerializedObject(view);
            SerializedProperty shades = fields.FindProperty("shades");
            shades.arraySize = shadeNames.Length;
            for (int i = 0; i < shadeNames.Length; i++)
            {
                Image shade = FindOrCreateImage(root.transform, shadeNames[i]);
                shade.color = ShadeColor;
                shade.raycastTarget = true;
                shade.gameObject.SetActive(false);
                shades.GetArrayElementAtIndex(i).objectReferenceValue = shade.rectTransform;
            }

            fields.ApplyModifiedPropertiesWithoutUndo();

            RectTransform frame = FindOrCreateRect(root.transform, "Frame");
            var frameGroup = BuilderTools.GetOrAddComponent<CanvasGroup>(frame.gameObject);
            frameGroup.blocksRaycasts = false;
            frameGroup.interactable = false;
            SetUpFrameBar(frame, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f));
            SetUpFrameBar(frame, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f));
            SetUpFrameBar(frame, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f));
            SetUpFrameBar(frame, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f));
            frame.gameObject.SetActive(false);

            // 설명 말풍선. 너비는 정해 두고 높이는 글 길이에 맞추며, 아래에 [다음]을 오른쪽으로 붙인다.
            Image caption = FindOrCreateImage(root.transform, "Caption");
            caption.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardSpritePath);
            caption.type = Image.Type.Sliced;
            caption.color = CaptionColor;
            caption.raycastTarget = false;
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.zero;
            captionRect.pivot = Vector2.zero;
            captionRect.sizeDelta = new Vector2(CaptionWidth, 120f);
            var captionLayout = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(
                caption.gameObject
            );
            captionLayout.padding = new RectOffset(26, 26, 20, 20);
            captionLayout.spacing = 14f;
            captionLayout.childAlignment = TextAnchor.UpperRight;
            captionLayout.childControlWidth = captionLayout.childControlHeight = true;
            captionLayout.childForceExpandWidth = captionLayout.childForceExpandHeight = false;
            var captionFitter = BuilderTools.GetOrAddComponent<ContentSizeFitter>(
                caption.gameObject
            );
            captionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            captionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image tail = FindOrCreateImage(captionRect, "Tail");
            tail.color = CaptionColor;
            tail.raycastTarget = false;
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = Vector2.zero;
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.sizeDelta = new Vector2(28f, 28f);
            BuilderTools.GetOrAddComponent<LayoutElement>(tail.gameObject).ignoreLayout = true;
            tail.transform.SetAsFirstSibling();

            TMP_Text captionText = UiBuilderTools.FindOrCreateText(
                captionRect,
                "Text",
                UiBuilderTools.BoldFont(),
                22f,
                CaptionTextColor
            );
            captionText.alignment = TextAlignmentOptions.TopLeft;
            captionText.enableAutoSizing = false;
            captionText.fontSize = 22f;
            captionText.textWrappingMode = TextWrappingModes.Normal;
            captionText.raycastTarget = false;
            BuilderTools.GetOrAddComponent<LayoutElement>(captionText.gameObject).flexibleWidth =
                1f;

            Button nextButton = UiBuilderTools.FindOrCreateButton(
                captionRect,
                "NextButton",
                "다음",
                UiBuilderTools.BoldFont(),
                150f
            );
            var nextSize = nextButton.GetComponent<LayoutElement>();
            nextSize.preferredHeight = 52f;
            nextButton.transform.SetAsLastSibling();
            nextButton.gameObject.SetActive(false);
            caption.gameObject.SetActive(false);

            Image marker = FindOrCreateImage(root.transform, "Marker");
            marker.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BadgeSpritePath);
            marker.color = HighlightColor;
            marker.raycastTarget = false;
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = Vector2.zero;
            markerRect.anchorMax = Vector2.zero;
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(76f, 76f);
            Image arrow = FindOrCreateImage(markerRect, "Arrow");
            arrow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
            arrow.raycastTarget = false;
            arrow.rectTransform.sizeDelta = new Vector2(52f, 52f);
            // 오른쪽을 가리키는 아이콘을 코치 마크가 대상 쪽으로 돌린다. 처음에는 아래를 가리킨다.
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            marker.gameObject.SetActive(false);

            BuilderTools.SetReference(view, "canvas", root.GetComponent<Canvas>());
            BuilderTools.SetReference(view, "frame", frameGroup);
            BuilderTools.SetReference(view, "caption", captionRect);
            BuilderTools.SetReference(view, "captionText", captionText);
            BuilderTools.SetReference(view, "captionTail", tail.rectTransform);
            BuilderTools.SetReference(view, "nextButton", nextButton);
            BuilderTools.SetReference(view, "marker", markerRect);
            BuilderTools.SetReference(view, "markerArrow", arrow.rectTransform);
            BuilderTools.RequireReferences(view);
        }

        // 테두리 한 변. 위·아래 변은 가로로, 왼쪽·오른쪽 변은 세로로 구멍 가장자리를 따라 늘어난다.
        private static void SetUpFrameBar(
            RectTransform frame,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            Image bar = FindOrCreateImage(frame, name);
            bar.color = HighlightColor;
            bar.raycastTarget = false;
            bool isHorizontal = anchorMin.y == anchorMax.y;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = isHorizontal
                ? new Vector2(0.5f, anchorMin.y)
                : new Vector2(anchorMin.x, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = isHorizontal
                ? new Vector2(0f, FrameThickness)
                : new Vector2(FrameThickness, 0f);
        }

        // 예전 자리에 있는 자식을 새 묶음으로 옮긴다. 이미 옮겼으면 그대로 둔다.
        private static void MoveInto(Transform from, string name, Transform to)
        {
            Transform child = from.Find(name);
            if (child != null && to.Find(name) == null)
            {
                child.SetParent(to, false);
            }
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(parent, false);
            }

            return (RectTransform)child;
        }

        private static Image FindOrCreateImage(Transform parent, string name)
        {
            RectTransform rect = FindOrCreateRect(parent, name);
            return BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
        }
    }
}
