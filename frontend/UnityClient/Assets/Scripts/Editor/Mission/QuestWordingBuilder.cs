using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트가 여러 개가 되므로 이미 만든 미션 화면과 튜토리얼 안내에 남은 '첫날'·'여정' 문구를 퀘스트 문구로 바꾼다.
    /// 다른 글자와 디자인은 그대로 둔다. 새로 만드는 화면은 각 Builder의 기본 문구가 이미 퀘스트 문구다.
    /// </summary>
    public static class QuestWordingBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Quest Wording";
        private const string MissionPrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string TutorialGuidePath = "Assets/Data/Tutorial/TutorialGuide.asset";

        private static readonly (string from, string to)[] Wording =
        {
            ("첫날의 작은 소동", "퀘스트"),
            ("지도 버튼을 눌러 여정을 확인하세요.", QuestCardBuilder.DirectionPlaceholder),
            ("여정 시작", "퀘스트 시작"),
            ("여행 첫날", "여행 시작"),
        };

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                MissionPrefabPath,
                root =>
                {
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                        text.text = Reworded(text.text);
                }
            );
            var guide = AssetDatabase.LoadMainAssetAtPath(TutorialGuidePath);
            if (guide != null)
            {
                var fields = new SerializedObject(guide);
                SerializedProperty property = fields.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.String)
                        property.stringValue = Reworded(property.stringValue);
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(guide);
            }
            AssetDatabase.SaveAssets();
        }

        private static string Reworded(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            foreach (var (from, to) in Wording)
            {
                if (text == from)
                    return to;
            }
            return text;
        }
    }
}
