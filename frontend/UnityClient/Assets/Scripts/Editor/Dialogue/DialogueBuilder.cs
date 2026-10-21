using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 임시 대화 화면 프리팹을 준비하고, 공간 씬에 대화 처리를 붙인다.
    /// 임시 화면은 [대화 종료]만 있으며, 대화 내용 UI는 대화 연동 작업에서 이 프리팹을 바꾼다.
    /// </summary>
    public static class DialogueBuilder
    {
        public const string MenuPath = "Tools/A305/Dialogue/Build";

        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PrepareAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"대화 화면을 준비했습니다: {PanelPrefabPath}");
        }

        /// <summary>임시 대화 화면 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.UpdateOrCreatePrefab(PanelPrefabPath, "DialoguePanel", SetUpPanel);
        }

        /// <summary>
        /// 씬에 UI 입력, 임시 대화 화면, 대화 처리가 없으면 붙이고 플레이어·카메라와 연결한다.
        /// </summary>
        public static DialogueController ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            PlayerCameraRig cameraRig
        )
        {
            BuilderTools.EnsureEventSystem(groups);

            var panel = BuilderTools.FindSingleInScene<DialoguePanelView>(groups.Scene);
            if (panel == null)
            {
                panel = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(PanelPrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<DialoguePanelView>();
            }

            var dialogue = BuilderTools.FindSingleInScene<DialogueController>(groups.Scene);
            if (dialogue == null)
            {
                dialogue = BuilderTools
                    .CreateEmpty("Dialogue", groups.Systems, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<DialogueController>();
            }

            BuilderTools.SetReference(dialogue, "player", player);
            BuilderTools.SetReference(dialogue, "cameraRig", cameraRig);
            BuilderTools.SetReference(dialogue, "panel", panel);
            BuilderTools.RequireReferences(dialogue);
            return dialogue;
        }

        private static void SetUpPanel(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, sortingOrder: 120, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<DialoguePanelView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateBottomPanel(root.transform, "Panel", 900f, 60f);
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 30f, Color.white, "대화 중");
            UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                regular,
                22f,
                new Color(0.8f, 0.82f, 0.86f),
                "대화 화면은 준비 중입니다. [대화 종료]를 누르면 대화를 끝내고 다시 움직일 수 있습니다."
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button endButton = UiBuilderTools.FindOrCreateButton(buttons, "EndButton", "대화 종료", bold);

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "endButton", endButton);
            BuilderTools.RequireReferences(view);

            // 대화 중에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }
    }
}
