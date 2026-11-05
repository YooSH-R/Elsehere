using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoHandTemplateAudit
    {
        public static TripoAuditReport Inspect(
            GameObject master,
            SkinnedMeshRenderer skin,
            Mesh mesh
        )
        {
            var report = new TripoAuditReport();
            var donor = master
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var reference = donor.sharedMesh;
            int hip = Array.FindIndex(skin.bones, b => b.name == "Hips");
            int donorHip = Array.FindIndex(donor.bones, b => b.name == "Hips");
            var alignment = mesh.bindposes[hip].inverse * reference.bindposes[donorHip];
            var expected = reference.vertices.Select(alignment.MultiplyPoint3x4).ToArray();
            var actual = mesh.vertices;
            var weights = mesh.boneWeights;
            var sourceWeights = reference.boneWeights;
            var used = mesh.triangles.Distinct().ToArray();
            if (used.Length != mesh.vertexCount)
                report.Errors.Add("교체 전 손 정점이 사용되지 않는 상태로 남아 있습니다.");
            foreach (string side in new[] { "Left", "Right" })
            {
                int lower = Array.FindIndex(skin.bones, b => b.name == side + "_LowerArm");
                int hand = Array.FindIndex(skin.bones, b => b.name == side + "_Hand");
                int donorHand = Array.FindIndex(donor.bones, b => b.name == side + "_Hand");
                var elbow = mesh.bindposes[lower].inverse.MultiplyPoint3x4(Vector3.zero);
                var axis = mesh.bindposes[hand].inverse.MultiplyPoint3x4(Vector3.zero) - elbow;
                bool Selected(
                    Vector3 point,
                    BoneWeight weight,
                    Transform[] bones,
                    Transform wrist
                ) =>
                    bones[weight.boneIndex0].IsChildOf(wrist)
                    && weight.weight0 > .75f
                    && Vector3.Dot(point - elbow, axis) / axis.sqrMagnitude > 1.65f;
                var referencePoints = Enumerable
                    .Range(0, expected.Length)
                    .Where(i =>
                        Selected(expected[i], sourceWeights[i], donor.bones, donor.bones[donorHand])
                    )
                    .ToArray();
                var targetPoints = used.Where(i =>
                        Selected(actual[i], weights[i], skin.bones, skin.bones[hand])
                    )
                    .ToArray();
                if (referencePoints.Length < 10 || targetPoints.Length < 10)
                {
                    report.Errors.Add(side + " 마스터 손 표면이 충분하지 않습니다.");
                    continue;
                }
                bool Same(int a, int b) =>
                    Vector3.Distance(actual[a], expected[b]) < .0001f
                    && WeightDifference(weights[a], skin.bones, sourceWeights[b], donor.bones)
                        < .0001f;
                if (
                    referencePoints.Any(b => !targetPoints.Any(a => Same(a, b)))
                    || targetPoints.Any(a => !referencePoints.Any(b => Same(a, b)))
                )
                    report.Errors.Add(side + " 마스터 손가락 형태 또는 관절 가중치가 다릅니다.");
                CheckSeam(skin, mesh, lower, elbow, axis, report);
            }
            return report;
        }

        private static float WeightDifference(
            BoneWeight a,
            Transform[] aBones,
            BoneWeight b,
            Transform[] bBones
        )
        {
            var weights = new Dictionary<string, float>();
            void Add(BoneWeight w, Transform[] bones, float sign)
            {
                void Slot(int i, float value)
                {
                    string name = bones[i].name;
                    weights.TryGetValue(name, out float total);
                    weights[name] = total + value * sign;
                }
                Slot(w.boneIndex0, w.weight0);
                Slot(w.boneIndex1, w.weight1);
                Slot(w.boneIndex2, w.weight2);
                Slot(w.boneIndex3, w.weight3);
            }
            Add(a, aBones, 1);
            Add(b, bBones, -1);
            return weights.Values.Sum(Mathf.Abs);
        }

        private static void CheckSeam(
            SkinnedMeshRenderer skin,
            Mesh mesh,
            int lower,
            Vector3 elbow,
            Vector3 axis,
            TripoAuditReport report
        )
        {
            var points = mesh.vertices;
            var weights = mesh.boneWeights;
            var edges =
                new Dictionary<
                    (Vector3Int, Vector3Int),
                    (int count, int direction, int a, int b)
                >();
            Vector3Int Key(Vector3 p) =>
                new Vector3Int(
                    Mathf.RoundToInt(p.x * 100000),
                    Mathf.RoundToInt(p.y * 100000),
                    Mathf.RoundToInt(p.z * 100000)
                );
            bool Before(Vector3Int a, Vector3Int b) =>
                a.x != b.x ? a.x < b.x
                : a.y != b.y ? a.y < b.y
                : a.z < b.z;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = triangles[i + e],
                    b = triangles[i + (e + 1) % 3];
                var ka = Key(points[a]);
                var kb = Key(points[b]);
                if (ka == kb)
                    continue;
                bool forward = Before(ka, kb);
                var key = forward ? (ka, kb) : (kb, ka);
                edges.TryGetValue(key, out var value);
                edges[key] = (value.count + 1, value.direction + (forward ? 1 : -1), a, b);
            }
            foreach (var edge in edges.Values)
            {
                float along =
                    Vector3.Dot((points[edge.a] + points[edge.b]) * .5f - elbow, axis)
                    / axis.sqrMagnitude;
                if (
                    along < .4f
                    || along > 1.65f
                    || !skin.bones[weights[edge.a].boneIndex0].IsChildOf(skin.bones[lower])
                )
                    continue;
                if (edge.count != 2 || edge.direction != 0)
                {
                    report.Errors.Add(
                        skin.bones[lower].name + " 손목 연결면이 열렸거나 뒤집혔습니다."
                    );
                    return;
                }
            }
            // UV 경계의 중복 정점도 같은 가중치를 가져야 움직일 때 벌어지지 않는다.
            foreach (
                var group in points.Select((p, i) => (key: Key(p), index: i)).GroupBy(p => p.key)
            )
            {
                var entries = group.Select(p => p.index).ToArray();
                int first = entries[0];
                float along = Vector3.Dot(points[first] - elbow, axis) / axis.sqrMagnitude;
                if (
                    along < .4f
                    || along > 1.65f
                    || !skin.bones[weights[first].boneIndex0].IsChildOf(skin.bones[lower])
                )
                    continue;
                if (
                    entries.Any(i =>
                        WeightDifference(weights[first], skin.bones, weights[i], skin.bones)
                        > .0001f
                    )
                )
                {
                    report.Errors.Add(
                        skin.bones[lower].name
                            + " 손목 경계의 관절 가중치가 달라 동작 중 틈이 생깁니다."
                    );
                    return;
                }
            }
        }
    }
}
