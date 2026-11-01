using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>소파·벤치처럼 자리가 붙어 있어도 플레이어에게 가장 가까운 자리 하나에만 앉기를 제안한다.</summary>
    public class RestSeatPickerTests
    {
        private const float ReachMeters = 2f;

        [Test]
        public void Nearest_PicksTheClosestSeatWithinReach()
        {
            // 소파 앞에서 오른쪽 쿠션에 더 붙어 선 경우
            var sofaSeats = new List<Vector3>
            {
                new Vector3(-.57f, 0f, 0f),
                new Vector3(.57f, 0f, 0f),
            };

            int nearest = RestSeatPicker.Nearest(new Vector3(.3f, 0f, 1f), sofaSeats, ReachMeters);

            Assert.That(nearest, Is.EqualTo(1));
        }

        [Test]
        public void Nearest_SeatExactlyAtReach_CanBeChosen()
        {
            var seats = new List<Vector3> { new Vector3(0f, 0f, ReachMeters) };

            Assert.That(RestSeatPicker.Nearest(Vector3.zero, seats, ReachMeters), Is.EqualTo(0));
        }

        [Test]
        public void Nearest_EverySeatBeyondReach_ChoosesNone()
        {
            var seats = new List<Vector3> { new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, -3f) };

            Assert.That(RestSeatPicker.Nearest(Vector3.zero, seats, ReachMeters), Is.EqualTo(-1));
        }

        [Test]
        public void Nearest_WithoutSeats_ChoosesNone()
        {
            var seats = new List<Vector3>();

            Assert.That(RestSeatPicker.Nearest(Vector3.zero, seats, ReachMeters), Is.EqualTo(-1));
        }
    }
}
