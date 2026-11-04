using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public class MockFeedbackExampleSourceTests
    {
        // 튜토리얼 Ben은 카페 가는 길을 묻는 행인이라, 길을 알려 줄 때 현지인이 자주 쓰는 표현을 3개 넘게 보여 준다.
        [Test]
        public void GetSituationExpressions_ForBen_GivesDirectionIdioms()
        {
            var source = ScriptableObject.CreateInstance<MockFeedbackExampleSource>();

            var situations = source.GetSituationExpressions("Ben");

            Assert.That(situations, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(situations.All(s => !string.IsNullOrEmpty(s.MeaningKo)), Is.True);
            Assert.That(situations.All(s => !string.IsNullOrEmpty(s.UsageKo)), Is.True);
            Assert.That(situations.All(s => !string.IsNullOrEmpty(s.ExampleEn)), Is.True);
            Assert.That(
                situations.Select(s => s.Expression.ToLowerInvariant()),
                Has.None.Contains("check").And.None.Contains("reservation"),
                "길 안내 대화에 호텔 체크인 표현을 보이지 않습니다."
            );
            Assert.That(
                situations.Select(s => s.ExampleEn),
                Has.Some.Contains("left").Or.Some.Contains("right"),
                "길을 알려 주는 예문이 있습니다."
            );
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
