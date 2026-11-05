using System;
using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class TripoHandTemplateTests
    {
        [Test]
        public void GeneratedPoliceNeedsMasterHandSurfaceBeforeApproval()
        {
            const string folder = "Assets/__PoliceHandTemplateTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/Tripo/NPC/LittleMung_Police_Male/LittleMung_Police_Male.fbx"
            );
            Mesh copy = null;
            try
            {
                var prepared = TripoMasterRig.Build(source, master, folder);
                var skin = prepared.GetComponentInChildren<SkinnedMeshRenderer>();
                copy = Object.Instantiate(skin.sharedMesh);
                Assert.That(
                    TripoHandTemplateAudit.Inspect(master, skin, copy).Errors,
                    Is.Not.Empty
                );
                TripoHandTemplate.Apply(master, skin, copy);
                Assert.That(TripoHandTemplateAudit.Inspect(master, skin, copy).Errors, Is.Empty);
            }
            finally
            {
                if (copy != null)
                    Object.DestroyImmediate(copy);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void PoliceCopyUsesMasterFingerSurfaceAndKeepsFaceChannels()
        {
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var donor = master.GetComponentInChildren<SkinnedMeshRenderer>();
            var source = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Police_Male.prefab"
                )
            );
            var skin = source
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var mesh = Object.Instantiate(skin.sharedMesh);
            try
            {
                string[] shapes = Enumerable
                    .Range(0, mesh.blendShapeCount)
                    .Select(mesh.GetBlendShapeName)
                    .ToArray();
                var face = mesh.vertices;
                TripoHandTemplate.Apply(master, skin, mesh);
                Assert.That(
                    mesh.triangles.Distinct().Count(),
                    Is.EqualTo(mesh.vertexCount),
                    "삭제한 손 정점이 접지·관통 검사와 bounds에 남으면 안 됩니다."
                );
                var used = mesh.triangles.Distinct().Select(i => mesh.vertices[i]).ToArray();
                int hip = Array.FindIndex(skin.bones, b => b.name == "Hips");
                int donorHip = Array.FindIndex(donor.bones, b => b.name == "Hips");
                var alignment = mesh.bindposes[hip].inverse * donor.sharedMesh.bindposes[donorHip];
                var weights = donor.sharedMesh.boneWeights;
                var points = donor.sharedMesh.vertices;
                int checkedPoints = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    string bone = donor.bones[weights[i].boneIndex0].name;
                    if (!bone.Contains("Distal") || weights[i].weight0 < .75f)
                        continue;
                    var expected = alignment.MultiplyPoint3x4(points[i]);
                    Assert.That(
                        used.Min(p => Vector3.Distance(p, expected)),
                        Is.LessThan(.0001f),
                        bone + ": the rendered fingertips must use the validated master surface."
                    );
                    checkedPoints++;
                }
                Assert.That(checkedPoints, Is.GreaterThan(10));
                Assert.That(
                    Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName),
                    Is.EqualTo(shapes)
                );
                int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                var originalWeights = skin.sharedMesh.boneWeights;
                foreach (int i in skin.sharedMesh.triangles.Distinct())
                    if (originalWeights[i].boneIndex0 == head)
                        Assert.That(
                            used.Min(p => Vector3.Distance(p, face[i])),
                            Is.LessThan(1e-7f)
                        );
                AssertClosedWrists(skin, mesh);
                AssertFaceFrames(skin, mesh);
                Assert.That(TripoHandTemplateAudit.Inspect(master, skin, mesh).Errors, Is.Empty);
                var stablePoints = mesh.vertices;
                var stableFaces = mesh.triangles;
                TripoHandTemplate.Apply(master, skin, mesh);
                Assert.That(
                    mesh.vertices,
                    Is.EqualTo(stablePoints),
                    "재실행 시 이미 연결된 손을 다시 변형하면 안 됩니다."
                );
                Assert.That(mesh.triangles, Is.EqualTo(stableFaces));
                var damaged = mesh.vertices;
                int fingertip = Enumerable
                    .Range(0, mesh.vertexCount)
                    .First(i =>
                        skin.bones[mesh.boneWeights[i].boneIndex0].name.Contains("Distal")
                        && mesh.boneWeights[i].weight0 > .75f
                    );
                damaged[fingertip] += Vector3.up * .01f;
                mesh.vertices = damaged;
                Assert.That(
                    TripoHandTemplateAudit.Inspect(master, skin, mesh).Errors,
                    Is.Not.Empty,
                    "손 형태가 마스터와 달라지면 최종 적용을 차단해야 합니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(source);
            }
        }

        private static void AssertFaceFrames(SkinnedMeshRenderer skin, Mesh repaired)
        {
            var original = skin.sharedMesh;
            var before = original.vertices;
            var uv = original.uv;
            var after = repaired.vertices;
            var afterUv = repaired.uv;
            var index = Enumerable
                .Range(0, after.Length)
                .GroupBy(i => (after[i], afterUv[i]))
                .ToDictionary(g => g.Key, g => g.First());
            var weights = original.boneWeights;
            int head = Array.FindIndex(skin.bones, b => b.name == "Head");
            var face = original
                .triangles.Distinct()
                .Where(i => weights[i].boneIndex0 == head)
                .ToArray();
            Assert.That(face.Length, Is.GreaterThan(100));
            for (int shape = 0; shape < original.blendShapeCount; shape++)
            for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
            {
                var oldDelta = new Vector3[before.Length];
                var oldNormals = new Vector3[before.Length];
                var oldTangents = new Vector3[before.Length];
                var newDelta = new Vector3[after.Length];
                var newNormals = new Vector3[after.Length];
                var newTangents = new Vector3[after.Length];
                original.GetBlendShapeFrameVertices(
                    shape,
                    frame,
                    oldDelta,
                    oldNormals,
                    oldTangents
                );
                repaired.GetBlendShapeFrameVertices(
                    shape,
                    frame,
                    newDelta,
                    newNormals,
                    newTangents
                );
                foreach (int i in face)
                {
                    int j = index[(before[i], uv[i])];
                    Assert.That(newDelta[j], Is.EqualTo(oldDelta[i]));
                    Assert.That(newNormals[j], Is.EqualTo(oldNormals[i]));
                    Assert.That(newTangents[j], Is.EqualTo(oldTangents[i]));
                }
            }
        }

        private static void AssertClosedWrists(SkinnedMeshRenderer skin, Mesh mesh)
        {
            var points = mesh.vertices;
            var weights = mesh.boneWeights;
            var edges = new System.Collections.Generic.Dictionary<
                string,
                (int count, int a, int b)
            >();
            string Key(Vector3 p) =>
                $"{Mathf.RoundToInt(p.x * 100000)},{Mathf.RoundToInt(p.y * 100000)},{Mathf.RoundToInt(p.z * 100000)}";
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = triangles[i + e],
                    b = triangles[i + (e + 1) % 3];
                string ka = Key(points[a]),
                    kb = Key(points[b]);
                if (ka == kb)
                    continue;
                string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "/" + kb : kb + "/" + ka;
                edges.TryGetValue(key, out var edge);
                edges[key] = (edge.count + 1, a, b);
            }
            foreach (string side in new[] { "Left", "Right" })
            {
                int lower = Array.FindIndex(skin.bones, b => b.name == side + "_LowerArm");
                int hand = Array.FindIndex(skin.bones, b => b.name == side + "_Hand");
                var elbow = mesh.bindposes[lower].inverse.MultiplyPoint3x4(Vector3.zero);
                var axis = mesh.bindposes[hand].inverse.MultiplyPoint3x4(Vector3.zero) - elbow;
                foreach (var edge in edges.Values)
                {
                    var center = (points[edge.a] + points[edge.b]) * .5f;
                    float along = Vector3.Dot(center - elbow, axis) / axis.sqrMagnitude;
                    if (
                        along < .4f
                        || along > 1.65f
                        || !skin.bones[weights[edge.a].boneIndex0].IsChildOf(skin.bones[lower])
                    )
                        continue;
                    Assert.That(
                        edge.count,
                        Is.EqualTo(2),
                        side + ": 손목 연결면에 열린 경계가 있습니다: " + center
                    );
                }
            }
        }
    }
}
