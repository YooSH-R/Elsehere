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
    /// 대화 화면 프리팹과 확인용 대화 대본을 준비하고, 공간 씬에 대화 처리를 붙인다.
    /// 화면은 없는 요소만 만들고, 이미 있는 요소의 디자인은 유지한다.
    /// </summary>
    public static class DialogueBuilder
    {
        public const string MenuPath = "Tools/A305/Dialogue/Build";

        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string MockScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";

        // 대화 내용 화면이 생기기 전의 임시 안내 문구. 이 문구가 그대로면 숨기고, 디자인에서 바꾼 문구는 둔다.
        private const string PlaceholderNotice =
            "대화 화면은 준비 중입니다. [대화 종료]를 누르면 대화를 끝내고 다시 움직일 수 있습니다.";

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
            Debug.Log($"대화 화면을 준비했습니다: {PanelPrefabPath}");
        }

        /// <summary>확인용 대본(없을 때만)과 대화 화면 프리팹의 기능 구성을 맞춘다. 디자인은 유지한다.</summary>
        public static void PrepareAssets()
        {
            BuilderTools.LoadOrCreateAsset<MockDialogueScriptSource>(MockScriptsPath);
            BuilderTools.UpdateOrCreatePrefab(PanelPrefabPath, "DialoguePanel", SetUpPanel);
        }

        /// <summary>
        /// 씬에 UI 입력, 대화 화면, 대화 처리가 없으면 붙이고 플레이어·카메라·대본과 연결한다.
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
            BuilderTools.SetReference(
                dialogue,
                "scriptSource",
                BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(MockScriptsPath, MenuPath)
            );
            BuilderTools.RequireReferences(dialogue);
            return dialogue;
        }

        private static void SetUpPanel(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, sortingOrder: 120, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<DialoguePanelView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateBottomPanel(
                root.transform,
                "Panel",
                900f,
                60f
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            UiBuilderTools.FindOrCreateText(panel, "Title", bold, 30f, Color.white, "대화 중");
            TMP_Text notice = UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                regular,
                22f,
                SubTextColor,
                PlaceholderNotice
            );
            if (notice.text == PlaceholderNotice)
            {
                notice.gameObject.SetActive(false);
            }

            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button endButton = UiBuilderTools.FindOrCreateButton(
                buttons,
                "EndButton",
                "대화 종료",
                bold
            );

            // 대화 내용은 [대화 종료] 줄 위에 둔다. 처음 만들 때만 자리를 정하고, 이후에는 디자인한 순서를 유지한다.
            bool isConversationNew = panel.Find("Conversation") == null;
            RectTransform conversation = UiBuilderTools.FindOrCreateColumn(panel, "Conversation");
            if (isConversationNew)
            {
                conversation.SetSiblingIndex(buttons.GetSiblingIndex());
            }

            TMP_Text lines = UiBuilderTools.FindOrCreateText(
                conversation,
                "Lines",
                regular,
                24f,
                Color.white
            );
            TMP_Text guide = UiBuilderTools.FindOrCreateText(
                conversation,
                "Guide",
                regular,
                20f,
                SubTextColor
            );
            RectTransform inputRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "InputRow");
            TMP_InputField input = UiBuilderTools.FindOrCreateInputField(
                inputRow,
                "Input",
                regular,
                "영어로 입력해 보세요 (Enter로 보내기)"
            );
            Button sendButton = UiBuilderTools.FindOrCreateButton(
                inputRow,
                "SendButton",
                "보내기",
                bold,
                160f
            );
            Button exampleButton = UiBuilderTools.FindOrCreateButton(
                inputRow,
                "ExampleButton",
                "예시 문장",
                bold,
                160f
            );
            RectTransform toolRow = UiBuilderTools.FindOrCreateButtonRow(conversation, "ToolRow");
            Button meaningButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "MeaningButton",
                "뜻 보기",
                bold,
                160f
            );
            Button saveButton = UiBuilderTools.FindOrCreateButton(
                toolRow,
                "SaveButton",
                "표현 저장",
                bold,
                160f
            );

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "endButton", endButton);
            BuilderTools.SetReference(view, "linesText", lines);
            BuilderTools.SetReference(view, "guideText", guide);
            BuilderTools.SetReference(view, "inputField", input);
            BuilderTools.SetReference(view, "sendButton", sendButton);
            BuilderTools.SetReference(view, "exampleButton", exampleButton);
            BuilderTools.SetReference(view, "meaningButton", meaningButton);
            BuilderTools.SetReference(view, "saveButton", saveButton);
            BuilderTools.RequireReferences(view);

            // 대화 중에만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }
    }
}
