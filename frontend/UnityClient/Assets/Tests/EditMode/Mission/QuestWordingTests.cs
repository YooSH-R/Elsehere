using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>퀘스트가 여러 개가 되므로 화면 문구에 '첫날'·'여정'을 쓰지 않는다(미션 화면·진입 화면·튜토리얼 안내).</summary>
    public class QuestWordingTests
    {
        private static readonly string[] Words = { "첫날", "여정" };

        [TestCase("Assets/Prefabs/Mission/FirstDayMission.prefab")]
        [TestCase("Assets/Prefabs/Onboarding/EntryScreens.prefab")]
        public void PrefabTexts_DoNotTalkAboutTheFirstDayJourney(string path)
        {
            string[] texts = AssetDatabase
                .LoadAssetAtPath<GameObject>(path)
                .GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.text)
                .Where(Mentions)
                .ToArray();

            Assert.That(texts, Is.Empty);
        }

        [Test]
        public void TutorialGuideTexts_DoNotTalkAboutTheFirstDayJourney()
        {
            var guide = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Data/Tutorial/TutorialGuide.asset"
            );
            SerializedProperty property = new SerializedObject(guide).GetIterator();
            var found = new System.Collections.Generic.List<string>();
            while (property.Next(true))
            {
                if (
                    property.propertyType == SerializedPropertyType.String
                    && Mentions(property.stringValue)
                )
                    found.Add(property.stringValue);
            }

            Assert.That(found, Is.Empty);
        }

        private static bool Mentions(string text) =>
            !string.IsNullOrEmpty(text) && Words.Any(word => text.Contains(word));
    }
}
