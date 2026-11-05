using System.Reflection;
using A305.Player;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 카메라에서 먼 NPC는 팔 관통 보정을 끄고 가까우면 켠다. 이 보정은 매 프레임 피부 정점을 계산해 캐릭터 하나에 수 ms가 든다.
    /// </summary>
    public class NpcDetailDistanceTests
    {
        private const float Radius = 12f;

        private GameObject npc;
        private LittleMungArmClearance armClearance;
        private NpcDetailDistance detail;

        [SetUp]
        public void CreateNpc()
        {
            npc = new GameObject("Npc");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(npc.transform);
            armClearance = visual.AddComponent<LittleMungArmClearance>();
            detail = npc.AddComponent<NpcDetailDistance>();
        }

        [TearDown]
        public void DestroyNpc()
        {
            Object.DestroyImmediate(npc);
        }

        [Test]
        public void FarViewer_TurnsArmClearanceOff()
        {
            detail.Refresh(new Vector3(0, 1.5f, Radius + 1f));

            Assert.That(armClearance.enabled, Is.False);
        }

        [Test]
        public void ViewerComingClose_TurnsArmClearanceBackOn()
        {
            detail.Refresh(new Vector3(0, 1.5f, 40f));
            detail.Refresh(new Vector3(0, 1.5f, 5f));

            Assert.That(armClearance.enabled, Is.True);
        }

        [Test]
        public void ViewerExactlyAtTheRadius_KeepsArmClearanceOn()
        {
            detail.Refresh(new Vector3(Radius, 0, 0));

            Assert.That(armClearance.enabled, Is.True);
        }

        // 앉아 있는 동안에는 StandingPoseCorrections가 서 있는 자세 보정을 꺼 둔다. 카메라가 가까워졌다고 다시 켜면 앉은 자세를 덮어쓴다.
        [Test]
        public void NpcSeatedNearTheViewer_KeepsArmClearanceSuspended()
        {
            StandingPoseCorrections.Suspend(npc.transform);
            Sit();

            detail.Refresh(new Vector3(0, 1.5f, 5f));

            Assert.That(armClearance.enabled, Is.False);
        }

        [Test]
        public void NpcSeatedWhileFar_DoesNotTurnArmClearanceOnWhenTheViewerComesClose()
        {
            detail.Refresh(new Vector3(0, 1.5f, 40f));
            Sit();

            detail.Refresh(new Vector3(0, 1.5f, 5f));

            Assert.That(armClearance.enabled, Is.False);
        }

        [Test]
        public void NpcStandingUpNearTheViewer_GetsArmClearanceBack()
        {
            detail.Refresh(new Vector3(0, 1.5f, 40f));
            Sit();
            detail.Refresh(new Vector3(0, 1.5f, 5f));
            StandUp();

            detail.Refresh(new Vector3(0, 1.5f, 5f));

            Assert.That(armClearance.enabled, Is.True);
        }

        // 생활(NpcRoutine)이 의자에 앉힌 것과 같은 상태를 만든다. Edit Mode에서는 Awake가 돌지 않아 자리만 정한다.
        private void Sit()
        {
            SeatField.SetValue(Routine, new TestSeat());
        }

        private void StandUp()
        {
            SeatField.SetValue(Routine, null);
        }

        private NpcRoutine Routine =>
            npc.TryGetComponent(out NpcRoutine routine) ? routine : npc.AddComponent<NpcRoutine>();

        private static FieldInfo SeatField =>
            typeof(NpcRoutine).GetField("seat", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class TestSeat : INpcSeat
        {
            public Transform SeatPoint => null;
            public float CushionTopMeters => 0.45f;
            public float FrontEdgeMeters => 0.2f;
            public bool IsTaken => true;

            public bool TryTake(Object npc) => false;

            public void Release(Object npc) { }
        }
    }
}
