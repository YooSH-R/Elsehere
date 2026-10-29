using System.Collections.Generic;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoCharacterSurfaceTests
    {
        private const string Path =
            "Assets/Art/Characters/Tripo/LittleMung_Casual_Male_41b6e942_Review/MasterRig 2/Model.prefab";

        [Test]
        public void HeadAndDressedBodyPartitionPreservesSourceAndAllTriangles()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = skin.sharedMesh;
            var vertices = original.vertices;
            var weights = original.boneWeights;
            var triangles = original.triangles;
            var uv = original.uv;
            var originals = new HashSet<(Vector3, Vector2, BoneWeight)>();
            for (int i = 0; i < vertices.Length; i++)
                originals.Add((vertices[i], uv[i], weights[i]));
            var head = TripoCharacterSurface.Extract(
                skin,
                model.GetComponentInChildren<Animator>(),
                TripoCharacterRegion.Head
            );
            var body = TripoCharacterSurface.Extract(
                skin,
                model.GetComponentInChildren<Animator>(),
                TripoCharacterRegion.DressedBody
            );
            try
            {
                Assert.That(head, Is.Not.Null);
                Assert.That(body, Is.Not.Null);
                Assert.That(head.triangles.Length, Is.GreaterThan(0));
                Assert.That(body.triangles.Length, Is.GreaterThan(0));
                Assert.That(
                    head.triangles.Length + body.triangles.Length,
                    Is.EqualTo(triangles.Length)
                );
                foreach (var part in new[] { head, body })
                {
                    Assert.That(part.bindposes, Is.EqualTo(original.bindposes));
                    var points = part.vertices;
                    var partWeights = part.boneWeights;
                    var partUv = part.uv;
                    for (int i = 0; i < points.Length; i++)
                    {
                        bool found = originals.Contains((points[i], partUv[i], partWeights[i]));
                        Assert.That(
                            found,
                            Is.True,
                            "Part must reuse a source vertex, UV and weight without resculpting."
                        );
                    }
                }
                Assert.That(original.vertices, Is.EqualTo(vertices));
                Assert.That(original.boneWeights, Is.EqualTo(weights));
                Assert.That(original.triangles, Is.EqualTo(triangles));
            }
            finally
            {
                if (head != null)
                    Object.DestroyImmediate(head);
                if (body != null)
                    Object.DestroyImmediate(body);
            }
        }
    }
}
