using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordMeshRegionsTests
    {
        [Test]
        public void Split_CombinedSeparateShapes_DoesNotCoverEmptySpaceBetweenThem()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var combined = new Mesh();
            try
            {
                Mesh shape = cube.GetComponent<MeshFilter>().sharedMesh;
                combined.CombineMeshes(
                    new[]
                    {
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.left * 2f),
                        },
                        new CombineInstance
                        {
                            mesh = shape,
                            transform = Matrix4x4.Translate(Vector3.right * 2f),
                        },
                    }
                );
                Bounds[] regions = ObjectWordMeshRegions.Split(combined);
                Assert.That(regions, Has.Length.EqualTo(2));
                foreach (Bounds region in regions)
                {
                    Assert.That(region.Contains(Vector3.zero), Is.False);
                    Assert.That(region.size, Is.EqualTo(Vector3.one));
                }
                Assert.That(
                    ObjectWordMeshRegions.Split(shape),
                    Has.Length.EqualTo(1),
                    "면마다 복제된 정점은 같은 모양으로 묶어야 한다."
                );
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(combined);
            }
        }
    }
}
