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
    // 검수 사본에 마스터 손과 손목 연결면을 이식한다. 원본과 얼굴 저작 결과는 보존한다.
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
            string prefabPath = "Assets/Prefabs/Npc/Review/" + name + ".prefab";
            var changes = new Dictionary<Mesh, Mesh>();
            GameObject preview = null;
            BuilderTools.EnsureFolder(staging);
            try
            {
                preview = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                var fresh = preview
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                string reviewRoot = ServiceNpcReviewBuilder.Root + "/" + name + "/";
                string activeBodyPath = AssetDatabase.GetAssetPath(fresh.sharedMesh);
                if (
                    !activeBodyPath.EndsWith(".asset", StringComparison.Ordinal)
                    || !(
                        activeBodyPath.StartsWith(reviewRoot, StringComparison.Ordinal)
                        || activeBodyPath.StartsWith(
                            "Assets/Art/Npc/Emotions/" + name + "/",
                            StringComparison.Ordinal
                        )
                    )
                )
                    throw new InvalidOperationException(
                        "원본이 아닌 검수·감정 저작 사본에만 손을 적용할 수 있습니다."
                    );
                var reviewMeshes = AssetDatabase
                    .FindAssets("t:Mesh", new[] { reviewRoot.TrimEnd('/') })
                    .Select(guid =>
                        AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid))
                    );
                // 감정 저작 이후에는 프리팹이 별도 메시 사본을 참조하므로 실제 연결도 갱신한다.
                foreach (var original in reviewMeshes.Append(fresh.sharedMesh).Distinct())
                {
                    if (!HasHeadAndBothHands(original, fresh))
                        continue;
                    var repaired = Object.Instantiate(original);
                    repaired.name = original.name;
                    changes.Add(original, repaired);
                    TripoHandTemplate.Apply(TripoMasterWorkflow.RequireMaster(), fresh, repaired);
                    var handReport = TripoHandTemplateAudit.Inspect(
                        TripoMasterWorkflow.RequireMaster(),
                        fresh,
                        repaired
                    );
                    if (!handReport.Passed)
                        throw new InvalidOperationException(string.Join("\n", handReport.Errors));
                    AssetDatabase.CreateAsset(
                        repaired,
                        staging + "/Surface" + changes.Count + ".asset"
                    );
                }
                if (changes.Count == 0)
                    throw new InvalidOperationException("손을 교체할 검수 몸 메시가 없습니다.");
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
                report.Errors.AddRange(
                    CharacterFaceController.MissingChannels(preview.transform, true)
                );
                report.Errors.AddRange(TripoMasterMotionAudit.Inspect(preview).Errors);
                Directory.CreateDirectory(output);
                File.WriteAllText(
                    Path.Combine(output, "Validation.txt"),
                    $"검사 통과: {report.Passed}\n"
                        + string.Join(
                            "\n",
                            changes.Select(pair =>
                                $"{AssetDatabase.GetAssetPath(pair.Key)}: {pair.Key.vertexCount} → {pair.Value.vertexCount} 정점; 표정 채널 {pair.Value.blendShapeCount}"
                            )
                        )
                        + "\n"
                        + string.Join("\n", report.Errors)
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
                if (!report.Passed && apply)
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
                foreach (var mesh in changes.Values)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh))
                        Object.DestroyImmediate(mesh);
            }
        }

        private static bool HasHeadAndBothHands(Mesh mesh, SkinnedMeshRenderer skin)
        {
            if (mesh.bindposes.Length != skin.bones.Length)
                return false;
            var head = skin.bones.First(b => b.name == "Head");
            var left = skin.bones.First(b => b.name == "Left_Hand");
            var right = skin.bones.First(b => b.name == "Right_Hand");
            var weights = mesh.boneWeights;
            bool Contains(Transform root)
            {
                bool[] group = skin.bones.Select(b => b.IsChildOf(root)).ToArray();
                return weights.Any(w =>
                    (group[w.boneIndex0] ? w.weight0 : 0)
                        + (group[w.boneIndex1] ? w.weight1 : 0)
                        + (group[w.boneIndex2] ? w.weight2 : 0)
                        + (group[w.boneIndex3] ? w.weight3 : 0)
                    > .5f
                );
            }
            return Contains(head) && Contains(left) && Contains(right);
        }
    }
}
