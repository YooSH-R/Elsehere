using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 유리·버튼 그림은 그림자를 그릴 바깥 여백을 가진다. 그 여백만큼 그리는 면을 넓혀 보이는 모양이 칸 경계에 맞게 한다.
    /// 9칸 나눔의 테두리 폭은 그대로 두고 바깥쪽으로만 민다.
    /// </summary>
    public class SurfaceBleedTests
    {
        private GameObject target;

        [TearDown]
        public void Clean() => Object.DestroyImmediate(target);

        [Test]
        public void ModifyMesh_PushesEveryVertexOutwardByTheMargin()
        {
            SurfaceBleed bleed = Create(new Vector2(200, 100), new Vector2(30, 10));
            var mesh = new VertexHelper();
            // 9칸 나눔처럼 바깥 가장자리와 테두리 안쪽 줄의 꼭짓점
            foreach (
                var point in new[]
                {
                    new Vector2(-100, -50),
                    new Vector2(-60, -20),
                    new Vector2(60, 20),
                    new Vector2(100, 50),
                }
            )
                mesh.AddVert(point, Color.white, Vector2.zero);

            bleed.ModifyMesh(mesh);

            var moved = new List<Vector2>();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                UIVertex vertex = default;
                mesh.PopulateUIVertex(ref vertex, i);
                moved.Add(vertex.position);
            }
            Assert.That(moved[0], Is.EqualTo(new Vector2(-130, -60)));
            Assert.That(moved[1], Is.EqualTo(new Vector2(-90, -30)));
            Assert.That(moved[2], Is.EqualTo(new Vector2(90, 30)));
            Assert.That(moved[3], Is.EqualTo(new Vector2(130, 60)));
        }

        [Test]
        public void ModifyMesh_WithoutMargin_LeavesTheMeshAlone()
        {
            SurfaceBleed bleed = Create(new Vector2(200, 100), Vector2.zero);
            var mesh = new VertexHelper();
            mesh.AddVert(new Vector2(-100, -50), Color.white, Vector2.zero);

            bleed.ModifyMesh(mesh);

            UIVertex vertex = default;
            mesh.PopulateUIVertex(ref vertex, 0);
            Assert.That((Vector2)vertex.position, Is.EqualTo(new Vector2(-100, -50)));
        }

        private SurfaceBleed Create(Vector2 size, Vector2 margin)
        {
            target = new GameObject("Surface", typeof(RectTransform), typeof(Image));
            target.GetComponent<RectTransform>().sizeDelta = size;
            var bleed = target.AddComponent<SurfaceBleed>();
            bleed.Margin = margin;
            return bleed;
        }
    }
}
