using A305.ObjectHover;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    [CustomEditor(typeof(ObjectWordDefinition))]
    public sealed class ObjectWordDefinitionEditor : Editor
    {
        private void OnEnable() => ObjectWordAuthoring.EnsureIdentity((ObjectWordDefinition)target);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "이 공용 데이터를 참조하는 모든 사물에 같은 내용이 표시됩니다.",
                MessageType.Info
            );
            Draw("languageCode", "언어 코드");
            Draw("text", "단어");
            Draw("meaningKo", "한국어 뜻");
            Draw("example", "영문 예문");
            Draw("exampleKo", "예문 해석");
            Draw("pronunciationAudioKey", "발음 파일 키 (선택)");
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("내부 공용 키", ((ObjectWordDefinition)target).Key);
            if (GUILayout.Button("백엔드 데이터 내보내기"))
                ObjectWordAuthoring.Export();
        }

        private void Draw(string property, string label) =>
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("details." + property),
                new GUIContent(label)
            );
    }
}
