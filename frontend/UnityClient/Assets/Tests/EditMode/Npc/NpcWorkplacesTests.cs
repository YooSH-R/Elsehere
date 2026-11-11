using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcWorkplacesTests
    {
        // 매장 직원은 그 매장 실내 장면에서 일한다. 교대로 맡은 다른 직업의 NPC도 생활 데이터의 일터를 따른다.
        [TestCase("POST_OFFICE_STAFF", "PostOfficeInterior")]
        [TestCase("TOWN_RETIREE", "PostOfficeInterior")]
        [TestCase("HOTEL_CONCIERGE", "BloomFlowersInterior")]
        [TestCase("CAFE_STAFF", "CafeScene")]
        [TestCase("TOWN_GRANDMOTHER", "TownScene")]
        public void SceneOf_Npc_IsTheSceneOfItsWorkplace(string npcCode, string scene)
        {
            Assert.That(NpcWorkplaces.SceneOf(npcCode), Is.EqualTo(scene));
        }

        [TestCase("NOT_AN_NPC")]
        [TestCase(null)]
        public void SceneOf_UnknownNpc_IsNull(string npcCode)
        {
            Assert.That(NpcWorkplaces.SceneOf(npcCode), Is.Null);
        }
    }
}
