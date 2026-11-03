using NUnit.Framework;

namespace A305.Wayfinding.Tests
{
    public class GuideDestinationTests
    {
        [Test]
        public void Show_GuidesToThePlaceTheMissionAskedFor()
        {
            var destination = new GuideDestination();

            destination.Show("HOTEL");

            Assert.That(destination.PlaceCode, Is.EqualTo("HOTEL"));
        }

        [Test]
        public void Show_AnotherPlace_ReplacesTheDestination()
        {
            var destination = new GuideDestination();
            destination.Show("HOTEL");

            destination.Show("CAFE");

            Assert.That(destination.PlaceCode, Is.EqualTo("CAFE"));
        }

        [Test]
        public void Clear_StopsGuiding()
        {
            var destination = new GuideDestination();
            destination.Show("HOTEL");

            destination.Clear();

            Assert.That(destination.PlaceCode, Is.Null);
        }
    }
}
