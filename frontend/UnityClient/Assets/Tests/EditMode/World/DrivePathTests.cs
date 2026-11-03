using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>주차 칸과 본길 사이를 잇는 짧은 곡선 길. 3차 베지어 곡선을 이어 붙이고 길 위 거리로 위치·방향을 구한다.</summary>
    public class DrivePathTests
    {
        // 동쪽으로 나가 왼쪽으로 꺾어 북쪽을 향하는 길
        private static DrivePath LeftTurn() =>
            new DrivePath(
                new DrivePath.Curve(
                    new Vector3(0, 0, 0),
                    new Vector3(3, 0, 0),
                    new Vector3(5, 0, 2),
                    new Vector3(5, 0, 5)
                )
            );

        [Test]
        public void Start_IsTheFirstPointFacingItsFirstHandle()
        {
            DrivePath.Pose pose = LeftTurn().PoseAt(0);

            Assert.That(Vector3.Distance(pose.Position, Vector3.zero), Is.LessThan(.001f));
            Assert.That(Vector3.Dot(pose.Forward, Vector3.right), Is.GreaterThan(.999f));
        }

        [Test]
        public void End_IsTheLastPointFacingAwayFromItsLastHandle()
        {
            DrivePath path = LeftTurn();
            DrivePath.Pose pose = path.PoseAt(path.Length);

            Assert.That(Vector3.Distance(pose.Position, new Vector3(5, 0, 5)), Is.LessThan(.001f));
            Assert.That(Vector3.Dot(pose.Forward, Vector3.forward), Is.GreaterThan(.999f));
        }

        [Test]
        public void Length_IsLongerThanTheStraightLineButShorterThanTheHandles()
        {
            float length = LeftTurn().Length;

            Assert.That(length, Is.GreaterThan(new Vector3(5, 0, 5).magnitude));
            Assert.That(length, Is.LessThan(3 + new Vector2(2, 2).magnitude + 3));
        }

        [Test]
        public void DistancesBeyondTheEnds_StayOnTheEnds()
        {
            DrivePath path = LeftTurn();

            Assert.That(
                Vector3.Distance(path.PoseAt(-2).Position, Vector3.zero),
                Is.LessThan(.001f)
            );
            Assert.That(
                Vector3.Distance(path.PoseAt(path.Length + 2).Position, new Vector3(5, 0, 5)),
                Is.LessThan(.001f)
            );
        }

        [Test]
        public void EqualSteps_MoveAboutTheSameDistanceAlongTheCurve()
        {
            DrivePath path = LeftTurn();
            float step = path.Length / 10;
            for (int index = 0; index < 10; index++)
            {
                float moved = Vector3.Distance(
                    path.PoseAt(index * step).Position,
                    path.PoseAt((index + 1) * step).Position
                );

                Assert.That(moved, Is.EqualTo(step).Within(step * .05f), $"step {index}");
            }
        }

        [Test]
        public void TwoCurves_FollowOneAfterTheOther()
        {
            var first = new DrivePath.Curve(
                Vector3.zero,
                new Vector3(1, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0)
            );
            var second = new DrivePath.Curve(
                new Vector3(3, 0, 0),
                new Vector3(4, 0, 0),
                new Vector3(5, 0, 0),
                new Vector3(6, 0, 0)
            );
            DrivePath path = new DrivePath(first, second);

            Assert.That(path.Length, Is.EqualTo(6).Within(.01f));
            Assert.That(path.PoseAt(4.5f).Position.x, Is.EqualTo(4.5f).Within(.05f));
        }
    }
}
