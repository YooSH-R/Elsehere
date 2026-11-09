using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 대화창 도구 줄에 NPC 대사 자막을 켜고 끄는 [자막] 버튼을 더한다.
    /// 대화창 전체 Builder를 다시 돌리면 승인된 디자인 값까지 다시 써서, 이 버튼만 더하고 도구 줄 배치만 다시 맞춘다.
    /// </summary>
    public static class DialogueSubtitleBuilder
    {
        private const string MenuPath = "Tools/A305/Dialogue/Apply Subtitle Button";
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.UpdatePrefabIfExists(
                PanelPrefabPath,
                root => SetUp(root, root.GetComponent<DialoguePanelView>())
            );
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root, DialoguePanelView view)
        {
            Transform tools = root.transform.Find("Panel/Conversation/ToolRow");
            Button subtitle = UiBuilderTools.FindOrCreateButton(
                tools,
                "SubtitleButton",
                "자막 켜기",
                UiBuilderTools.BoldFont(),
                108f
            );
            DialogueDockBuilder.ToolRow(tools);
            BuilderTools.SetReference(view, "subtitleButton", subtitle);
        }
    }
}
