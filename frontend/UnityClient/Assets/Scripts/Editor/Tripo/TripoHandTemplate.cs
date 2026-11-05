using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoHandTemplate
    {
        private sealed class Geometry
        {
            public readonly List<Vector3> Points = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uv = new();
            public readonly List<BoneWeight> Weights = new();

            public int Add(Vector3 point, Vector3 normal, Vector2 uv, BoneWeight weight)
            {
                int index = Points.Count;
                Points.Add(point);
                Normals.Add(normal);
                Uv.Add(uv);
                Weights.Add(weight);
                return index;
            }
        }

        public static void Apply(GameObject master, SkinnedMeshRenderer skin, Mesh mesh)
        {
            if (TripoHandTemplateAudit.Inspect(master, skin, mesh).Passed)
                return;
            var donor = master
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var reference = donor.sharedMesh;
            int hip = Array.FindIndex(skin.bones, b => b.name == "Hips");
            int donorHip = Array.FindIndex(donor.bones, b => b.name == "Hips");
            var alignment = mesh.bindposes[hip].inverse * reference.bindposes[donorHip];
            var normalAlignment = alignment.inverse.transpose;
            var donorPoints = reference.vertices.Select(alignment.MultiplyPoint3x4).ToArray();
            var donorNormals = reference
                .normals.Select(n => normalAlignment.MultiplyVector(n).normalized)
                .ToArray();
            var remap = donor
                .bones.Select(b => Array.FindIndex(skin.bones, t => t.name == b.name))
                .ToArray();
            if (remap.Any(i => i < 0))
                throw new InvalidOperationException("마스터 손의 본 대응이 없습니다.");
            var donorWeights = reference
                .boneWeights.Select(w => new BoneWeight
                {
                    boneIndex0 = remap[w.boneIndex0],
                    weight0 = w.weight0,
                    boneIndex1 = remap[w.boneIndex1],
                    weight1 = w.weight1,
                    boneIndex2 = remap[w.boneIndex2],
                    weight2 = w.weight2,
                    boneIndex3 = remap[w.boneIndex3],
                    weight3 = w.weight3,
                })
                .ToArray();
            var originalPoints = mesh.vertices;
            var originalNormals = mesh.normals;
            var originalUv = mesh.uv;
            var originalWeights = mesh.boneWeights;
            var geometry = new Geometry();
            for (int i = 0; i < originalPoints.Length; i++)
                geometry.Add(
                    originalPoints[i],
                    originalNormals[i],
                    originalUv[i],
                    originalWeights[i]
                );
            var originalTriangles = mesh.triangles;
            var donorTriangles = reference.triangles;
            var remove = new HashSet<int>();
            var added = new List<int>();
            var seam = new HashSet<int>();
            Vector2? commonSkinUv = null;
            foreach (string side in new[] { "Left", "Right" })
            {
                int lower = Array.FindIndex(skin.bones, b => b.name == side + "_LowerArm");
                int wrist = Array.FindIndex(skin.bones, b => b.name == side + "_Hand");
                var lowerBone = skin.bones[lower];
                var group = skin.bones.Select(b => b.IsChildOf(lowerBone)).ToArray();
                Vector3 elbow = mesh.bindposes[lower].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 hand = mesh.bindposes[wrist].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 axis = hand - elbow;
                if (axis.sqrMagnitude < 1e-10f)
                    throw new InvalidOperationException("전완 길이가 0입니다.");
                bool[] Region(Vector3[] points, BoneWeight[] weights, int[] triangles, float cut)
                {
                    float Influence(int index)
                    {
                        var w = weights[index];
                        return (group[w.boneIndex0] ? w.weight0 : 0)
                            + (group[w.boneIndex1] ? w.weight1 : 0)
                            + (group[w.boneIndex2] ? w.weight2 : 0)
                            + (group[w.boneIndex3] ? w.weight3 : 0);
                    }
                    var selected = new bool[triangles.Length / 3];
                    for (int i = 0; i < selected.Length; i++)
                    {
                        int a = triangles[i * 3],
                            b = triangles[i * 3 + 1],
                            c = triangles[i * 3 + 2];
                        Vector3 center = (points[a] + points[b] + points[c]) / 3f;
                        selected[i] =
                            (Influence(a) + Influence(b) + Influence(c)) / 3f > .5f
                            && Vector3.Dot(center - elbow, axis) / axis.sqrMagnitude > cut;
                    }
                    return selected;
                }
                var oldRegion = Region(originalPoints, originalWeights, originalTriangles, .35f);
                var newRegion = Region(donorPoints, donorWeights, donorTriangles, 1.35f);
                for (int i = 0; i < oldRegion.Length; i++)
                    if (oldRegion[i])
                        remove.Add(i);
                var oldRing = Boundary(originalPoints, originalTriangles, oldRegion, false);
                var donorRing = Boundary(donorPoints, donorTriangles, newRegion, true);
                var sourceSkinPoints = Enumerable
                    .Range(0, oldRegion.Length)
                    .Where(i => oldRegion[i])
                    .SelectMany(i => originalTriangles.Skip(i * 3).Take(3))
                    .Distinct()
                    .ToArray();
                int sample = sourceSkinPoints
                    .Where(i =>
                        skin.bones[originalWeights[i].boneIndex0].IsChildOf(skin.bones[wrist])
                        && originalWeights[i].weight0 > .75f
                    )
                    .OrderBy(i => (originalPoints[i] - hand).sqrMagnitude)
                    .First();
                commonSkinUv ??= originalUv[sample];
                Vector2 skinUv = commonSkinUv.Value;
                var copied = new Dictionary<int, int>();
                int Copy(int i)
                {
                    if (copied.TryGetValue(i, out int result))
                        return result;
                    result = geometry.Add(donorPoints[i], donorNormals[i], skinUv, donorWeights[i]);
                    copied.Add(i, result);
                    return result;
                }
                for (int i = 0; i < newRegion.Length; i++)
                    if (newRegion[i])
                    {
                        added.Add(Copy(donorTriangles[i * 3]));
                        added.Add(Copy(donorTriangles[i * 3 + 1]));
                        added.Add(Copy(donorTriangles[i * 3 + 2]));
                    }
                var newRing = donorRing.Select(Copy).ToArray();
                // 원래 UV 섬을 새 손으로 보간하면 옷 무늬가 손 위에 나타난다.
                // 경계 위치와 가중치는 공유하고 새 표면은 피부 표본을 사용한다.
                var bridgeRing = oldRing
                    .Select(i =>
                        geometry.Add(
                            originalPoints[i],
                            originalNormals[i],
                            skinUv,
                            originalWeights[i]
                        )
                    )
                    .ToArray();
                foreach (int i in oldRing.Concat(bridgeRing).Concat(newRing))
                    seam.Add(i);
                Bridge(geometry, bridgeRing, newRing, axis.normalized, hand, added, seam);
                for (int i = 0; i < geometry.Points.Count; i++)
                {
                    float along = Vector3.Dot(geometry.Points[i] - elbow, axis) / axis.sqrMagnitude;
                    if (along <= .15f || along >= 1.35f)
                        continue;
                    var w = geometry.Weights[i];
                    if (
                        !(group[w.boneIndex0] && w.weight0 > 0)
                        && !(group[w.boneIndex1] && w.weight1 > 0)
                        && !(group[w.boneIndex2] && w.weight2 > 0)
                        && !(group[w.boneIndex3] && w.weight3 > 0)
                    )
                        continue;
                    var fixedWeight = ForearmWeight(
                        w,
                        group,
                        lower,
                        wrist,
                        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.75f, 1.2f, along))
                    );
                    geometry.Weights[i] = MixWeights(
                        fixedWeight,
                        w,
                        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.05f, 1.35f, along))
                    );
                }
            }
            var shapes =
                new List<(
                    string name,
                    float weight,
                    Vector3[] vertices,
                    Vector3[] normals,
                    Vector3[] tangents
                )>();
            var removedVertices = remove
                .SelectMany(i => originalTriangles.Skip(i * 3).Take(3))
                .ToHashSet();
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
            {
                var vertices = new Vector3[mesh.vertexCount];
                var normals = new Vector3[mesh.vertexCount];
                var tangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(shape, frame, vertices, normals, tangents);
                if (removedVertices.Any(i => vertices[i].sqrMagnitude > 1e-14f))
                    throw new InvalidOperationException(
                        "교체할 손 영역에 별도 Blend Shape가 있어 자동 교체를 중단합니다."
                    );
                Array.Resize(ref vertices, geometry.Points.Count);
                Array.Resize(ref normals, geometry.Points.Count);
                Array.Resize(ref tangents, geometry.Points.Count);
                shapes.Add(
                    (
                        mesh.GetBlendShapeName(shape),
                        mesh.GetBlendShapeFrameWeight(shape, frame),
                        vertices,
                        normals,
                        tangents
                    )
                );
            }
            var submeshes = new List<int[]>();
            int triangleIndex = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                var kept = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3, triangleIndex++)
                    if (!remove.Contains(triangleIndex))
                        kept.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                if (sub == 0)
                    kept.AddRange(added);
                submeshes.Add(kept.ToArray());
            }
            // 보이지 않는 옛 손 정점도 제거해야 런타임 관통 보정과 bounds가 실제 표면을 측정한다.
            int[] used = submeshes.SelectMany(t => t).Distinct().OrderBy(i => i).ToArray();
            var compact = used.Select((old, index) => (old, index))
                .ToDictionary(p => p.old, p => p.index);
            mesh.ClearBlendShapes();
            for (int sub = 0; sub < submeshes.Count; sub++)
                mesh.SetTriangles(Array.Empty<int>(), sub);
            mesh.vertices = used.Select(i => geometry.Points[i]).ToArray();
            mesh.normals = used.Select(i => geometry.Normals[i]).ToArray();
            mesh.uv = used.Select(i => geometry.Uv[i]).ToArray();
            mesh.boneWeights = used.Select(i => geometry.Weights[i]).ToArray();
            mesh.tangents = new Vector4[used.Length];
            for (int sub = 0; sub < submeshes.Count; sub++)
                mesh.SetTriangles(submeshes[sub].Select(i => compact[i]).ToArray(), sub);
            foreach (var shape in shapes)
                mesh.AddBlendShapeFrame(
                    shape.name,
                    shape.weight,
                    used.Select(i => shape.vertices[i]).ToArray(),
                    used.Select(i => shape.normals[i]).ToArray(),
                    used.Select(i => shape.tangents[i]).ToArray()
                );
            var changed = new Matrix4x4?[mesh.vertexCount];
            foreach (int i in seam)
                if (compact.TryGetValue(i, out int index))
                    changed[index] = Matrix4x4.identity;
            TripoHandTransfer.RefreshNormals(mesh, changed);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        private static Vector3Int Key(Vector3 p) =>
            new Vector3Int(
                Mathf.RoundToInt(p.x * 100000),
                Mathf.RoundToInt(p.y * 100000),
                Mathf.RoundToInt(p.z * 100000)
            );

        private static int[] Boundary(
            Vector3[] points,
            int[] triangles,
            bool[] selected,
            bool keepSelected
        )
        {
            var edges = new Dictionary<(Vector3Int, Vector3Int), (int a, int b, int flags)>();
            bool Before(Vector3Int a, Vector3Int b) =>
                a.x != b.x ? a.x < b.x
                : a.y != b.y ? a.y < b.y
                : a.z < b.z;
            for (int i = 0; i < triangles.Length; i += 3)
            for (int edge = 0; edge < 3; edge++)
            {
                int a = triangles[i + edge],
                    b = triangles[i + (edge + 1) % 3];
                var ka = Key(points[a]);
                var kb = Key(points[b]);
                if (ka == kb)
                    continue;
                if (!Before(ka, kb))
                {
                    (ka, kb) = (kb, ka);
                    (a, b) = (b, a);
                }
                var key = (ka, kb);
                edges.TryGetValue(key, out var value);
                bool kept = selected[i / 3] == keepSelected;
                if (value.flags == 0 || kept)
                {
                    value.a = a;
                    value.b = b;
                }
                value.flags |= kept ? 1 : 2;
                edges[key] = value;
            }
            var boundary = edges.Values.Where(e => e.flags == 3).ToArray();
            var neighbors = new Dictionary<Vector3Int, List<Vector3Int>>();
            var indices = new Dictionary<Vector3Int, int>();
            void Connect(int a, int b)
            {
                var ka = Key(points[a]);
                var kb = Key(points[b]);
                if (!neighbors.TryGetValue(ka, out var list))
                    neighbors[ka] = list = new List<Vector3Int>();
                if (!list.Contains(kb))
                    list.Add(kb);
                indices[ka] = a;
            }
            foreach (var edge in boundary)
            {
                Connect(edge.a, edge.b);
                Connect(edge.b, edge.a);
            }
            if (neighbors.Count < 3 || neighbors.Values.Any(n => n.Count != 2))
                throw new InvalidOperationException("손목 연결 경계가 단일 폐곡선이 아닙니다.");
            var visited = new HashSet<Vector3Int>();
            var start = neighbors.Keys.First();
            var current = start;
            Vector3Int? previous = null;
            var ring = new List<int>();
            while (visited.Add(current))
            {
                ring.Add(indices[current]);
                var next = neighbors[current].First(n => !previous.HasValue || n != previous.Value);
                if (next == start)
                    break;
                previous = current;
                current = next;
            }
            if (ring.Count != neighbors.Count)
                throw new InvalidOperationException(
                    "분리된 손목 경계가 있어 자동 연결을 중단합니다."
                );
            return ring.ToArray();
        }

        private static void Bridge(
            Geometry geometry,
            int[] source,
            int[] target,
            Vector3 axis,
            Vector3 wrist,
            List<int> triangles,
            HashSet<int> seam
        )
        {
            Vector3 centerA =
                source.Aggregate(Vector3.zero, (s, i) => s + geometry.Points[i]) / source.Length;
            Vector3 centerB =
                target.Aggregate(Vector3.zero, (s, i) => s + geometry.Points[i]) / target.Length;
            Vector3 across = Vector3.Cross(axis, Vector3.up).normalized;
            if (across.sqrMagnitude < .1f)
                across = Vector3.Cross(axis, Vector3.right).normalized;
            Vector3 depth = Vector3.Cross(axis, across);
            Vector3 span = centerB - centerA;
            float wristTime = Mathf.Clamp01(Vector3.Dot(wrist - centerA, span) / span.sqrMagnitude);
            var previous = source;
            for (int step = 1; step <= 7; step++)
            {
                float t = step / 8f;
                var ring = new int[16];
                for (int j = 0; j < ring.Length; j++)
                {
                    float angle = j * Mathf.PI * 2 / ring.Length;
                    var radial = across * Mathf.Cos(angle) + depth * Mathf.Sin(angle);
                    int a = source
                        .OrderByDescending(i =>
                            Vector3.Dot((geometry.Points[i] - centerA).normalized, radial)
                        )
                        .First();
                    int b = target
                        .OrderByDescending(i =>
                            Vector3.Dot((geometry.Points[i] - centerB).normalized, radial)
                        )
                        .First();
                    float radiusA = Vector3
                        .ProjectOnPlane(geometry.Points[a] - centerA, axis)
                        .magnitude;
                    float radiusB = Vector3
                        .ProjectOnPlane(geometry.Points[b] - centerB, axis)
                        .magnitude;
                    var weight = MixWeights(geometry.Weights[a], geometry.Weights[b], t);
                    float taper =
                        1
                        - .38f
                            * Mathf.Sin(Mathf.PI * t)
                            * Mathf.Exp(-Mathf.Pow((t - wristTime) / .28f, 2));
                    ring[j] = geometry.Add(
                        Vector3.Lerp(centerA, centerB, t)
                            + radial * Mathf.Lerp(radiusA, radiusB, t) * taper,
                        radial,
                        geometry.Uv[a],
                        weight
                    );
                    seam.Add(ring[j]);
                }
                Stitch(geometry, previous, ring, axis, triangles);
                previous = ring;
            }
            Stitch(geometry, previous, target, axis, triangles);
        }

        private static BoneWeight MixWeights(BoneWeight a, BoneWeight b, float t)
        {
            var weights = new Dictionary<int, float>();
            void Add(BoneWeight w, float amount)
            {
                void Slot(int bone, float value)
                {
                    weights.TryGetValue(bone, out float total);
                    weights[bone] = total + value * amount;
                }
                Slot(w.boneIndex0, w.weight0);
                Slot(w.boneIndex1, w.weight1);
                Slot(w.boneIndex2, w.weight2);
                Slot(w.boneIndex3, w.weight3);
            }
            Add(a, 1 - t);
            Add(b, t);
            return PackWeights(weights);
        }

        private static BoneWeight ForearmWeight(
            BoneWeight original,
            bool[] armGroup,
            int forearm,
            int hand,
            float handWeight
        )
        {
            var weights = new Dictionary<int, float>();
            float amount = 0;
            void Add(int index, float weight)
            {
                if (armGroup[index])
                    amount += weight;
                else
                {
                    weights.TryGetValue(index, out float total);
                    weights[index] = total + weight;
                }
            }
            Add(original.boneIndex0, original.weight0);
            Add(original.boneIndex1, original.weight1);
            Add(original.boneIndex2, original.weight2);
            Add(original.boneIndex3, original.weight3);
            weights[forearm] = amount * (1 - handWeight);
            weights[hand] = amount * handWeight;
            return PackWeights(weights);
        }

        private static BoneWeight PackWeights(Dictionary<int, float> weights)
        {
            var sorted = weights.OrderByDescending(p => p.Value).Take(4).ToArray();
            float sum = sorted.Sum(p => p.Value);
            var result = new BoneWeight();
            if (sorted.Length > 0)
            {
                result.boneIndex0 = sorted[0].Key;
                result.weight0 = sorted[0].Value / sum;
            }
            if (sorted.Length > 1)
            {
                result.boneIndex1 = sorted[1].Key;
                result.weight1 = sorted[1].Value / sum;
            }
            if (sorted.Length > 2)
            {
                result.boneIndex2 = sorted[2].Key;
                result.weight2 = sorted[2].Value / sum;
            }
            if (sorted.Length > 3)
            {
                result.boneIndex3 = sorted[3].Key;
                result.weight3 = sorted[3].Value / sum;
            }
            return result;
        }

        private static void Stitch(
            Geometry geometry,
            int[] source,
            int[] target,
            Vector3 axis,
            List<int> triangles
        )
        {
            Vector3 centerA =
                source.Aggregate(Vector3.zero, (s, i) => s + geometry.Points[i]) / source.Length;
            Vector3 centerB =
                target.Aggregate(Vector3.zero, (s, i) => s + geometry.Points[i]) / target.Length;
            Vector3 across = Vector3.Cross(axis, Vector3.up).normalized;
            if (across.sqrMagnitude < .1f)
                across = Vector3.Cross(axis, Vector3.right).normalized;
            Vector3 depth = Vector3.Cross(axis, across);
            float Angle(int i, Vector3 center)
            {
                var p = geometry.Points[i] - center;
                return Mathf.Atan2(Vector3.Dot(p, depth), Vector3.Dot(p, across));
            }
            int[] Orient(int[] ring, Vector3 center)
            {
                Vector3 area = Vector3.zero;
                for (int i = 0; i < ring.Length; i++)
                    area += Vector3.Cross(
                        geometry.Points[ring[i]] - center,
                        geometry.Points[ring[(i + 1) % ring.Length]] - center
                    );
                if (Vector3.Dot(area, axis) < 0)
                    ring = ring.Reverse().ToArray();
                int start = Enumerable
                    .Range(0, ring.Length)
                    .OrderBy(i => Angle(ring[i], center))
                    .First();
                return Enumerable
                    .Range(0, ring.Length)
                    .Select(i => ring[(start + i) % ring.Length])
                    .ToArray();
            }
            float[] Progress(int[] ring)
            {
                var progress = new float[ring.Length + 1];
                for (int i = 0; i < ring.Length; i++)
                    progress[i + 1] =
                        progress[i]
                        + Vector3.Distance(
                            geometry.Points[ring[i]],
                            geometry.Points[ring[(i + 1) % ring.Length]]
                        );
                float perimeter = progress[ring.Length];
                for (int i = 0; i < progress.Length; i++)
                    progress[i] /= perimeter;
                return progress;
            }
            source = Orient(source, centerA);
            target = Orient(target, centerB);
            var sourceProgress = Progress(source);
            var targetProgress = Progress(target);
            void Add(int a, int b, int c)
            {
                triangles.AddRange(new[] { a, b, c });
            }
            int first = 0,
                second = 0;
            while (first < source.Length || second < target.Length)
            {
                float nextA =
                    first < source.Length ? sourceProgress[first + 1] : float.PositiveInfinity;
                float nextB =
                    second < target.Length ? targetProgress[second + 1] : float.PositiveInfinity;
                if (first < source.Length && (second == target.Length || nextA <= nextB))
                {
                    Add(
                        source[first % source.Length],
                        source[(first + 1) % source.Length],
                        target[second % target.Length]
                    );
                    first++;
                }
                else
                {
                    Add(
                        source[first % source.Length],
                        target[(second + 1) % target.Length],
                        target[second % target.Length]
                    );
                    second++;
                }
            }
        }
    }
}
