using System.Linq;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class MannequinEyelidTests
    {
        [Test]
        public void BlinkCoversStationaryEyesAndRestoresNeutral()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Female_160.prefab"
            );
            var instance = Object.Instantiate(prefab);
            try
            {
                var face = instance.GetComponentInChildren<CharacterFaceController>();
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (string side in new[] { "Left", "Right" })
                {
                    var eye = skins.SingleOrDefault(s => s.sharedMesh.name == side + "Eye");
                    Assert.That(eye, Is.Not.Null, "Blink needs a separate stationary eyeball.");
                    var upper = skins.Single(s => s.sharedMesh.name == "Upper" + side + "Lid");
                    var lower = skins.Single(s => s.sharedMesh.name == "Lower" + side + "Lid");
                    string channel = "eyeBlink" + side;
                    Assert.That(eye.sharedMesh.GetBlendShapeIndex(channel), Is.EqualTo(-1));
                    int index = upper.sharedMesh.GetBlendShapeIndex(channel);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0));
                    Assert.That(
                        upper.sharedMesh.GetBlendShapeFrameCount(index),
                        Is.GreaterThanOrEqualTo(4)
                    );
                    var eyeBase = eye.sharedMesh.vertices;
                    face.SetPose(CharacterFacePose.Blink);
                    CollectionAssert.AreEqual(eyeBase, eye.sharedMesh.vertices);
                    // Head-local rays: Unity X is depth, Y is up, Z is lateral.
                    float z = side == "Left" ? -.130f : .130f;
                    foreach (float y in new[] { .580f, .600f, .615f })
                    {
                        float eyeDepth = Depth(eye.sharedMesh, eyeBase, y, z);
                        float closed = Mathf.Min(
                            Depth(upper.sharedMesh, Posed(upper.sharedMesh, channel), y, z),
                            Depth(lower.sharedMesh, Posed(lower.sharedMesh, channel), y, z)
                        );
                        Assert.That(
                            closed,
                            Is.LessThan(eyeDepth),
                            side + " lid must cover the fixed eye."
                        );
                    }
                }
                face.SetPose(CharacterFacePose.Neutral);
                foreach (var skin in skins)
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                        Assert.That(skin.GetBlendShapeWeight(i), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Vector3[] Posed(Mesh mesh, string channel)
        {
            var vertices = mesh.vertices;
            var delta = new Vector3[vertices.Length];
            int shape = mesh.GetBlendShapeIndex(channel);
            mesh.GetBlendShapeFrameVertices(
                shape,
                mesh.GetBlendShapeFrameCount(shape) - 1,
                delta,
                null,
                null
            );
            return vertices.Zip(delta, (v, d) => v + d).ToArray();
        }

        private static float Depth(Mesh mesh, Vector3[] vertices, float y, float z)
        {
            float result = float.PositiveInfinity;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.y - c.y) + (c.y - b.y) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 1e-10f)
                    continue;
                float u = ((b.z - c.z) * (y - c.y) + (c.y - b.y) * (z - c.z)) / denominator;
                float v = ((c.z - a.z) * (y - c.y) + (a.y - c.y) * (z - c.z)) / denominator;
                if (u >= -1e-5f && v >= -1e-5f && u + v <= 1.00001f)
                    result = Mathf.Min(result, a.x * u + b.x * v + c.x * (1 - u - v));
            }
            return result;
        }
    }
}
