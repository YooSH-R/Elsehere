using System;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
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

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color TaskColor = new Color(0.62f, 0.8f, 1f);
        // Linear 색 공간에서 섞이므로 눈으로 절반쯤 어두워 보이게 진하게 둔다.
        private static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color HighlightColor = new Color(1f, 0.84f, 0.29f);
        private static readonly Color CaptionColor = new Color(1f, 0.97f, 0.92f);
        private static readonly Color CaptionTextColor = new Color(0.23f, 0.18f, 0.16f);

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
                    "guidedLineCaption",
                    "dialogueEndCaption",
                    "travelLockedMessage",
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
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                panel,
                "Title",
                bold,
                26f,
                Color.white
            );
            TMP_Text body = UiBuilderTools.FindOrCreateText(
                panel,
                "Body",
                regular,
                22f,
                SubTextColor
            );
            TMP_Text task = UiBuilderTools.FindOrCreateText(panel, "Task", regular, 20f, TaskColor);
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button skipButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "SkipButton",
                "튜토리얼 건너뛰기",
                bold,
                240f
            );
            Button continueButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "ContinueButton",
                "다음으로",
                bold,
                240f
            );

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "titleText", title);
            BuilderTools.SetReference(view, "bodyText", body);
            BuilderTools.SetReference(view, "taskText", task);
            BuilderTools.SetReference(view, "continueButton", continueButton);
            BuilderTools.SetReference(
                view,
                "continueButtonLabel",
                continueButton.GetComponentInChildren<TMP_Text>(true)
            );
            BuilderTools.SetReference(view, "skipButton", skipButton);
            BuilderTools.RequireReferences(view);

            // 단계에 맞는 안내가 있을 때만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
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

            Image caption = FindOrCreateImage(root.transform, "Caption");
            caption.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardSpritePath);
            caption.type = Image.Type.Sliced;
            caption.color = CaptionColor;
            caption.raycastTarget = false;
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.zero;
            captionRect.pivot = Vector2.zero;
            captionRect.sizeDelta = new Vector2(680f, 156f);
            TMP_Text captionText = UiBuilderTools.FindOrCreateText(
                captionRect,
                "Text",
                UiBuilderTools.BoldFont(),
                22f,
                CaptionTextColor
            );
            RectTransform textRect = captionText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 16f);
            textRect.offsetMax = new Vector2(-24f, -16f);
            captionText.alignment = TextAlignmentOptions.Center;
            captionText.enableAutoSizing = true;
            captionText.fontSizeMin = 16f;
            captionText.fontSizeMax = 22f;
            captionText.raycastTarget = false;
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
