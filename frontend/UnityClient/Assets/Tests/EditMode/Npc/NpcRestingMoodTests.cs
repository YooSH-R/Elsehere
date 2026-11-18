using A305.Characters;
using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class NpcRestingMoodTests
    {
        [TestCase("HOTEL_STAFF", false, true)]
        [TestCase("HOTEL_RECEPTIONIST", false, false)]
        [TestCase("HOTEL_STAFF", true, false)]
        [TestCase("HOTEL_RECEPTIONIST", true, true)]
        [TestCase("HOTEL_BELLHOP", true, false)]
        [TestCase("DINER_STAFF", true, false)]
        public void JakeIsIndifferentExceptInTheShowcaseWhereOwenIs(
            string code,
            bool showcase,
            bool indifferent
        )
        {
            Assert.That(
                NpcRestingMood.Of(code, showcase),
                Is.EqualTo(indifferent ? CharacterEmotion.Indifferent : (CharacterEmotion?)null)
            );
        }
    }
}
