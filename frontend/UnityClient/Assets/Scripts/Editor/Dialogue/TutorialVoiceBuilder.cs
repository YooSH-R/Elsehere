using System;
using A305.Dialogue;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Dialogue
{
    /// <summary>
    /// 튜토리얼 Ben 길 안내 대본의 첫 말과 대답에 Ben 목소리로 미리 합성한 음성을 잇는다. 길 안내는 서버 대화가 아니라 서버 음성을 받지 않는다.
    /// 음성은 ai/tools/synthesize_script_lines.py로 <see cref="VoiceFolder"/>에 Opening·Reply1… 이름으로 만든다.
    /// </summary>
    public static class TutorialVoiceBuilder
    {
        public const string MenuPath = "Tools/A305/Dialogue/Apply Tutorial Ben Voice";

        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";
        private const string VoiceFolder = "Assets/Audio/Dialogue/TutorialBen";
        private const string TutorialNpcCode = "TUTORIAL_PASSERBY";

        [MenuItem("Tools/A305/Dialogue/Apply Tutorial Ben Greeting")]
        public static void ApplyGreeting()
        {
            var scripts = BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(
                ScriptsPath,
                DialogueBuilder.MenuPath
            );
            var defaults = ScriptableObject.CreateInstance<MockDialogueScriptSource>();
            var target = new SerializedObject(scripts);
            var source = new SerializedObject(defaults);
            SerializedProperty entry = FindEntry(target.FindProperty("entries"));
            SerializedProperty initial = FindEntry(source.FindProperty("entries"));
            if (entry.propertyPath != initial.propertyPath)
                throw new InvalidOperationException("튜토리얼 대본의 배열 위치가 변경됐습니다.");
            target.CopyFromSerializedProperty(initial);
            target.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(defaults);
            Apply();
            A305.EditorTools.Tutorial.TutorialBuilder.AlignServiceGuideTexts();
        }

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            var scripts = BuilderTools.LoadRequiredAsset<MockDialogueScriptSource>(
                ScriptsPath,
                DialogueBuilder.MenuPath
            );
            var serialized = new SerializedObject(scripts);
            SerializedProperty entry = FindEntry(serialized.FindProperty("entries"));
            SetVoice(entry.FindPropertyRelative("opening"), "Opening");
            SerializedProperty turns = entry.FindPropertyRelative("guidedTurns");
            for (int i = 0; i < turns.arraySize; i++)
            {
                SetVoice(
                    turns.GetArrayElementAtIndex(i).FindPropertyRelative("reply"),
                    $"Reply{i + 1}"
                );
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scripts);
            AssetDatabase.SaveAssets();
        }

        private static SerializedProperty FindEntry(SerializedProperty entries)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("npcCode").stringValue == TutorialNpcCode)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException(
                $"{ScriptsPath}에 {TutorialNpcCode} 대본이 없습니다."
            );
        }

        private static void SetVoice(SerializedProperty line, string name)
        {
            string path = $"{VoiceFolder}/{name}.bytes";
            var voice = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (voice == null)
            {
                throw new InvalidOperationException(
                    $"{path}가 없습니다. ai/tools/synthesize_script_lines.py로 먼저 만듭니다."
                );
            }

            line.FindPropertyRelative("voice").objectReferenceValue = voice;
        }
    }
}
