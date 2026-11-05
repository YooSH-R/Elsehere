using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // 원본과 얼굴 저작 결과는 보존하고 검수 사본의 손 영역만 새 마스터 변환으로 갱신한다.
    public static class ServiceNpcHandRepairBuilder
    {
        public static void PreviewSelected() => ProcessSelected(false);

        public static void RepairSelected() => ProcessSelected(true);

        private static void ProcessSelected(bool apply)
        {
            BuilderTools.ThrowIfPlaying();
            string name = Environment.GetEnvironmentVariable("NPC_REBUILD_NAME");
            string output = Environment.GetEnvironmentVariable("NPC_HAND_OUTPUT");
            if (!ServiceNpcReviewBuilder.Names.Contains(name) || string.IsNullOrEmpty(output))
                throw new InvalidOperationException(
                    "NPC_REBUILD_NAME과 NPC_HAND_OUTPUT을 지정하세요."
                );
            const string staging = "Assets/__ServiceNpcHandReview";
            if (AssetDatabase.IsValidFolder(staging))
                throw new IOException("기존 검수 자료를 덮어쓰지 않습니다: " + staging);
            string sourcePath = $"Assets/Art/Characters/Tripo/NPC/{name}/{name}.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            string prefabPath = "Assets/Prefabs/Npc/Review/" + name + ".prefab";
            var changes = new Dictionary<Mesh, Mesh>();
            GameObject preview = null;
            BuilderTools.EnsureFolder(staging);
            try
            {
                var bound = TripoMasterRig.Build(
                    source,
                    TripoMasterWorkflow.RequireMaster(),
                    staging + "/Master"
                );
                var fresh = bound.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                var animator = bound.GetComponentInChildren<Animator>();
                var left = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var right = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                bool[] hands = fresh
                    .bones.Select(b => b.IsChildOf(left) || b.IsChildOf(right))
                    .ToArray();
                int hip = Array.IndexOf(
                    fresh.bones,
                    animator.GetBoneTransform(HumanBodyBones.Hips)
                );
                foreach (
                    string guid in AssetDatabase.FindAssets(
                        "t:Mesh",
                        new[] { ServiceNpcReviewBuilder.Root + "/" + name }
                    )
                )
                {
                    var original = AssetDatabase.LoadAssetAtPath<Mesh>(
                        AssetDatabase.GUIDToAssetPath(guid)
                    );
                    if (original.vertexCount != fresh.sharedMesh.vertexCount)
                        continue;
                    var repaired = RepairSurface(original, fresh.sharedMesh, hands, hip);
                    changes.Add(original, repaired);
                    AssetDatabase.CreateAsset(
                        repaired,
                        staging + "/Surface" + changes.Count + ".asset"
                    );
                }
                if (changes.Count == 0)
                    throw new InvalidOperationException(
                        "원본과 정점 순서가 같은 검수 몸 메시가 없습니다."
                    );
                preview = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                int replacements = 0;
                foreach (var skin in preview.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (changes.TryGetValue(skin.sharedMesh, out var repaired))
                    {
                        if (
                            !skin
                                .bones.Select(b => b.name)
                                .SequenceEqual(fresh.bones.Select(b => b.name))
                        )
                            throw new InvalidOperationException(
                                "검수 사본의 본 순서가 원본과 다릅니다."
                            );
                        skin.sharedMesh = repaired;
                        skin.localBounds = repaired.bounds;
                        replacements++;
                    }
                if (replacements != 1)
                    throw new InvalidOperationException(
                        "손을 갱신할 본체 스킨을 하나로 식별하지 못했습니다."
                    );
                var report = TripoMasterRig.Inspect(preview, TripoMasterWorkflow.RequireMaster());
                report.Errors.AddRange(TripoMasterHandAudit.Inspect(source, bound).Errors);
                report.Errors.AddRange(
                    CharacterFaceController.MissingChannels(preview.transform, true)
                );
                report.Errors.AddRange(TripoMasterMotionAudit.Inspect(preview).Errors);
                Directory.CreateDirectory(output);
                File.WriteAllText(
                    Path.Combine(output, "Validation.txt"),
                    string.Join("\n", report.Errors)
                );
                string candidatePath = staging + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(preview, candidatePath);
                string previousModels = Environment.GetEnvironmentVariable("TRIPO_HAND_MODELS");
                string previousOutput = Environment.GetEnvironmentVariable("TRIPO_HAND_OUTPUT");
                string previousFull = Environment.GetEnvironmentVariable("TRIPO_HAND_FULL");
                try
                {
                    Environment.SetEnvironmentVariable(
                        "TRIPO_HAND_MODELS",
                        sourcePath + "|" + prefabPath + "|" + candidatePath
                    );
                    Environment.SetEnvironmentVariable(
                        "TRIPO_HAND_OUTPUT",
                        Path.Combine(output, "Hands")
                    );
                    Environment.SetEnvironmentVariable("TRIPO_HAND_FULL", "1");
                    TripoHandReview.Render();
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TRIPO_HAND_MODELS", previousModels);
                    Environment.SetEnvironmentVariable("TRIPO_HAND_OUTPUT", previousOutput);
                    Environment.SetEnvironmentVariable("TRIPO_HAND_FULL", previousFull);
                }
                if (!report.Passed)
                    throw new InvalidOperationException(string.Join("\n", report.Errors));
                ServiceNpcComparisonRenderer.RenderModel(
                    prefabPath,
                    Path.Combine(output, "Before")
                );
                ServiceNpcComparisonRenderer.RenderModel(
                    candidatePath,
                    Path.Combine(output, "After")
                );
                if (!apply)
                    return;
                foreach (var pair in changes)
                {
                    EditorUtility.CopySerialized(pair.Value, pair.Key);
                    EditorUtility.SetDirty(pair.Key);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (preview != null)
                    Object.DestroyImmediate(preview);
                AssetDatabase.DeleteAsset(staging);
            }
        }

        private static Mesh RepairSurface(Mesh original, Mesh bound, bool[] handBones, int hip)
        {
            if (original.bindposes.Length != bound.bindposes.Length)
                throw new InvalidOperationException("검수 메시의 바인딩 구조가 다릅니다.");
            var result = Object.Instantiate(original);
            result.name = original.name;
            var mapping = original.bindposes[hip].inverse * bound.bindposes[hip];
            var normalMapping = mapping.inverse.transpose;
            var vertices = result.vertices;
            var normals = result.normals;
            var tangents = result.tangents;
            var sourceVertices = bound.vertices;
            var sourceNormals = bound.normals;
            var sourceTangents = bound.tangents;
            var weights = bound.boneWeights;
            var uv = original.uv;
            var sourceUv = bound.uv;
            var changed = new List<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                bool affected =
                    (handBones[w.boneIndex0] && w.weight0 > 0)
                    || (handBones[w.boneIndex1] && w.weight1 > 0)
                    || (handBones[w.boneIndex2] && w.weight2 > 0)
                    || (handBones[w.boneIndex3] && w.weight3 > 0);
                if (!affected)
                    continue;
                if (
                    uv.Length != vertices.Length
                    || sourceUv.Length != vertices.Length
                    || Vector2.Distance(uv[i], sourceUv[i]) > .000001f
                )
                    throw new InvalidOperationException(
                        "손 영역의 정점·UV 대응이 달라 자동 복원을 중단합니다."
                    );
                changed.Add(i);
                vertices[i] = mapping.MultiplyPoint3x4(sourceVertices[i]);
                normals[i] = normalMapping.MultiplyVector(sourceNormals[i]).normalized;
                if (tangents.Length == vertices.Length && sourceTangents.Length == vertices.Length)
                {
                    Vector3 direction = mapping.MultiplyVector(sourceTangents[i]).normalized;
                    tangents[i] = new Vector4(
                        direction.x,
                        direction.y,
                        direction.z,
                        sourceTangents[i].w
                    );
                }
            }
            result.vertices = vertices;
            result.normals = normals;
            result.tangents = tangents;
            var delta = new Vector3[vertices.Length];
            for (int shape = 0; shape < original.blendShapeCount; shape++)
            for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
            {
                original.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                if (changed.Any(i => delta[i].sqrMagnitude > 1e-14f))
                    throw new InvalidOperationException(
                        "손 영역에 별도로 저작한 Blend Shape가 있어 자동 복원을 중단합니다."
                    );
            }
            result.RecalculateBounds();
            return result;
        }
    }
}
