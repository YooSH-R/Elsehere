using NUnit.Framework;

namespace A305.World.Tests
{
    /// <summary>
    /// 경찰서 옆에 서 있던 순찰차는 신고를 받으면 경광등을 켜고 주차 칸을 나와, 말한 장소로 가서 잠시 서 있다가 돌아온다.
    /// 장소가 없으면 본길을 한 바퀴 순찰하고 돌아온다. 돌아오는 길에는 경광등을 끈다.
    /// </summary>
    public class PoliceDispatchTests
    {
        private PoliceDispatch dispatch;

        [SetUp]
        public void Create()
        {
            dispatch = new PoliceDispatch();
        }

        [Test]
        public void NewPatrolCar_IsParkedWithLightsOff()
        {
            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(dispatch.LightsOn, Is.False);
        }

        [Test]
        public void Dispatch_WhileParked_LeavesTheBayWithLightsOn()
        {
            Assert.That(dispatch.TryDispatch("DINER"), Is.True);
            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.LeavingBay));
            Assert.That(dispatch.SceneCode, Is.EqualTo("DINER"));
            Assert.That(dispatch.LightsOn, Is.True);
        }

        // 신고를 받은 경찰관이 직접 순찰차를 몰고 나가, 돌아와 주차할 때까지 운전석에 있다.
        [Test]
        public void Dispatch_WithTheOfficer_KeepsThemAtTheWheelUntilParkedAgain()
        {
            dispatch.TryDispatch("DINER", "POLICE_SERGEANT");
            Assert.That(dispatch.DriverCode, Is.EqualTo("POLICE_SERGEANT"));

            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f); // 장소로
            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f); // 장소 앞
            dispatch.Tick(reachedLegEnd: false, deltaSeconds: PoliceDispatch.OnSceneSeconds);
            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f); // 주차 칸으로
            Assert.That(dispatch.DriverCode, Is.EqualTo("POLICE_SERGEANT"));

            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(dispatch.DriverCode, Is.Null);
        }

        [Test]
        public void Dispatch_WhileAlreadyOut_KeepsTheFirstOfficerAtTheWheel()
        {
            dispatch.TryDispatch("DINER", "POLICE_OFFICER");

            dispatch.TryDispatch("BANK", "POLICE_PATROL_OFFICER");

            Assert.That(dispatch.DriverCode, Is.EqualTo("POLICE_OFFICER"));
        }

        [Test]
        public void Dispatch_WhileAlreadyOut_IsIgnored()
        {
            dispatch.TryDispatch("DINER");

            Assert.That(dispatch.TryDispatch("BANK"), Is.False);
            Assert.That(dispatch.SceneCode, Is.EqualTo("DINER"));
        }

        [Test]
        public void LeavingTheBay_WithAPlace_RespondsToIt()
        {
            dispatch.TryDispatch("DINER");

            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Responding));
        }

        [Test]
        public void LeavingTheBay_WithoutAPlace_Patrols()
        {
            dispatch.TryDispatch(null);

            dispatch.Tick(reachedLegEnd: true, deltaSeconds: .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Patrolling));
            Assert.That(dispatch.LightsOn, Is.True);
        }

        [Test]
        public void UntilTheLegEnds_ThePhaseStays()
        {
            dispatch.TryDispatch("DINER");

            dispatch.Tick(reachedLegEnd: false, deltaSeconds: 30);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.LeavingBay));
        }

        [Test]
        public void ArrivingAtThePlace_StaysOnSceneWithLightsOn()
        {
            dispatch.TryDispatch("DINER");
            dispatch.Tick(true, .1f);

            dispatch.Tick(true, .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.OnScene));
            Assert.That(dispatch.LightsOn, Is.True);
        }

        [Test]
        public void OnScene_ReturnsAfterTheWaitWithLightsOff()
        {
            dispatch.TryDispatch("DINER");
            dispatch.Tick(true, .1f);
            dispatch.Tick(true, .1f);

            dispatch.Tick(true, PoliceDispatch.OnSceneSeconds - 1);
            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.OnScene));

            dispatch.Tick(true, 1);
            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Returning));
            Assert.That(dispatch.LightsOn, Is.False);
        }

        [Test]
        public void FinishingThePatrol_HeadsIntoTheBay()
        {
            dispatch.TryDispatch(null);
            dispatch.Tick(true, .1f);

            dispatch.Tick(true, .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.EnteringBay));
            Assert.That(dispatch.LightsOn, Is.False);
        }

        [Test]
        public void BackInTheBay_IsParkedAndCanBeDispatchedAgain()
        {
            dispatch.TryDispatch("DINER");
            dispatch.Tick(true, .1f);
            dispatch.Tick(true, .1f);
            dispatch.Tick(true, PoliceDispatch.OnSceneSeconds);
            dispatch.Tick(true, .1f);
            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.EnteringBay));

            dispatch.Tick(true, .1f);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(dispatch.SceneCode, Is.Null);
            Assert.That(dispatch.TryDispatch("BANK"), Is.True);
        }

        [Test]
        public void WhileParked_TickDoesNothing()
        {
            dispatch.Tick(true, 100);

            Assert.That(dispatch.Phase, Is.EqualTo(PolicePhase.Parked));
        }
    }
}
