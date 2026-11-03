using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public class MockFeedbackExampleSourceTests
    {
        // 튜토리얼(Ben, 호텔 체크인)도 그 상황에서 현지인이 자주 쓰는 표현을 3개 넘게 보여 준다.
        [Test]
        public void GetSituationExpressions_ForBen_GivesHotelCheckInIdioms()
        {
            var source = ScriptableObject.CreateInstance<MockFeedbackExampleSource>();

            var situations = source.GetSituationExpressions("Ben");

            Assert.That(situations, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(situations.All(s => !string.IsNullOrEmpty(s.MeaningKo)), Is.True);
            Assert.That(situations.All(s => !string.IsNullOrEmpty(s.UsageKo)), Is.True);
            Object.DestroyImmediate(source);
        }

        [Test]
        public void GetSituationExpressions_ForAnUnknownNpc_IsEmpty()
        {
            var source = ScriptableObject.CreateInstance<MockFeedbackExampleSource>();

            Assert.That(source.GetSituationExpressions("Nobody"), Is.Empty);
            Object.DestroyImmediate(source);
        }
    }
}
