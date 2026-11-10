using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// NPC 머리 위 이름표. 가까운 NPC만 이름을 보이고, 근무 중인 직원은 역할(예: 벨보이)을 칩으로 함께 보여 행인과 구분한다.
    /// </summary>
    public class NpcNameTagsTests
    {
        [Test]
        public void StaffOffDuty_ShowsNameOnly()
        {
            // 쉬는 시간·퇴근한 직원은 역할 칩을 떼고 이름만 보인다.
            NpcNameTag onDuty = NpcNameTags.Describe("Dan", "경찰관", "STAFF").Value;

            NpcNameTag shown = NpcNameTags.ForDuty(onDuty, isOffDuty: true);

            Assert.That(shown.Name, Is.EqualTo("Dan"));
            Assert.That(shown.StaffRole, Is.Null);
        }

        [Test]
        public void StaffOnDuty_KeepsRoleChip()
        {
            NpcNameTag onDuty = NpcNameTags.Describe("Dan", "경찰관", "STAFF").Value;

            NpcNameTag shown = NpcNameTags.ForDuty(onDuty, isOffDuty: false);

            Assert.That(shown.Name, Is.EqualTo("Dan"));
            Assert.That(shown.StaffRole, Is.EqualTo("경찰관"));
        }

        [Test]
        public void Staff_ShowsNameWithRoleChip()
        {
            NpcNameTag? tag = NpcNameTags.Describe("Diego", "벨보이", "STAFF");

            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.Value.Name, Is.EqualTo("Diego"));
            Assert.That(tag.Value.StaffRole, Is.EqualTo("벨보이"));
        }

        [Test]
        public void Passerby_ShowsNameOnly()
        {
            NpcNameTag? tag = NpcNameTags.Describe("Ruby", "행인", "PASSERBY");

            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.Value.Name, Is.EqualTo("Ruby"));
            Assert.That(tag.Value.StaffRole, Is.Null);
        }

        [Test]
        public void NpcWithoutLifeData_ShowsNameOnly()
        {
            NpcNameTag? tag = NpcNameTags.Describe("Ben", "행인", null);

            Assert.That(tag.Value.StaffRole, Is.Null);
        }

        [Test]
        public void StaffWithoutRoleLabel_ShowsNameOnly()
        {
            NpcNameTag? tag = NpcNameTags.Describe("Mia", "", "STAFF");

            Assert.That(tag.Value.StaffRole, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NpcWithUnknownName_HasNoTag(string name)
        {
            Assert.That(NpcNameTags.Describe(name, "벨보이", "STAFF"), Is.Null);
        }

        [TestCase(3f, ExpectedResult = true)]
        [TestCase(NpcNameTags.ShowRadiusMeters, ExpectedResult = true)]
        [TestCase(NpcNameTags.ShowRadiusMeters + .1f, ExpectedResult = false)]
        public bool Tag_ShowsOnlyForNpcsNearThePlayer(float metersFromPlayer)
        {
            return NpcNameTags.IsShown(metersFromPlayer, isPlayerBusy: false);
        }

        // 대화나 단어장·지도 같은 창으로 플레이어가 움직이지 못하는 동안에는 이름표가 화면을 가리지 않게 숨긴다.
        [Test]
        public void BusyPlayer_SeesNoTags()
        {
            Assert.That(NpcNameTags.IsShown(1f, isPlayerBusy: true), Is.False);
        }

        // [이웃]에서 찾는 NPC는 멀리서도 머리 위 표시로 알아보게 이름표를 띄운다.
        [Test]
        public void TrackedNpc_ShowsFromFarAway()
        {
            Assert.That(NpcNameTags.IsShown(60f, isPlayerBusy: false, isTracked: true), Is.True);
        }

        [Test]
        public void TrackedNpc_StillHidesWhileThePlayerIsBusy()
        {
            Assert.That(NpcNameTags.IsShown(60f, isPlayerBusy: true, isTracked: true), Is.False);
        }
    }
}
