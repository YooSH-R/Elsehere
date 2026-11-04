using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 대화에서 준비해 주기로 한 것이 다 되면 NPC가 자리를 떠나지 않고 부른다. 부르는 거리 안에서만 부르고,
    /// 대화를 마친 뒤 쉬는 시간은 기다리지 않지만 부른 것을 거절·무시하면 쉬는 시간이 지나야 다시 부른다.
    /// </summary>
    public class PasserbyFollowUpCallTests
    {
        private const float CallDistance = 6f;
        private const float RestSeconds = 20f;

        // 카운터 직원처럼 감지 거리(3.5m)·무시 거리(4m)가 부르는 거리보다 짧은 NPC
        private static readonly PasserbyRules CounterStaff = new PasserbyRules(
            detectRadiusMeters: 3.5f,
            offerDistanceMeters: 2.5f,
            dismissDistanceMeters: 4f,
            restSeconds: RestSeconds,
            canDecline: true,
            followUpCallDistanceMeters: CallDistance
        );

        [Test]
        public void TryCall_WithinCallDistance_OffersFromWhereItStands()
        {
            var staff = new PasserbyApproach(CounterStaff);

            bool called = staff.TryCall(0d, distanceToPlayer: 5.5f, isPlayerBusy: false);

            // 다가가지(Approaching) 않고 바로 제안한다.
            Assert.That(called, Is.True);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Offering));
            Assert.That(staff.IsCalling, Is.True);
        }

        [Test]
        public void TryCall_FartherThanCallDistance_DoesNotCall()
        {
            var staff = new PasserbyApproach(CounterStaff);

            bool called = staff.TryCall(0d, distanceToPlayer: 6.5f, isPlayerBusy: false);

            Assert.That(called, Is.False);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Waiting));
            Assert.That(staff.IsCalling, Is.False);
        }

        [Test]
        public void TryCall_PlayerBusy_DoesNotCall()
        {
            // 다른 대화나 다른 NPC의 제안 중에는 끼어들지 않는다.
            var staff = new PasserbyApproach(CounterStaff);

            Assert.That(staff.TryCall(0d, 3f, isPlayerBusy: true), Is.False);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void TryCall_RightAfterTheOrderConversation_CallsWithoutWaitingRestTime()
        {
            // 주문 대화를 마치고 쉬는 중(20초)이어도 준비가 다 되면 부른다.
            var staff = new PasserbyApproach(CounterStaff);
            staff.Tick(0d, 2f, isPlayerBusy: false);
            staff.Tick(0.1d, 2f, isPlayerBusy: false);
            Assert.That(staff.TryAccept(), Is.True, "준비: 주문 대화");
            staff.EndTalk(10d);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Resting), "준비: 대화 뒤 쉬는 중");

            Assert.That(staff.TryCall(15d, 3f, isPlayerBusy: false), Is.True);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        // 걷는 속도가 0인 카운터 직원은 감지 거리(3.5m) 안, 제안 거리(2.5m) 밖에서 다가가는 상태로 서 있다. 그때도 그 자리에서 부른다.
        [Test]
        public void TryCall_WhileApproaching_StopsAndCallsFromThere()
        {
            var staff = new PasserbyApproach(CounterStaff);
            staff.Tick(0d, 3f, isPlayerBusy: false);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Approaching), "준비: 다가가는 중");

            Assert.That(staff.TryCall(0.1d, 3f, isPlayerBusy: false), Is.True);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Offering));
            Assert.That(staff.IsCalling, Is.True);
        }

        [Test]
        public void TryCall_WhileOfferingOrTalking_DoesNotCall()
        {
            var staff = new PasserbyApproach(CounterStaff);
            staff.Tick(0d, 2f, isPlayerBusy: false);
            staff.Tick(0.1d, 2f, isPlayerBusy: false);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Offering), "준비: 평소 제안 중");
            Assert.That(staff.TryCall(0.2d, 2f, isPlayerBusy: false), Is.False);
            Assert.That(staff.IsCalling, Is.False);

            staff.TryAccept();
            Assert.That(staff.TryCall(0.3d, 2f, isPlayerBusy: false), Is.False);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Talking));
        }

        [Test]
        public void TryCall_RulesWithoutCallDistance_NeverCall()
        {
            var rules = new PasserbyRules(6f, 2f, 6f, 60f, canDecline: true);
            var passerby = new PasserbyApproach(rules);

            Assert.That(passerby.TryCall(0d, 0.5f, isPlayerBusy: false), Is.False);
        }

        [Test]
        public void Calling_PlayerStaysWithinCallDistance_KeepsTheOfferOpen()
        {
            // 무시 거리(4m)보다 멀어도 부르는 거리(6m) 안이면 부르는 중에는 닫지 않는다.
            var staff = new PasserbyApproach(CounterStaff);
            staff.TryCall(0d, 5.5f, isPlayerBusy: false);

            staff.Tick(1d, 5.5f, isPlayerBusy: false);

            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        [Test]
        public void DeclinedCall_CallsAgainOnlyAfterRestTime()
        {
            var staff = new PasserbyApproach(CounterStaff);
            staff.TryCall(0d, 3f, isPlayerBusy: false);

            Assert.That(staff.TryDecline(1d), Is.True);
            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(staff.IsCalling, Is.False);
            Assert.That(staff.TryCall(10d, 3f, isPlayerBusy: false), Is.False);

            Assert.That(staff.TryCall(1d + RestSeconds, 3f, isPlayerBusy: false), Is.True);
        }

        [Test]
        public void IgnoredCall_PlayerWalksBeyondCallDistance_RestsLikeDecline()
        {
            var staff = new PasserbyApproach(CounterStaff);
            staff.TryCall(0d, 5f, isPlayerBusy: false);

            staff.Tick(1d, 6.5f, isPlayerBusy: false);

            Assert.That(staff.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(staff.TryCall(5d, 3f, isPlayerBusy: false), Is.False);
            Assert.That(staff.TryCall(1d + RestSeconds, 3f, isPlayerBusy: false), Is.True);
        }

        [Test]
        public void AcceptedCall_IfStillNotHandedOver_CallsAgainOnlyAfterRestTime()
        {
            // 서버가 건네지 못해 아직 남아 있어도 대화를 마치자마자 다시 부르지 않는다.
            var staff = new PasserbyApproach(CounterStaff);
            staff.TryCall(0d, 3f, isPlayerBusy: false);
            Assert.That(staff.TryAccept(), Is.True);
            staff.EndTalk(5d);

            Assert.That(staff.IsCalling, Is.False);
            Assert.That(staff.TryCall(6d, 3f, isPlayerBusy: false), Is.False);
            Assert.That(staff.TryCall(5d + RestSeconds, 3f, isPlayerBusy: false), Is.True);
        }

        [Test]
        public void Settings_CallFromAboutSixMetersByDefault()
        {
            var settings = ScriptableObject.CreateInstance<PasserbySettings>();

            Assert.That(settings.ToRules().FollowUpCallDistanceMeters, Is.EqualTo(6f));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CallHint_NamesWhoIsCalling()
        {
            Assert.That(PasserbyOfferText.CallHint("Mia"), Is.EqualTo("Mia이(가) 불러요"));
            Assert.That(PasserbyOfferText.CallHint("바리스타"), Is.EqualTo("바리스타가 불러요"));
            Assert.That(PasserbyOfferText.CallHint(null), Is.EqualTo("누군가 불러요"));
        }

        [Test]
        public void CallLine_SaysWhatIsReadyWithTheRightParticle()
        {
            Assert.That(
                PasserbyOfferText.CallLine("아이스 라테"),
                Is.EqualTo("기다리던 아이스 라테가 준비됐어요")
            );
            Assert.That(
                PasserbyOfferText.CallLine("꽃다발"),
                Is.EqualTo("기다리던 꽃다발이 준비됐어요")
            );
            Assert.That(PasserbyOfferText.CallLine(""), Is.EqualTo("기다리던 것이 준비됐어요"));
        }
    }
}
