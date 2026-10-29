using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoSurfaceCleanupTests
    {
        [Test]
        public void RemovesOnlyTinyAttachedFacesWhenUnderlyingSurfaceStaysClosed()
        {
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    new Vector3(.0003f, 0, 0),
                    new Vector3(.0003f, 0, .0003f),
                    new Vector3(0, 1, 0),
                    new Vector3(.00015f, 0, .00015f),
                };
                int[] closed = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };
                mesh.triangles = new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3, 0, 1, 4, 1, 2, 4 };
                mesh.RecalculateBounds();
                Assert.That(TripoSurfaceCleanup.RemoveAttachedSlivers(mesh), Is.EqualTo(2));
                Assert.That(mesh.triangles, Is.EqualTo(closed));
                Assert.That(TripoSurfaceCleanup.RemoveAttachedSlivers(mesh), Is.Zero);
                mesh.triangles = new[] { 0, 1, 2 };
                Assert.That(
                    TripoSurfaceCleanup.RemoveAttachedSlivers(mesh),
                    Is.Zero,
                    "An intentional open surface must not be silently removed."
                );
                Assert.That(mesh.triangles, Has.Length.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
