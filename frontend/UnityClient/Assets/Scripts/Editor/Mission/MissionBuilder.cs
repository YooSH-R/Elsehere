using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Mission;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 미션 창 프리팹과 확인용 미션을 준비하고 공간 씬에 미션 처리를 붙인다.
    /// 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class MissionBuilder
    {
        public const string MenuPath = "Tools/A305/Mission/Build";

        private const string PanelPrefabPath = "Assets/Prefabs/Mission/MissionPanel.prefab";
        private const string MockMissionsPath = "Assets/Data/Mission/MockMissions.asset";

        // 화면 왼쪽 위에 늘 보이는 창이라 사물 툴팁(100)보다 위, 대화 제안(110)보다 아래에 둔다.
        private const int PanelSortingOrder = 105;

        private static readonly Color SubTextColor = new Color(0.8f, 0.82f, 0.86f);
        private static readonly Color StatusColor = new Color(0.62f, 0.8f, 1f);

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"미션 창을 준비했습니다: {PanelPrefabPath}");
        }

        /// <summary>확인용 미션(없을 때만)과 미션 창 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.LoadOrCreateAsset<MockMissionSource>(MockMissionsPath);
            BuilderTools.UpdateOrCreatePrefab(PanelPrefabPath, "MissionPanel", SetUpPanel);
        }

        /// <summary>씬에 미션 창과 미션 처리가 없으면 붙이고 대화 처리와 연결한다.</summary>
        public static MissionController ConnectToScene(
            SceneGroups groups,
            DialogueController dialogue
        )
        {
            var panel = BuilderTools.FindSingleInScene<MissionPanelView>(groups.Scene);
            if (panel == null)
            {
                panel = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(PanelPrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<MissionPanelView>();
            }

            var mission = BuilderTools.FindSingleInScene<MissionController>(groups.Scene);
            if (mission == null)
            {
                mission = BuilderTools
                    .CreateEmpty("Mission", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<MissionController>();
            }

            BuilderTools.SetReference(mission, "dialogue", dialogue);
            BuilderTools.SetReference(
                mission,
                "missionSource",
                BuilderTools.LoadRequiredAsset<MockMissionSource>(MockMissionsPath, MenuPath)
            );
            BuilderTools.SetReference(mission, "panelView", panel);
            BuilderTools.RequireReferences(mission);
            return mission;
        }

        private static void SetUpPanel(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, PanelSortingOrder, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<MissionPanelView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "Panel",
                new Vector2(0f, 1f),
                420f,
                new Vector2(24f, -24f)
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            TMP_Text header = UiBuilderTools.FindOrCreateText(
                panel,
                "Header",
                bold,
                26f,
                Color.white,
                "오늘의 작은 도전"
            );
            TMP_Text message = UiBuilderTools.FindOrCreateText(
                panel,
                "Message",
                regular,
                18f,
                SubTextColor
            );
            RectTransform rows = UiBuilderTools.FindOrCreateColumn(panel, "Rows");
            MissionRowView rowTemplate = SetUpRowTemplate(rows, bold, regular);

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "headerText", header);
            BuilderTools.SetReference(view, "messageText", message);
            BuilderTools.SetReference(view, "rowsParent", rows);
            BuilderTools.SetReference(view, "rowTemplate", rowTemplate);
            BuilderTools.RequireReferences(view);
        }

        // 미션 수는 서버 데이터에 따라 달라지므로 줄 하나를 템플릿으로 두고 실행 중에 복제한다.
        private static MissionRowView SetUpRowTemplate(
            Transform rows,
            TMP_FontAsset bold,
            TMP_FontAsset regular
        )
        {
            RectTransform template = UiBuilderTools.FindOrCreateColumn(rows, "RowTemplate");
            var row = BuilderTools.GetOrAddComponent<MissionRowView>(template.gameObject);
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                template,
                "Title",
                regular,
                22f,
                Color.white
            );
            TMP_Text description = UiBuilderTools.FindOrCreateText(
                template,
                "Description",
                regular,
                18f,
                SubTextColor
            );
            TMP_Text status = UiBuilderTools.FindOrCreateText(
                template,
                "Status",
                regular,
                18f,
                StatusColor
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(template, "Buttons");
            Button startButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "StartButton",
                "미션 시작",
                bold,
                180f
            );

            BuilderTools.SetReference(row, "titleText", title);
            BuilderTools.SetReference(row, "descriptionText", description);
            BuilderTools.SetReference(row, "statusText", status);
            BuilderTools.SetReference(row, "startButton", startButton);
            BuilderTools.RequireReferences(row);

            template.gameObject.SetActive(false);
            return row;
        }
    }
}
