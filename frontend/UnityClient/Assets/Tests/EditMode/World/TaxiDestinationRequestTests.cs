using NUnit.Framework;

namespace A305.World.Tests
{
    public class TaxiDestinationRequestTests
    {
        [TestCase("Where is the hotel?")]
        [TestCase("How much does it cost to go to the hotel?")]
        [TestCase("Could you tell me how to get to the cafe?")]
        [TestCase("I went to the hotel yesterday.")]
        [TestCase("Is the hotel near the cafe?")]
        [TestCase("I don't want to go to the hotel.")]
        [TestCase("Can you take me to the airport?")]
        [TestCase("The hotel or the cafe, I'm not sure.")]
        [TestCase("")]
        [TestCase(null)]
        public void WithoutAnUnambiguousTravelRequest_DoesNotSelectADestination(string text)
        {
            Assert.That(TaxiDestinationRequest.TryResolve(text, out string code), Is.False);
            Assert.That(code, Is.Null);
        }

        [TestCase("Cafe, please.", "CAFE")]
        [TestCase("The coffee shop, please.", "CAFE")]
        [TestCase("Take me to the caffee, please.", "CAFE")]
        [TestCase("I'd like to go home.", "HOME")]
        [TestCase("Can you drive me to the drugstore?", "SHOP")]
        [TestCase("Please take me to the convenience store.", "SHOP")]
        [TestCase("호텔로 가 주세요", "HOTEL")]
        [TestCase("카페로 데려다 주세요", "CAFE")]
        public void SupportedDestinationRequest_ResolvesTheBuilding(string text, string expected)
        {
            Assert.That(TaxiDestinationRequest.TryResolve(text, out string code), Is.True);
            Assert.That(code, Is.EqualTo(expected));
        }

        [TestCase("Please take me to the post office.", "POST_OFFICE")]
        [TestCase("Maple Diner, please.", "DINER")]
        [TestCase("I'd like to go to the bank.", "BANK")]
        [TestCase("Take me to the laundromat.", "LAUNDROMAT")]
        [TestCase("The flower shop, please.", "FLOWER_SHOP")]
        [TestCase("Can you take me to the barber shop?", "BARBER")]
        [TestCase("Take me to the police station, please.", "POLICE_STATION")]
        [TestCase("경찰서로 가 주세요", "POLICE_STATION")]
        public void NewStorefrontRequest_ResolvesTheShop(string text, string expected)
        {
            Assert.That(TaxiDestinationRequest.TryResolve(text, out string code), Is.True);
            Assert.That(code, Is.EqualTo(expected));
        }

        [Test]
        public void DestinationRequest_ResolvesTheHotel()
        {
            bool resolved = TaxiDestinationRequest.TryResolve(
                "Could you take me to the hotel, please?",
                out string sceneCode
            );

            Assert.That(resolved, Is.True);
            Assert.That(sceneCode, Is.EqualTo("HOTEL"));
        }
    }
}
