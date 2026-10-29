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
    /// 튜토리얼 안내 카드 프리팹과 안내 문구를 준비하고, 공간 씬에 튜토리얼 진행 처리를 붙인다.
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

        private const string GuidePrefabPath = "Assets/Prefabs/Tutorial/TutorialGuide.prefab";
        private const string GuideDefinitionPath = "Assets/Data/Tutorial/TutorialGuide.asset";

        // 미션 창(105)·대화 제안(110) 위에 두지만, 제안 화면이 떠 있거나 대화 중이면 카드를 숨긴다.
        private const int GuideSortingOrder = 115;

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color TaskColor = new Color(0.62f, 0.8f, 1f);

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
            BuilderTools.LoadOrCreateAsset<TutorialGuideDefinition>(GuideDefinitionPath);
            BuilderTools.UpdateOrCreatePrefab(GuidePrefabPath, "TutorialGuide", SetUpGuide);
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
            ConnectToApi(tutorial);
            BuilderTools.RequireReferences(tutorial);
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
    }
}
