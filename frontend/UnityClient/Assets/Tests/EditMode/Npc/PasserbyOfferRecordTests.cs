using System;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class PasserbyOfferRecordTests
    {
        private static readonly DateTime November2 = new DateTime(2026, 11, 2);

        [SetUp]
        [TearDown]
        public void ClearRecord()
        {
            PasserbyOfferRecord.Clear();
        }

        [Test]
        public void HasOfferedOn_NeverOffered_IsFalse()
        {
            Assert.That(PasserbyOfferRecord.HasOfferedOn("TOWN_PASSERBY", November2), Is.False);
        }

        [Test]
        public void HasOfferedOn_OfferedThatDate_IsTrueOnlyForThatNpcAndDate()
        {
            PasserbyOfferRecord.Save("TOWN_PASSERBY", November2.AddHours(9));

            Assert.That(
                PasserbyOfferRecord.HasOfferedOn("TOWN_PASSERBY", November2.AddHours(23)),
                Is.True,
                "같은 날 밤"
            );
            Assert.That(
                PasserbyOfferRecord.HasOfferedOn("TOWN_PASSERBY", November2.AddDays(1)),
                Is.False,
                "다음 날"
            );
            Assert.That(
                PasserbyOfferRecord.HasOfferedOn("CAFE_STAFF", November2),
                Is.False,
                "다른 NPC"
            );
        }

        [Test]
        public void Reload_KeepsSavedDate_LikeTurningTheGameOffAndOn()
        {
            PasserbyOfferRecord.Save("TOWN_PASSERBY", November2);

            PasserbyOfferRecord.Reload();

            Assert.That(PasserbyOfferRecord.HasOfferedOn("TOWN_PASSERBY", November2), Is.True);
        }

        [Test]
        public void Clear_ForgetsSavedDate_AfterReload()
        {
            PasserbyOfferRecord.Save("TOWN_PASSERBY", November2);

            PasserbyOfferRecord.Clear();
            PasserbyOfferRecord.Reload();

            Assert.That(PasserbyOfferRecord.HasOfferedOn("TOWN_PASSERBY", November2), Is.False);
        }
    }
}
