using NUnit.Framework;

namespace A305.Wayfinding.Tests
{
    public class GuideDestinationTests
    {
        [Test]
        public void TownStoryNpcDestinationsMatchTheirShopAndArrival()
        {
            var destination = new GuideDestination();
            foreach (
                string place in new[]
                {
                    "DINER",
                    "LAUNDROMAT",
                    "BANK",
                    "BARBER",
                    "FLOWER_SHOP",
                    "BAKERY",
                    "HOME_STORE",
                    "ARCADE",
                    "POST_OFFICE",
                }
            )
            {
                destination.Show(place + "_STAFF");
                Assert.That(destination.PlaceCode, Is.EqualTo(place));
                Assert.That(destination.Arrived, Is.False);
                destination.EnterPlace(place);
                Assert.That(destination.Arrived, Is.True);
            }
        }

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
        public void Arrive_StaysArrivedUntilAnotherPlaceIsShown()
        {
            var destination = new GuideDestination();
            destination.Show("HOTEL");

            destination.Arrive();
            Assert.That(destination.Arrived, Is.True);
            // 미션이 상태를 다시 읽어 같은 목적지를 알려도 도착한 채로 둔다.
            destination.Show("HOTEL");
            Assert.That(destination.Arrived, Is.True);
            destination.Show("CAFE");
            Assert.That(destination.Arrived, Is.False);
        }

        [Test]
        public void EnteringTheDestinationPlace_CountsAsArriving()
        {
            var destination = new GuideDestination();
            destination.Show("HOTEL");

            destination.EnterPlace("HOTEL");

            Assert.That(destination.Arrived, Is.True);
        }

        [Test]
        public void EnteringAnotherPlace_KeepsGuiding()
        {
            var destination = new GuideDestination();
            destination.Show("HOTEL");

            destination.EnterPlace("CAFE");

            Assert.That(destination.Arrived, Is.False);
        }

        [Test]
        public void DestinationShownInsideThatPlace_IsAlreadyReached()
        {
            var destination = new GuideDestination();
            destination.EnterPlace("HOTEL");

            destination.Show("HOTEL");

            Assert.That(destination.Arrived, Is.True);
        }

        [Test]
        public void ArriveWithoutDestination_DoesNotHideTheNextOne()
        {
            var destination = new GuideDestination();
            destination.Arrive();

            destination.Show("HOTEL");

            Assert.That(destination.Arrived, Is.False);
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
