using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Player;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 장소 씬에 영업시간 처리(<see cref="OpeningHoursController"/>)와 화면 위 가운데 안내판(<see cref="HudNoticeView"/>)을 붙이고 연결한다.
    /// 안내판 프리팹은 택시 상태 판(TaxiTripStatus)과 같은 자리·테마로 없을 때만 만들고, 이미 있으면 기능 연결만 맞춘다.
    /// 여러 번 실행해도 씬에 다시 놓지 않는다.
    /// </summary>
    public static class OpeningHoursBuilder
    {
        public const string MenuPath = "Tools/A305/World/Connect Opening Hours";
        public const string PlaceHoursPath = "Assets/Data/World/PlaceHours.json";
        public const string NoticePrefabPath = "Assets/Prefabs/World/HudNotice.prefab";

        private const string ThemeCatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";

        // 미션 창(108)·행인 제안(110) 위, 대화창(120) 아래에 그린다.
        private const int NoticeSortingOrder = 115;

        private static readonly Color TextColor = new Color(.27f, .24f, .20f);

        /// <summary>배포용 장소 씬 5개에 영업시간 처리만 다시 맞춘다. 디자인한 씬의 다른 객체는 건드리지 않는다.</summary>
        [MenuItem(MenuPath)]
        public static void ConnectToProductionScenes()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            foreach (string name in SpaceSceneSetup.ProductionSpaceScenes)
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                if (player == null)
                {
                    throw new InvalidOperationException(
                        $"{path}에 플레이어가 없습니다. {SpaceSceneSetup.MenuPath}를 먼저 실행합니다."
                    );
                }

                ConnectToScene(BuilderTools.EnsureGroups(scene), player);
                // 영업시간 연결만 바꾸도록 사물 단어 등록 없이 저장한다. 단어 연결은 ObjectHover → Register All Scene Words가 맡는다.
                if (!EditorSceneManager.SaveScene(scene, path))
                {
                    throw new InvalidOperationException($"씬을 저장하지 못했습니다: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("배포용 장소 씬 5개에 영업시간 처리와 안내판을 연결했습니다.");
        }

        /// <summary>안내판 프리팹을 없을 때만 만들고, 이미 있으면 기능 연결만 맞춘다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.UpdateOrCreatePrefab(NoticePrefabPath, "HudNotice", SetUpNotice);
        }

        /// <summary>안내판을 UI 그룹에, 영업시간 처리를 Systems 그룹에 하나씩 두고 플레이어·마을 출입구와 연결한다.</summary>
        public static void ConnectToScene(SceneGroups groups, PlayerMover player)
        {
            var notice = BuilderTools.FindSingleInScene<HudNoticeView>(groups.Scene);
            if (notice == null)
            {
                notice = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(NoticePrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<HudNoticeView>();
            }

            var controller = BuilderTools.FindSingleInScene<OpeningHoursController>(groups.Scene);
            if (controller == null)
            {
                controller = BuilderTools
                    .CreateEmpty("OpeningHours", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<OpeningHoursController>();
            }

            BuilderTools.SetReference(
                controller,
                "placeHours",
                BuilderTools.LoadRequiredAsset<TextAsset>(
                    PlaceHoursPath,
                    "ai/tools/npc_life_export.py"
                )
            );
            BuilderTools.SetReference(controller, "notice", notice);
            BuilderTools.SetReference(controller, "player", player);
            BuilderTools.SetReference(controller, "exitToTown", FindExitToTown(groups.Scene));
            BuilderTools.RequireReferences(controller, "exitToTown");
        }

        // 마을로 나가는 출입구. 마을 씬에는 없다.
        private static SpaceDoorway FindExitToTown(Scene scene)
        {
            return BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Where(doorway => doorway.DestinationScene == PlaceScenes.SceneOf("TOWN"))
                .OrderBy(doorway => doorway.DoorwayId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static void SetUpNotice(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, NoticeSortingOrder, receivesClicks: false);
            var notice = BuilderTools.GetOrAddComponent<HudNoticeView>(root);
            Transform plate = root.transform.Find("Plate");
            if (plate == null)
            {
                plate = CreatePlate(root.transform);
            }

            TMP_Text label = plate.Find("Label").GetComponent<TMP_Text>();
            BuilderTools.SetReference(notice, "plate", plate.gameObject);
            BuilderTools.SetReference(notice, "label", label);
            plate.gameObject.SetActive(false);
        }

        // 택시 상태 판처럼 화면 위 가운데에 테마의 판을 깔고 굵은 글자를 가운데 맞춘다.
        private static Transform CreatePlate(Transform root)
        {
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)plate.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -40);
            rect.sizeDelta = new Vector2(760, 64);
            plate.GetComponent<Image>().raycastTarget = false;

            TMP_Text label = UiBuilderTools.FindOrCreateText(
                plate.transform,
                "Label",
                UiBuilderTools.BoldFont(),
                24,
                TextColor
            );
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24, 10);
            labelRect.offsetMax = new Vector2(-24, -10);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            // 긴 매장 이름과 영업시간도 판 안에 한 줄로 들어가게 줄인다.
            label.enableAutoSizing = true;
            label.fontSizeMin = 18;
            label.fontSizeMax = 24;

            var theme = plate.AddComponent<ThemedElement>();
            BuilderTools.SetReference(
                theme,
                "catalog",
                BuilderTools.LoadRequiredAsset<UiThemeCatalog>(
                    ThemeCatalogPath,
                    "Tools/A305/UI/Build Themes"
                )
            );
            var themeFields = new SerializedObject(theme);
            themeFields.FindProperty("role").enumValueIndex = (int)UiThemeRole.Panel;
            themeFields.ApplyModifiedPropertiesWithoutUndo();
            theme.ApplyCurrent();
            return plate.transform;
        }
    }
}
