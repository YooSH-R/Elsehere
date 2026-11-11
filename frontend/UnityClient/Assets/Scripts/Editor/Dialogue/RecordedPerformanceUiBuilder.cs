using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    public static class RecordedPerformanceUiBuilder
    {
        private const string Path = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        public static void BuildAndCapture()
        {
            Build();
            JonePerformanceUiPreview.Capture();
        }

        [MenuItem("Tools/A305/Dialogue/Connect Recorded Performance Button")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            JohnParkRapBuilder.Build();
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                var view = root.GetComponent<DialoguePanelView>();
                var serialized = new SerializedObject(view);
                var property = serialized.FindProperty("performanceButton");
                var button = property.objectReferenceValue as Button;
                if (button == null)
                {
                    var replay = (Button)
                        serialized.FindProperty("replayButton").objectReferenceValue;
                    var name = (TMP_Text)
                        serialized.FindProperty("npcNameText").objectReferenceValue;
                    button = Object.Instantiate(replay, name.transform.parent);
                    button.name = "PerformanceButton";
                    Object.DestroyImmediate(button.GetComponent<LayoutElement>());
                    var theme = new SerializedObject(button.GetComponent<ThemedElement>());
                    theme.FindProperty("role").enumValueIndex = (int)UiThemeRole.PillButton;
                    theme.FindProperty("tone").enumValueIndex = (int)UiTone.Secondary;
                    theme.ApplyModifiedPropertiesWithoutUndo();
                    button.GetComponent<ThemedElement>().ApplyCurrent();
                    var outline = button.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, .83f, .38f, 1);
                    outline.effectDistance = new Vector2(3, -3);
                    outline.enabled = false;
                    property.objectReferenceValue = button;
                }
                button.GetComponentInChildren<TMP_Text>(true).text = "랩 다시 듣기";
                button.gameObject.SetActive(false);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("John 저장 안내·번역·음성·표정·입모양·랩 버튼 연결 완료");
        }
    }
}
