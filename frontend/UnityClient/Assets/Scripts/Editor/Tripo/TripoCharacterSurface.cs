using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public enum TripoCharacterRegion
    {
        Head,
        DressedBody,
    }

    /// <summary>기준 스킨의 면을 분류한다. 정점·가중치·UV를 새로 추정하지 않는다.</summary>
    public static class TripoCharacterSurface
    {
        public static Mesh Extract(
            SkinnedMeshRenderer skin,
            Animator animator,
            TripoCharacterRegion region
        )
        {
            var source = skin.sharedMesh;
            if (source == null || !source.isReadable)
                throw new InvalidOperationException("읽을 수 있는 스킨 메시가 필요합니다.");
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                throw new InvalidOperationException("머리 본이 없습니다.");
            var headBones = new HashSet<Transform>(head.GetComponentsInChildren<Transform>());
            var flags = skin.bones.Select(headBones.Contains).ToArray();
            var weights = source.boneWeights;
            float HeadWeight(int index)
            {
                var w = weights[index];
                return (flags[w.boneIndex0] ? w.weight0 : 0)
                    + (flags[w.boneIndex1] ? w.weight1 : 0)
                    + (flags[w.boneIndex2] ? w.weight2 : 0)
                    + (flags[w.boneIndex3] ? w.weight3 : 0);
            }
            var indices = new List<int>();
            var mapping = new Dictionary<int, int>();
            var triangles = new List<int>[source.subMeshCount];
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                triangles[submesh] = new List<int>();
                int[] input = source.GetTriangles(submesh);
                for (int i = 0; i < input.Length; i += 3)
                {
                    bool isHead =
                        (HeadWeight(input[i]) + HeadWeight(input[i + 1]) + HeadWeight(input[i + 2]))
                            / 3f
                        >= .5f;
                    if (isHead != (region == TripoCharacterRegion.Head))
                        continue;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int original = input[i + corner];
                        if (!mapping.TryGetValue(original, out int target))
                        {
                            target = indices.Count;
                            indices.Add(original);
                            mapping.Add(original, target);
                        }
                        triangles[submesh].Add(target);
                    }
                }
            }
            var mesh = new Mesh
            {
                name = region.ToString(),
                indexFormat = source.indexFormat,
                subMeshCount = source.subMeshCount,
            };
            mesh.vertices = Select(source.vertices, indices);
            if (source.normals.Length == source.vertexCount)
                mesh.normals = Select(source.normals, indices);
            if (source.tangents.Length == source.vertexCount)
                mesh.tangents = Select(source.tangents, indices);
            if (source.colors32.Length == source.vertexCount)
                mesh.colors32 = Select(source.colors32, indices);
            mesh.boneWeights = Select(weights, indices);
            mesh.bindposes = source.bindposes;
            for (int channel = 0; channel < 8; channel++)
            {
                var uv = new List<Vector4>();
                source.GetUVs(channel, uv);
                if (uv.Count == source.vertexCount)
                    mesh.SetUVs(channel, indices.Select(i => uv[i]).ToList());
            }
            for (int submesh = 0; submesh < triangles.Length; submesh++)
                mesh.SetTriangles(triangles[submesh], submesh, false);
            var vertices = new Vector3[source.vertexCount];
            var normals = new Vector3[source.vertexCount];
            var tangents = new Vector3[source.vertexCount];
            for (int shape = 0; shape < source.blendShapeCount; shape++)
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, vertices, normals, tangents);
                mesh.AddBlendShapeFrame(
                    source.GetBlendShapeName(shape),
                    source.GetBlendShapeFrameWeight(shape, frame),
                    Select(vertices, indices),
                    Select(normals, indices),
                    Select(tangents, indices)
                );
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static T[] Select<T>(T[] values, List<int> indices) =>
            indices.Select(i => values[i]).ToArray();
    }
}
