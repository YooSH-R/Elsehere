using System.Collections.Generic;
using A305.EditorTools.Mission;
using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>앉을 수 있는 가구를 고르고, 가구마다 앉기 자리의 방향과 앉는 면을 맞추는 규칙.</summary>
    public class RestSeatLayoutTests
    {
        [TestCase("chair")]
        [TestCase("bench")]
        [TestCase("sofa")]
        [TestCase("stool")]
        [TestCase("barber chair")]
        public void IsSeatWord_FurnitureMadeForSitting_IsSeat(string word)
        {
            Assert.That(RestSeatLayout.IsSeatWord(word), Is.True);
        }

        [TestCase("bed")]
        [TestCase("stairs")]
        [TestCase("table")]
        [TestCase("")]
        [TestCase(null)]
        public void IsSeatWord_OtherFurniture_IsNotSeat(string word)
        {
            Assert.That(RestSeatLayout.IsSeatWord(word), Is.False);
        }

        [Test]
        public void BacklessYaw_NothingToFace_KeepsFurnitureFront()
        {
            Assert.That(RestSeatLayout.BacklessYawDegrees(new List<Vector3>()), Is.EqualTo(0f));
        }

        [TestCase(0f, -.18f, 180f)]
        [TestCase(.16f, 0f, 90f)]
        [TestCase(-.53f, .11f, 270f)]
        [TestCase(0f, .45f, 0f)]
        public void BacklessYaw_FacesTheSurfaceInFrontOfTheSeat(
            float surfaceX,
            float surfaceZ,
            float expectedYaw
        )
        {
            // 오락기·카운터·탁자처럼 앉아서 마주할 면 쪽으로, 가구 네 방향 중 가까운 쪽을 본다.
            var surfaces = new List<Vector3> { new Vector3(surfaceX, 0f, surfaceZ) };

            Assert.That(RestSeatLayout.BacklessYawDegrees(surfaces), Is.EqualTo(expectedYaw));
        }

        [Test]
        public void BacklessYaw_SeveralSurfaces_FacesTheNearest()
        {
            // 빵집 작은 의자: 뒤쪽 탁자가 옆 카운터보다 가깝다.
            var surfaces = new List<Vector3>
            {
                new Vector3(-.32f, 0f, 0f),
                new Vector3(0f, 0f, -.28f),
            };

            Assert.That(RestSeatLayout.BacklessYawDegrees(surfaces), Is.EqualTo(180f));
        }

        [Test]
        public void BacklessYaw_SurfaceBeyondReach_KeepsFurnitureFront()
        {
            // 세탁소 대기 의자: 0.74m 떨어진 탁자는 마주하는 탁자가 아니다.
            var surfaces = new List<Vector3> { new Vector3(-.42f, 0f, -.61f) };

            Assert.That(RestSeatLayout.BacklessYawDegrees(surfaces), Is.EqualTo(0f));
        }

        [Test]
        public void ScaledBy_ShrunkFurniture_ShrinksSeatHeightAndDepth()
        {
            // 빵집·가구점 2층처럼 줄여 놓은 의자는 앉는 면도 그만큼 낮고 얕다.
            var chair = new RestSeatModel("Chair.fbx", .90f, .28f, new[] { 0f });

            RestSeatModel shrunk = chair.ScaledBy(new Vector3(.75f, .5f, .75f));

            Assert.That(shrunk.CushionTopMeters, Is.EqualTo(.45f).Within(1e-5f));
            Assert.That(shrunk.FrontEdgeMeters, Is.EqualTo(.21f).Within(1e-5f));
        }

        [Test]
        public void ScaledBy_SeatFacingSideways_UsesSidewaysScaleForFrontEdge()
        {
            // 이발 의자처럼 가구 옆(-x)을 보는 자리는 앞 모서리가 x 방향에 있다.
            var barberChair = new RestSeatModel("Barber.asset", .64f, .31f, new[] { 0f }, -90f);

            RestSeatModel scaled = barberChair.ScaledBy(new Vector3(2f, 1f, .5f));

            Assert.That(scaled.FrontEdgeMeters, Is.EqualTo(.62f).Within(1e-5f));
            Assert.That(scaled.YawDegrees, Is.EqualTo(-90f));
        }
    }
}
