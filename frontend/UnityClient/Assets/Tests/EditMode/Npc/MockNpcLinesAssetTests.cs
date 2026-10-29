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
    }
}
