using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CafeFemaleFaceAssetTests
    {
        [Test]
        public void TenSpeechMouthsHaveDistinctAperturesAndRequiredTeeth()
        {
            const string folder = "Assets/Art/Npc/CafeFemaleFace/";
            var mouth = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "MouthInterior.asset");
            Assert.That(mouth, Is.Not.Null);
            var boundary = ApertureBoundary(mouth);
            Assert.That(boundary.Count, Is.GreaterThan(8));
            var poses = new Dictionary<string, Vector3[]> { ["REST"] = mouth.vertices };
            foreach (
                string channel in new[]
                {
                    "viseme_PP",
                    "viseme_AA",
                    "viseme_E",
                    "viseme_OH",
                    "viseme_OU",
                    "viseme_FF",
                    "viseme_TH",
                    "viseme_I",
                    "viseme_EU",
                }
            )
            {
                int shape = mouth.GetBlendShapeIndex(channel);
                Assert.That(shape, Is.GreaterThanOrEqualTo(0), channel);
                var offsets = new Vector3[mouth.vertexCount];
                mouth.GetBlendShapeFrameVertices(shape, 0, offsets, null, null);
                var points = mouth.vertices;
                for (int index = 0; index < points.Length; index++)
                {
                    points[index] += offsets[index];
                    Assert.That(
                        float.IsFinite(points[index].x)
                            && float.IsFinite(points[index].y)
                            && float.IsFinite(points[index].z),
                        Is.True,
                        channel
                    );
                }
                poses[channel] = points;
            }
            var names = poses.Keys.ToArray();
            for (int first = 0; first < names.Length; first++)
            for (int second = first + 1; second < names.Length; second++)
            {
                float difference = boundary.Max(index =>
                    (poses[names[first]][index] - poses[names[second]][index]).magnitude
                );
                Assert.That(
                    difference,
                    Is.GreaterThan(.0001f),
                    names[first] + " / " + names[second]
                );
            }
            Assert.That(
                ApertureSize(poses["viseme_PP"], boundary).y,
                Is.LessThanOrEqualTo(.0006f),
                "M/B/P must form a closed lip seam."
            );
            Assert.That(
                ApertureSize(poses["viseme_OH"], boundary).z,
                Is.GreaterThan(ApertureSize(poses["viseme_OU"], boundary).z * 1.3f),
                "OU must be narrower than OH."
            );
            foreach (string channel in new[] { "viseme_E", "viseme_I", "viseme_EU" })
            {
                var size = ApertureSize(poses[channel], boundary);
                Assert.That(
                    size.z,
                    Is.GreaterThan(size.y * 3),
                    channel + " must be horizontally open."
                );
            }
            foreach (string part in new[] { "UpperTeeth", "LowerTeeth" })
            {
                var teeth = AssetDatabase.LoadAssetAtPath<Mesh>(folder + part + ".asset");
                Assert.That(teeth, Is.Not.Null, part);
                Assert.That(teeth.GetBlendShapeIndex("viseme_I"), Is.GreaterThanOrEqualTo(0), part);
            }
        }

        private static HashSet<int> ApertureBoundary(Mesh mesh)
        {
            var counts = new Dictionary<(int, int), int>();
            var triangles = mesh.triangles;
            for (int offset = 0; offset < triangles.Length; offset += 3)
            for (int corner = 0; corner < 3; corner++)
            {
                int first = triangles[offset + corner];
                int second = triangles[offset + (corner + 1) % 3];
                var edge = first < second ? (first, second) : (second, first);
                counts.TryGetValue(edge, out int count);
                counts[edge] = count + 1;
            }
            var points = mesh.vertices;
            var result = new HashSet<int>();
            foreach (var edge in counts)
                if (
                    edge.Value == 1
                    && (points[edge.Key.Item1] - points[edge.Key.Item2]).sqrMagnitude > 1e-12f
                )
                {
                    result.Add(edge.Key.Item1);
                    result.Add(edge.Key.Item2);
                }
            return result;
        }

        private static Vector3 ApertureSize(Vector3[] points, HashSet<int> boundary)
        {
            var bounds = new Bounds(points[boundary.First()], Vector3.zero);
            foreach (int index in boundary)
                bounds.Encapsulate(points[index]);
            return bounds.size;
        }

        [Test]
        public void PreviewKeepsOriginalBodyAndRigWhileAddingRealFaceDeformations()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Barista.prefab"
            );
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/CafeFemaleFacePreview.prefab"
            );
            Assert.That(preview, Is.Not.Null, "The face preview copy must exist.");
            Assert.That(preview.GetComponent<CharacterFaceController>(), Is.Not.Null);
            var original = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var copy = preview.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(copy.sharedMesh, Is.Not.SameAs(original.sharedMesh));
            Assert.That(copy.sharedMesh.vertices, Is.EqualTo(original.sharedMesh.vertices));
            Assert.That(copy.sharedMesh.boneWeights, Is.EqualTo(original.sharedMesh.boneWeights));
            Assert.That(copy.sharedMesh.bindposes, Is.EqualTo(original.sharedMesh.bindposes));
            Assert.That(
                preview.GetComponentInChildren<Animator>().avatar,
                Is.SameAs(source.GetComponentInChildren<Animator>().avatar)
            );
            Assert.That(original.sharedMesh.blendShapeCount, Is.Zero);
            foreach (
                string name in new[]
                {
                    "mouthSmileLeft",
                    "mouthSmileRight",
                    "eyeBlinkLeft",
                    "eyeBlinkRight",
                    "viseme_AA",
                    "viseme_FF",
                    "viseme_TH",
                }
            )
            {
                int index = copy.sharedMesh.GetBlendShapeIndex(name);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), name);
                var offsets = new Vector3[copy.sharedMesh.vertexCount];
                copy.sharedMesh.GetBlendShapeFrameVertices(index, 0, offsets, null, null);
                float maximum = 0;
                for (int i = 0; i < offsets.Length; i++)
                    maximum = Mathf.Max(maximum, offsets[i].magnitude);
                Assert.That(maximum, Is.GreaterThan(.0001f), name + " must deform vertices.");
            }
        }
    }
}
