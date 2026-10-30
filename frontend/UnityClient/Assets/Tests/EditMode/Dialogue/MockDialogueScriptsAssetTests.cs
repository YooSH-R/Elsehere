using NUnit.Framework;
using UnityEditor;

namespace A305.Dialogue.Tests
{
    /// <summary>씬에 둔 NPC가 기본 대본("Neighbor")으로 빠지지 않고 자기 이름과 역할로 소개되는지.</summary>
    public class MockDialogueScriptsAssetTests
    {
        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";

        // 이름은 서버 시드(world.sql npcs)와 같아야 서버 대화가 시작돼도 이름이 바뀌지 않는다.
        [TestCase("SHOP_STAFF", "Alex", "드럭스토어 직원")]
        [TestCase("HOTEL_STAFF", "Emma", "호텔 직원")]
        public void StaffInTheJourney_AreIntroducedByNameAndRole(
            string npcCode,
            string name,
            string roleLabel
        )
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript(npcCode, out DialogueScript script);

            Assert.That(script.NpcName, Is.EqualTo(name));
            Assert.That(script.RoleLabel, Is.EqualTo(roleLabel));
        }

        [Test]
        public void ShopStaff_WelcomesVisitorsToTheDrugstore()
        {
            // 미국에서 비 올 때 우산을 사러 들르는 드럭스토어로 상점을 정했다. 간판·지도와 같은 이름으로 맞이한다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript("SHOP_STAFF", out DialogueScript script);

            Assert.That(script.OpeningLine.English, Does.Contain("drugstore"));
            Assert.That(script.OpeningLine.Korean, Does.Contain("드럭스토어"));
        }

        [TestCase("TUTORIAL_PASSERBY", "이웃")]
        [TestCase("TOWN_PASSERBY", "행인")]
        [TestCase("CAFE_STAFF", "카페 직원")]
        public void EarlierNpcs_GainTheirRole(string npcCode, string roleLabel)
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript(npcCode, out DialogueScript script);

            Assert.That(script.RoleLabel, Is.EqualTo(roleLabel));
        }
    }
}
