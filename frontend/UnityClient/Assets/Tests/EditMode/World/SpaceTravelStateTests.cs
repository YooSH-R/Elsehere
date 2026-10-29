using NUnit.Framework;

namespace A305.World.Tests
{
    public class SpaceTravelStateTests
    {
        private SpaceTravelState state;

        [SetUp]
        public void CreateState()
        {
            state = new SpaceTravelState();
        }

        [Test]
        public void NewState_IsNotTraveling()
        {
            Assert.That(state.IsTraveling, Is.False);
        }

        [Test]
        public void TryBegin_WhenIdle_StartsTravel()
        {
            bool isStarted = state.TryBegin("TownScene", "HomeDoor");

            Assert.That(isStarted, Is.True);
            Assert.That(state.IsTraveling, Is.True);
        }

        [Test]
        public void TryBegin_WithoutDestinationScene_IsRejected()
        {
            Assert.That(state.TryBegin("", "HomeDoor"), Is.False);
            Assert.That(state.TryBegin(null, "HomeDoor"), Is.False);
            Assert.That(state.IsTraveling, Is.False);
        }

        [Test]
        public void TryBegin_WhileTraveling_RejectsSecondRequestAndKeepsFirstArrival()
        {
            state.TryBegin("CafeScene", "TownDoor");

            bool isSecondStarted = state.TryBegin("HotelScene", "LobbyDoor");

            Assert.That(isSecondStarted, Is.False);
            Assert.That(state.CompleteArrival(), Is.EqualTo("TownDoor"));
        }

        [Test]
        public void CompleteArrival_ReturnsArrivalDoorwayAndEndsTravel()
        {
            state.TryBegin("TownScene", "HomeDoor");

            string arrivalDoorwayId = state.CompleteArrival();

            Assert.That(arrivalDoorwayId, Is.EqualTo("HomeDoor"));
            Assert.That(state.IsTraveling, Is.False);
        }

        [Test]
        public void CompleteArrival_WithoutTravel_ReturnsNoDoorway()
        {
            // 씬을 Editor에서 바로 열고 Play한 경우: 씬에 놓인 위치에서 시작한다.
            Assert.That(state.CompleteArrival(), Is.Null);
        }

        [Test]
        public void CompleteArrival_IsConsumedOnce()
        {
            state.TryBegin("TownScene", "HomeDoor");
            state.CompleteArrival();

            Assert.That(state.CompleteArrival(), Is.Null);
        }

        [Test]
        public void TryBegin_WithoutArrivalDoorway_StartsTravelAndArrivesAtScenePlacement()
        {
            bool isStarted = state.TryBegin("HomeScene", null);

            Assert.That(isStarted, Is.True);
            Assert.That(state.CompleteArrival(), Is.Null);
        }

        [Test]
        public void TryBegin_AfterArrival_AcceptsNextTravel()
        {
            state.TryBegin("TownScene", "HomeDoor");
            state.CompleteArrival();

            bool isStarted = state.TryBegin("CafeScene", "TownDoor");

            Assert.That(isStarted, Is.True);
            Assert.That(state.CompleteArrival(), Is.EqualTo("TownDoor"));
        }

        [Test]
        public void Cancel_EndsTravelWithoutArrivalAndAcceptsNextTravel()
        {
            // 씬을 불러오지 못했을 때 이동 중 상태에 갇히지 않아야 한다.
            state.TryBegin("MissingScene", "Door");

            state.Cancel();

            Assert.That(state.IsTraveling, Is.False);
            Assert.That(state.CompleteArrival(), Is.Null);
            Assert.That(state.TryBegin("TownScene", "HomeDoor"), Is.True);
        }
    }
}
