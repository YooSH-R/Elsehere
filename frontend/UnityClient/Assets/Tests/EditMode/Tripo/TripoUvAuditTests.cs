using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoUvAuditTests
    {
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Inspect_NonFiniteUv_BlocksApproval(float invalid)
        {
            var mesh = CreateMesh();
            mesh.uv = new[] { new Vector2(invalid, 0), Vector2.right, Vector2.up, Vector2.one };
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.Errors, Has.Some.Contains("UV0 비정상 값"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase("range", "UV0 범위")]
        [TestCase("collapsed", "UV0 퇴화")]
        [TestCase("overlap", "UV0 겹침")]
        public void Inspect_SuspiciousUv_RequiresManualReviewWithoutMutation(
            string scenario,
            string expected
        )
        {
            var mesh = CreateMesh();
            if (scenario == "range")
                mesh.uv = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.one };
            if (scenario == "collapsed")
                mesh.uv = new Vector2[4];
            if (scenario == "overlap")
                mesh.triangles = new[] { 0, 1, 2, 0, 1, 2 };
            var before = mesh.uv;
            var triangles = mesh.triangles;
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.Errors, Is.Empty);
                Assert.That(report.ManualChecks, Has.Some.Contains(expected));
                Assert.That(mesh.uv, Is.EqualTo(before));
                Assert.That(mesh.triangles, Is.EqualTo(triangles));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Inspect_SharedEdgeOrSeparateSubmeshes_AreNotOverlap(bool separateSubmeshes)
        {
            var mesh = CreateMesh();
            if (separateSubmeshes)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 0, 1, 2 }, 1);
            }
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.Errors, Is.Empty);
                Assert.That(report.ManualChecks, Has.None.Contains("UV0 겹침"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Inspect_UnreadableMesh_DoesNotReportSuccessfulInspection()
        {
            var mesh = CreateMesh();
            mesh.UploadMeshData(true);
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.Errors, Has.Some.Contains("검사 불가"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Inspect_ContainedOrReverseWindingTriangles_ReportsOverlap(bool reverse)
        {
            var mesh = CreateMesh();
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one * .25f };
            mesh.triangles = reverse ? new[] { 0, 1, 2, 2, 1, 0 } : new[] { 0, 1, 2, 0, 1, 3 };
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.ManualChecks, Has.Some.Contains("UV0 겹침 1쌍"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Inspect_OverlappingBoundingBoxesWithoutTriangleOverlap_DoesNotWarn()
        {
            var mesh = new Mesh
            {
                vertices = new Vector3[6],
                uv = new[]
                {
                    Vector2.zero,
                    new Vector2(.8f, 0),
                    new Vector2(0, .8f),
                    new Vector2(.5f, .5f),
                    new Vector2(.9f, .5f),
                    new Vector2(.5f, .9f),
                },
                triangles = new[] { 0, 1, 2, 3, 4, 5 },
            };
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.ManualChecks, Has.None.Contains("UV0 겹침"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Inspect_ExcessiveOverlapCandidates_ExplicitlyReportsIncompleteInspection()
        {
            var mesh = CreateMesh();
            var indices = new int[1500 * 3];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i % 3;
            mesh.triangles = indices;
            try
            {
                var report = new TripoAuditReport();
                TripoUvAudit.Inspect(mesh, true, report);
                Assert.That(report.ManualChecks, Has.Some.Contains("검사 미완료"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static Mesh CreateMesh() =>
            new Mesh
            {
                name = "UvFixture",
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 1, 2, 1, 3, 2 },
            };
    }
}
