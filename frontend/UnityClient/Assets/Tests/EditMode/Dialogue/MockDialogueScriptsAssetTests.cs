using NUnit.Framework;
using UnityEditor;

namespace A305.Dialogue.Tests
{
    /// <summary>씬에 둔 NPC가 기본 대본("Neighbor")으로 빠지지 않고 자기 이름과 역할로 소개되는지.</summary>
    public class MockDialogueScriptsAssetTests
    {
        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";

        // 이름은 서버 시드(world.sql npcs)와 같아야 서버 대화가 시작돼도 이름이 바뀌지 않는다.
        [TestCase("SHOP_STAFF", "Alex", "편의점 직원")]
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
        public void TaxiDriver_AsksWhereToGo()
        {
            // 마을 본길에서 택시를 세우면 기사에게 가고 싶은 곳을 영어로 말해 본다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            bool found = scripts.TryGetScript("TAXI_DRIVER", out DialogueScript script);

            Assert.That(found, Is.True);
            Assert.That(script.RoleLabel, Is.EqualTo("택시 기사"));
            Assert.That(script.OpeningLine.English, Does.Contain("Where"));
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
