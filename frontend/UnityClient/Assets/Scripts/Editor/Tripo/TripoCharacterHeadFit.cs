using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>단일 스킨 머리의 크기를 맞추고 닫힌 목 절단선을 기준 바디 경계에 연결한다.</summary>
    public static class TripoCharacterHeadFit
    {
        public static Mesh Fit(SkinnedMeshRenderer head, SkinnedMeshRenderer reference)
        {
            var source = head.sharedMesh;
            var target = reference.sharedMesh;
            Matrix4x4 localToCommon = ToCommon(head),
                referenceToCommon = ToCommon(reference);
            var original = source.vertices.Select(localToCommon.MultiplyPoint3x4).ToArray();
            var referencePoints = target
                .vertices.Select(referenceToCommon.MultiplyPoint3x4)
                .ToArray();
            int[] upper = NeckRing(source, original),
                lower = NeckRing(target, referencePoints);
            var from = BoundsOf(original);
            var to = BoundsOf(referencePoints);
            var scale = new Vector3(
                to.size.x / from.size.x,
                to.size.y / from.size.y,
                to.size.z / from.size.z
            );
            var points = original
                .Select(p => to.center + Vector3.Scale(p - from.center, scale))
                .ToList();
            var oldRing = upper.Select(i => points[i]).ToArray();
            float neckLift = lower.Max(i => referencePoints[i].y) + .008f - oldRing.Min(p => p.y);
            // 연결부 주변만 위로 정리해, 서로 다른 높이의 두 절단선 사이에 뒤집힌 면이 생기지 않게 한다.
            var neckBlend = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 nearest = oldRing.OrderBy(p => (p - points[i]).sqrMagnitude).First();
                float blend =
                    1 - Mathf.SmoothStep(0, 1, Vector3.Distance(nearest, points[i]) / .12f);
                neckBlend[i] = blend;
                points[i] += Vector3.up * (neckLift * blend);
            }
            Vector3 center =
                lower.Aggregate(Vector3.zero, (sum, i) => sum + referencePoints[i]) / lower.Length;

            upper = StartAtAngle(upper, points.ToArray(), center);
            lower = StartAtAngle(lower, referencePoints, center);
            var weights = source.boneWeights.ToList();
            var uvChannels = new List<Vector4>[8];
            for (int c = 0; c < 8; c++)
            {
                uvChannels[c] = new List<Vector4>();
                source.GetUVs(c, uvChannels[c]);
            }
            // 목 연결 띠는 얼굴 텍스처의 서로 다른 UV 섬을 가로질러 보간하지 않는다.
            // 경계 정점은 위치·가중치가 같은 UV 전용 사본을 사용한다.
            int[] sourceUpper = upper.ToArray();
            int skinSample = upper.OrderByDescending(i => points[i].y).First();
            int bridgeTop = points.Count;
            foreach (int index in sourceUpper)
            {
                points.Add(points[index]);
                weights.Add(weights[index]);
                foreach (var channel in uvChannels)
                    if (channel.Count > 0)
                        channel.Add(channel[skinSample]);
            }
            upper = Enumerable.Range(bridgeTop, sourceUpper.Length).ToArray();
            int start = points.Count;
            var boneLookup = head
                .bones.Select((b, i) => (b.name, i))
                .ToDictionary(p => p.name, p => p.i);
            foreach (int index in lower)
            {
                Vector3 p = referencePoints[index];
                points.Add(p);
                var w = target.boneWeights[index];
                w.boneIndex0 = boneLookup[reference.bones[w.boneIndex0].name];
                w.boneIndex1 = boneLookup[reference.bones[w.boneIndex1].name];
                w.boneIndex2 = boneLookup[reference.bones[w.boneIndex2].name];
                w.boneIndex3 = boneLookup[reference.bones[w.boneIndex3].name];
                weights.Add(w);

                foreach (var channel in uvChannels)
                    if (channel.Count > 0)
                        channel.Add(channel[skinSample]);
            }
            var bridge = new List<int>();
            void Triangle(int a, int b, int c)
            {
                bridge.AddRange(new[] { a, c, b });
            }
            float[] upperDistance = Perimeter(upper, points.ToArray()),
                lowerDistance = Perimeter(lower, referencePoints);
            int u = 0,
                l = 0;
            while (u < upper.Length || l < lower.Length)
            {
                float nextUpper = u < upper.Length ? upperDistance[u + 1] : float.PositiveInfinity;
                float nextLower = l < lower.Length ? lowerDistance[l + 1] : float.PositiveInfinity;
                if (nextUpper < nextLower)
                {
                    Triangle(
                        upper[u % upper.Length],
                        start + l % lower.Length,
                        upper[(u + 1) % upper.Length]
                    );
                    u++;
                }
                else
                {
                    Triangle(
                        upper[u % upper.Length],
                        start + l % lower.Length,
                        start + (l + 1) % lower.Length
                    );
                    l++;
                }
            }
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_FittedNeck";
            mesh.ClearBlendShapes();
            mesh.vertices = points.Select(localToCommon.inverse.MultiplyPoint3x4).ToArray();
            mesh.boneWeights = weights.ToArray();
            for (int c = 0; c < 8; c++)
                if (uvChannels[c].Count > 0)
                    mesh.SetUVs(c, uvChannels[c]);
            // 연결 면에는 기존 목 경계와 같은 머티리얼을 사용한다.
            int material = Enumerable
                .Range(0, source.subMeshCount)
                .First(s => source.GetTriangles(s).Any(sourceUpper.Contains));
            mesh.SetTriangles(source.GetTriangles(material).Concat(bridge).ToArray(), material);
            mesh.RecalculateNormals();
            var normals = mesh.normals;
            var sourceNormals = source.normals;
            var referenceNormals = target.normals;
            Matrix4x4 normalTransform = (
                localToCommon.inverse * Matrix4x4.Scale(scale) * localToCommon
            )
                .inverse
                .transpose;
            for (int i = 0; i < source.vertexCount; i++)
                normals[i] = Vector3.Slerp(
                    normalTransform.MultiplyVector(sourceNormals[i]).normalized,
                    normals[i],
                    neckBlend[i]
                );
            for (int i = 0; i < sourceUpper.Length; i++)
                normals[bridgeTop + i] = normals[sourceUpper[i]];
            var refNormalTransform = (localToCommon.inverse * referenceToCommon).inverse.transpose;
            for (int i = 0; i < lower.Length; i++)
                normals[start + i] = refNormalTransform
                    .MultiplyVector(referenceNormals[lower[i]])
                    .normalized;
            mesh.normals = normals;
            mesh.RecalculateTangents();
            // 기존 표정의 정점 델타도 머리 크기 변환을 따른다. 바디 접합 정점은 움직이지 않는다.
            var delta = new Vector3[source.vertexCount];
            var normalDelta = new Vector3[source.vertexCount];
            var tangentDelta = new Vector3[source.vertexCount];
            var shapeTransform = localToCommon.inverse * Matrix4x4.Scale(scale) * localToCommon;
            for (int shape = 0; shape < source.blendShapeCount; shape++)
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, delta, normalDelta, tangentDelta);
                var fittedDelta = new Vector3[points.Count];
                var fittedNormals = new Vector3[points.Count];
                var fittedTangents = new Vector3[points.Count];
                for (int i = 0; i < source.vertexCount; i++)
                {
                    fittedDelta[i] = shapeTransform.MultiplyVector(delta[i]) * (1 - neckBlend[i]);
                    fittedNormals[i] =
                        normalTransform.MultiplyVector(normalDelta[i]) * (1 - neckBlend[i]);
                    fittedTangents[i] =
                        shapeTransform.MultiplyVector(tangentDelta[i]) * (1 - neckBlend[i]);
                }
                mesh.AddBlendShapeFrame(
                    source.GetBlendShapeName(shape),
                    source.GetBlendShapeFrameWeight(shape, frame),
                    fittedDelta,
                    fittedNormals,
                    fittedTangents
                );
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        public static string[] Inspect(
            GameObject headModel,
            GameObject bodyModel,
            GameObject referenceHead
        )
        {
            var errors = new List<string>();
            var heads = headModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bodies = bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            var references = referenceHead.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (heads.Length != 1 || bodies.Length != 1 || references.Length != 1)
                return new[]
                {
                    "목 경계 자동 검사는 머리·바디가 각각 단일 스킨인 경우에 지원합니다. 부품 구조를 확인하세요.",
                };
            var head = heads[0];
            var body = bodies[0];
            var reference = references[0];
            var points = head.sharedMesh.vertices.Select(ToCommon(head).MultiplyPoint3x4).ToArray();
            var bodyPoints = body
                .sharedMesh.vertices.Select(ToCommon(body).MultiplyPoint3x4)
                .ToArray();
            var referencePoints = reference
                .sharedMesh.vertices.Select(ToCommon(reference).MultiplyPoint3x4)
                .ToArray();
            Vector3 size = BoundsOf(points).size,
                expected = BoundsOf(referencePoints).size;
            if (
                Mathf.Abs(size.x / expected.x - 1) > .05f
                || Mathf.Abs(size.y / expected.y - 1) > .05f
                || Mathf.Abs(size.z / expected.z - 1) > .05f
            )
                errors.Add(
                    "머리 크기가 공통 기준에서 5% 이상 다릅니다. 얼굴·머리카락 비율을 확인하고 별도 사본에서 맞추세요."
                );
            try
            {
                var headRing = NeckRing(head.sharedMesh, points);
                var bodyRing = NeckRing(body.sharedMesh, bodyPoints);
                bool compatible = headRing.Length == bodyRing.Length;
                var headWeights = head.sharedMesh.boneWeights;
                var bodyWeights = body.sharedMesh.boneWeights;
                foreach (int index in bodyRing)
                {
                    int nearest = headRing
                        .OrderBy(i => (points[i] - bodyPoints[index]).sqrMagnitude)
                        .First();
                    if (Vector3.Distance(points[nearest], bodyPoints[index]) > .0001f)
                        compatible = false;
                    foreach (var bone in body.bones)
                        if (
                            Mathf.Abs(
                                Influence(headWeights[nearest], head, bone.name)
                                    - Influence(bodyWeights[index], body, bone.name)
                            ) > .0001f
                        )
                            compatible = false;
                }
                if (!compatible)
                    errors.Add(
                        "목 경계 위치 또는 본 가중치가 바디와 일치하지 않습니다. 연결 면을 맞추고 동작 중 벌어짐을 재검사하세요."
                    );
            }
            catch (InvalidOperationException exception)
            {
                errors.Add("목 경계: " + exception.Message);
            }
            return errors.ToArray();
        }

        private static float Influence(BoneWeight w, SkinnedMeshRenderer skin, string name) =>
            (skin.bones[w.boneIndex0].name == name ? w.weight0 : 0)
            + (skin.bones[w.boneIndex1].name == name ? w.weight1 : 0)
            + (skin.bones[w.boneIndex2].name == name ? w.weight2 : 0)
            + (skin.bones[w.boneIndex3].name == name ? w.weight3 : 0);

        private static int[] StartAtAngle(int[] ring, Vector3[] points, Vector3 center)
        {
            int start = Enumerable
                .Range(0, ring.Length)
                .OrderBy(i =>
                    Mathf.Atan2(points[ring[i]].z - center.z, points[ring[i]].x - center.x)
                )
                .First();
            return Enumerable
                .Range(0, ring.Length)
                .Select(i => ring[(i + start) % ring.Length])
                .ToArray();
        }

        private static float[] Perimeter(int[] ring, Vector3[] points)
        {
            var distance = new float[ring.Length + 1];
            for (int i = 0; i < ring.Length; i++)
                distance[i + 1] =
                    distance[i]
                    + Vector3.Distance(points[ring[i]], points[ring[(i + 1) % ring.Length]]);
            for (int i = 1; i < distance.Length; i++)
                distance[i] /= distance[ring.Length];
            return distance;
        }

        internal static Matrix4x4 ToCommon(SkinnedMeshRenderer skin) =>
            skin.transform.root.GetComponentInChildren<Animator>().transform.worldToLocalMatrix
            * skin.transform.localToWorldMatrix;

        internal static Bounds BoundsOf(Vector3[] points)
        {
            var result = new Bounds(points[0], Vector3.zero);
            foreach (var p in points)
                result.Encapsulate(p);
            return result;
        }

        internal static int[] NeckRing(Mesh mesh, Vector3[] points)
        {
            var representatives = new Dictionary<Vector3Int, int>();
            int[] welded = new int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var key = Vector3Int.RoundToInt(points[i] * 1000000);
                if (!representatives.TryGetValue(key, out int first))
                {
                    first = i;
                    representatives.Add(key, i);
                }
                welded[i] = first;
            }
            var edges = new Dictionary<(int, int), int>();
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            for (int c = 0; c < 3; c++)
            {
                int a = welded[triangles[i + c]],
                    b = welded[triangles[i + (c + 1) % 3]];
                if (a == b)
                    continue;
                var edge = a < b ? (a, b) : (b, a);
                edges.TryGetValue(edge, out int count);
                edges[edge] = count + 1;
            }
            var adjacent = new Dictionary<int, List<int>>();
            foreach (var e in edges.Where(e => e.Value == 1).Select(e => e.Key))
            {
                if (!adjacent.ContainsKey(e.Item1))
                    adjacent[e.Item1] = new List<int>();
                if (!adjacent.ContainsKey(e.Item2))
                    adjacent[e.Item2] = new List<int>();
                adjacent[e.Item1].Add(e.Item2);
                adjacent[e.Item2].Add(e.Item1);
            }
            var remaining = new HashSet<int>(adjacent.Keys);
            var loops = new List<int[]>();
            while (remaining.Count > 0)
            {
                var pending = new Stack<int>();
                pending.Push(remaining.First());
                var group = new List<int>();
                while (pending.Count > 0)
                {
                    int i = pending.Pop();
                    if (!remaining.Remove(i))
                        continue;
                    group.Add(i);
                    foreach (int next in adjacent[i])
                        pending.Push(next);
                }
                if (group.Count >= 8 && group.All(i => adjacent[i].Count == 2))
                    loops.Add(group.ToArray());
            }
            if (loops.Count != 1)
                throw new InvalidOperationException(
                    "단일 닫힌 목 경계를 찾을 수 없습니다. 자동 연결 대신 경계를 직접 확인하세요."
                );
            var groupLoop = loops[0];
            var ordered = new List<int>();
            int previous = -1,
                current = groupLoop[0];
            do
            {
                ordered.Add(current);
                int next = adjacent[current].First(i => i != previous);
                previous = current;
                current = next;
            } while (current != ordered[0]);
            float area = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 a = points[ordered[i]],
                    b = points[ordered[(i + 1) % ordered.Count]];
                area += a.x * b.z - b.x * a.z;
            }
            if (area < 0)
                ordered.Reverse();
            return ordered.ToArray();
        }
    }
}
