using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 건물 안에서만 지내는 직원은 생활하며 고르는 사물·자리를 그 건물 1층 구역 안에서만 고른다.
    /// </summary>
    public class NpcStayAreaTests
    {
        // 마을 우체국 1층쯤의 구역
        private static readonly Bounds Shop = new Bounds(
            new Vector3(-10.9f, 1.6f, 30f),
            new Vector3(7.6f, 3.4f, 7.6f)
        );

        [Test]
        public void Allows_AnyPlace_WhenNotLimited()
        {
            Assert.That(NpcStayArea.Allows(false, Shop, Vector3.zero), Is.True);
        }

        [Test]
        public void Allows_PlaceInsideTheArea()
        {
            Assert.That(NpcStayArea.Allows(true, Shop, new Vector3(-12f, .5f, 31f)), Is.True);
        }

        [TestCase(-5f, .5f, 30f)] // 문 밖 보도
        [TestCase(0f, .5f, 30f)] // 길 건너
        [TestCase(-10.9f, 4.5f, 30f)] // 2층의 다른 가게
        public void Rejects_PlaceOutsideTheArea(float x, float y, float z)
        {
            Assert.That(NpcStayArea.Allows(true, Shop, new Vector3(x, y, z)), Is.False);
        }
    }
}
