using NUnit.Framework;
using UnityEditor;

namespace A305.Tutorial.Tests
{
    /// <summary>튜토리얼에서 길을 묻는 NPC가 마을 행인 Ben이고, 그동안 튜토리얼 대본으로 말하는지.</summary>
    public class TutorialGuideAssetTests
    {
        private const string GuidePath = "Assets/Data/Tutorial/TutorialGuide.asset";

        [Test]
        public void TutorialNpc_IsBenTheTownPasserbyUsingTheTutorialScript()
        {
            var guide = AssetDatabase.LoadAssetAtPath<TutorialGuideDefinition>(GuidePath);

            Assert.That(guide.TutorialNpcCode, Is.EqualTo("TOWN_PASSERBY"));
            Assert.That(guide.TutorialScriptKey, Is.EqualTo("TUTORIAL_PASSERBY"));
        }
    }
}
