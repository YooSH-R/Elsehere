using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class PoseCorrectiveDriverTests
    {
        [TestCase("bone")]
        [TestCase("shape")]
        [TestCase("renderer")]
        public void MissingTargetsAreRejectedDuringConfiguration(string missing)
        {
            var root = new GameObject("Character");
            var mesh = new Mesh();
            var profile = ScriptableObject.CreateInstance<PoseCorrectiveProfile>();
            try
            {
                var bone = new GameObject("Knee").transform;
                bone.SetParent(root.transform, false);
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("Knee", 100f, new[] { Vector3.up }, null, null);
                root.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
                profile.bindings.Add(
                    new PoseCorrectiveBinding
                    {
                        rendererPath = missing == "renderer" ? "Missing" : "",
                        shapeName = missing == "shape" ? "Missing" : "Knee",
                        joint = new CorrectiveJoint
                        {
                            bonePath = missing == "bone" ? "Missing" : "Knee",
                            magnitude = true,
                        },
                    }
                );
                Assert.Throws<System.InvalidOperationException>(() =>
                    root.AddComponent<PoseCorrectiveDriver>().Configure(profile)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AppliesCurrentJointPoseAndReturnsToZeroWithoutOverwritingOtherShapes()
        {
            var root = new GameObject("Character");
            var mesh = new Mesh();
            var profile = ScriptableObject.CreateInstance<PoseCorrectiveProfile>();
            try
            {
                var bone = new GameObject("Knee").transform;
                bone.SetParent(root.transform, false);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("Knee", 100f, new[] { Vector3.up }, null, null);
                mesh.AddBlendShapeFrame("Smile", 100f, new[] { Vector3.right }, null, null);
                renderer.sharedMesh = mesh;
                renderer.SetBlendShapeWeight(1, 37f);
                profile.bindings.Add(
                    new PoseCorrectiveBinding
                    {
                        rendererPath = "",
                        shapeName = "Knee",
                        joint = new CorrectiveJoint
                        {
                            bonePath = "Knee",
                            magnitude = true,
                            fullDegrees = 100f,
                        },
                    }
                );
                var driver = root.AddComponent<PoseCorrectiveDriver>();
                driver.Configure(profile);
                bone.localRotation = Quaternion.AngleAxis(50f, Vector3.right);
                driver.ApplyCorrections();
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(0.001f));
                Assert.That(renderer.GetBlendShapeWeight(1), Is.EqualTo(37f));
                bone.localRotation = Quaternion.identity;
                driver.ApplyCorrections();
                Assert.That(renderer.GetBlendShapeWeight(0), Is.Zero);
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
