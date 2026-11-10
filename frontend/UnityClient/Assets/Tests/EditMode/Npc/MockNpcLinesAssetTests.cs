using NUnit.Framework;
using UnityEditor;

namespace A305.Npc.Tests
{
    /// <summary>먼저 말을 거는 직원마다 대화 제안 화면에 보일 인사말이 있어야 한다.</summary>
    public class MockNpcLinesAssetTests
    {
        private const string LinesPath = "Assets/Data/Npc/MockNpcLines.asset";

        [TestCase("CAFE_STAFF")]
        [TestCase("HOTEL_STAFF")]
        [TestCase("SHOP_STAFF")]
        public void StaffWhoGreetVisitors_HaveAGreeting(string npcCode)
        {
            var lines = AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(LinesPath);

            Assert.That(lines.TryGetGreeting(npcCode, out string greeting), Is.True);
            Assert.That(greeting, Is.Not.Empty);
        }

        [Test]
        public void ShopStaffGreeting_NamesTheDrugstore()
        {
            var lines = AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(LinesPath);

            lines.TryGetGreeting("SHOP_STAFF", out string greeting);

            Assert.That(greeting, Does.Contain("drugstore"));
        }

        [TestCase("POLICE_ROOKIE", "Officer Taylor")]
        [TestCase("POLICE_SERGEANT", "police station")]
        [TestCase("HOTEL_STAFF", "Maple House")]
        [TestCase("HOTEL_RECEPTIONIST", "Maple House")]
        [TestCase("HOTEL_NIGHT_MANAGER", "your stay")]
        [TestCase("POST_OFFICE_STAFF", "post office")]
        [TestCase("TOWN_RETIREE", "post office")]
        [TestCase("BANK_STAFF", "Town Bank")]
        [TestCase("TOWN_EVENING_WALKER", "Town Bank")]
        [TestCase("TOWN_SHOPPER", "Town Bank")]
        [TestCase("BAKERY_STAFF", "Maple Bakery")]
        [TestCase("POLICE_PATROL_OFFICER", "Maple Bakery")]
        [TestCase("SHOP_NIGHT_CLERK", "drugstore")]
        public void StaffGreeting_MatchesTheirRoleThroughoutTheShift(
            string npcCode,
            string roleOrWorkplace
        )
        {
            var lines = AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(LinesPath);

            Assert.That(lines.TryGetGreeting(npcCode, out string greeting), Is.True);
            Assert.That(greeting, Does.Contain(roleOrWorkplace));
            Assert.That(greeting, Does.Not.Match(@"(?i)^(good )?(morning|afternoon|evening)\b"));
        }
    }
}
