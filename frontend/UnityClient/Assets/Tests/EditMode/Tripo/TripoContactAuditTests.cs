using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoContactAuditTests
    {
        [Test]
        public void TopologyFingerprint_IgnoresScaleButDetectsChangedVertexConnectivity()
        {
            var root = new GameObject("Mesh");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            try
            {
                string original = TripoContactAudit.GetTopologyFingerprint(root);
                root.transform.localScale = Vector3.one * 1.15f;
                Assert.That(TripoContactAudit.GetTopologyFingerprint(root), Is.EqualTo(original));
                mesh.triangles = new[] { 0, 2, 1 };
                Assert.That(
                    TripoContactAudit.GetTopologyFingerprint(root),
                    Is.Not.EqualTo(original)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ContactSamples_CheckSeamAndSignedPenetration_RelativeToUniformHeight()
        {
            var root = new GameObject("Character");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var part = new GameObject("Part");
            part.transform.SetParent(root.transform, false);
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                triangles = new[] { 0, 1, 2 },
            };
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var sample = new TripoContactSample { bodyMeshPath = "Body", partMeshPath = "Part" };
            try
            {
                part.transform.localPosition = Vector3.forward * 0.002f;
                Assert.That(
                    TripoContactAudit.Inspect(root, new[] { sample }, 1f, profile).Passed,
                    Is.False
                );
                root.transform.localScale = Vector3.one * 1.15f;
                Assert.That(
                    TripoContactAudit.Inspect(root, new[] { sample }, 1.15f, profile).Passed,
                    Is.False
                );
                part.transform.localPosition = Vector3.forward * 0.0002f;
                Assert.That(
                    TripoContactAudit.Inspect(root, new[] { sample }, 1.15f, profile).Passed,
                    Is.True
                );
                sample.kind = TripoContactKind.Penetration;
                part.transform.localPosition = Vector3.back * 0.001f;
                Assert.That(
                    TripoContactAudit.Inspect(root, new[] { sample }, 1.15f, profile).Passed,
                    Is.False
                );
                sample.bodyMeshPath = "Missing";
                Assert.That(
                    TripoContactAudit.Inspect(root, new[] { sample }, 1.15f, profile).Errors,
                    Has.Some.Contains("Missing")
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
