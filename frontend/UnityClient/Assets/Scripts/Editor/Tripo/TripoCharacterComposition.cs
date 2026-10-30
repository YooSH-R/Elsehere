using System;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoCharacterComposition : ScriptableObject
    {
        public TripoCharacterLibrary library;
        public TripoCharacterPart head;
        public TripoCharacterPart dressedBody;
        public GameObject preview;
        public string assembledFingerprint;
        public string visuallyApprovedFingerprint;
        public string CurrentFingerprint =>
            Hash128
                .Compute(
                    (library == null ? "missing" : library.CurrentFingerprint)
                        + (head == null ? "missing" : head.CurrentFingerprint)
                        + (dressedBody == null ? "missing" : dressedBody.CurrentFingerprint)
                        + TripoCharacterFingerprint.Of(preview)
                )
                .ToString();
    }

    public static class TripoCharacterAssembly
    {
        public static TripoAuditReport Validate(
            TripoCharacterComposition recipe,
            bool requireApproval = false
        )
        {
            var report = new TripoAuditReport();
            try
            {
                if (recipe == null || recipe.preview == null)
                    throw new InvalidOperationException("먼저 조합 후보를 만드세요.");
                RequireParts(recipe.library, recipe.head, recipe.dressedBody);
                if (recipe.assembledFingerprint != recipe.CurrentFingerprint)
                    throw new InvalidOperationException(
                        "기준·부품·후보가 변경되었습니다. 새 조합을 만들고 다시 검사하세요."
                    );
                report.Errors.AddRange(
                    TripoMasterRig
                        .Inspect(recipe.preview, TripoMasterWorkflow.RequireMaster())
                        .Errors
                );
                report.Errors.AddRange(
                    TripoCharacterHeadFit.Inspect(
                        recipe.head.model,
                        recipe.dressedBody.model,
                        recipe.library.head.model
                    )
                );
                var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
                try
                {
                    var structure = TripoAssetAudit.Inspect(
                        recipe.preview,
                        TripoAssetKind.Character,
                        TripoAttachmentKind.Skinned,
                        profile
                    );
                    report.Errors.AddRange(structure.Errors);
                    report.ManualChecks.AddRange(structure.ManualChecks);
                }
                finally
                {
                    Object.DestroyImmediate(profile);
                }
                if (report.Passed)
                {
                    var motion = TripoMasterMotionAudit.Inspect(recipe.preview);
                    report.Errors.AddRange(motion.Errors);
                    report.ManualChecks.AddRange(motion.ManualChecks);
                }
                report.ManualChecks.Add(
                    "목 연결부를 정면·좌우·후면과 머리 회전에서 확인하세요. 다른 머리와 바디의 경계는 자동 봉합하지 않습니다."
                );
                report.ManualChecks.Add(
                    "착의 바디는 옷·손·다리·신발을 포함합니다. 옷 안의 숨은 몸체를 자동 복원하지 않습니다. 새 복장은 처음 한 번 준비·검수한 부품으로 등록하세요."
                );
                if (requireApproval)
                {
                    if (!recipe.head.IsApproved || !recipe.dressedBody.IsApproved)
                        report.Errors.Add("외형 부품의 검수 승인이 필요합니다.");
                    if (recipe.visuallyApprovedFingerprint != recipe.CurrentFingerprint)
                        report.Errors.Add("현재 조합의 시각 검수 승인이 필요합니다.");
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(exception.Message);
            }
            return report;
        }

        public static void Approve(TripoCharacterComposition recipe, bool visuallyReviewed)
        {
            if (!visuallyReviewed)
                throw new InvalidOperationException(
                    "외형과 목 경계·대표 동작을 직접 확인한 뒤 승인하세요."
                );
            var report = Validate(recipe);
            if (!report.Passed)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
            foreach (var part in new[] { recipe.head, recipe.dressedBody })
            {
                part.approvedFingerprint = part.CurrentFingerprint;
                EditorUtility.SetDirty(part);
            }
            recipe.visuallyApprovedFingerprint = recipe.CurrentFingerprint;
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        public static string Register(TripoCharacterComposition recipe, string path)
        {
            BuilderTools.ThrowIfPlaying();
            var report = Validate(recipe, true);
            if (!report.Passed)
                throw new InvalidOperationException(string.Join("\n", report.Errors));
            if (
                !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".prefab", StringComparison.Ordinal)
                || path.Contains("..")
                || path.Contains('\\')
            )
                throw new InvalidOperationException("Assets 하위의 새 프리팹 경로가 필요합니다.");
            if (System.IO.File.Exists(path))
                throw new InvalidOperationException("기존 프리팹을 덮어쓰지 않습니다.");
            BuilderTools.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(recipe.preview), path))
                throw new InvalidOperationException("프리팹 등록에 실패했습니다.");
            AssetDatabase.SaveAssets();
            return path;
        }

        public static TripoCharacterComposition Build(
            TripoCharacterLibrary library,
            TripoCharacterPart head,
            TripoCharacterPart body,
            string folder
        )
        {
            BuilderTools.ThrowIfPlaying();
            TripoJob.NormalizeOutputFolder(folder);
            if (System.IO.Directory.Exists(folder))
                throw new InvalidOperationException("기존 조합을 덮어쓰지 않습니다.");
            RequireParts(library, head, body);
            BuilderTools.EnsureFolder(folder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(library.referenceVisual);
            GameObject preview;
            try
            {
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Object.DestroyImmediate(skin);
                var target = instance.GetComponentInChildren<Animator>();
                Attach(target, body);
                Attach(target, head);
                preview = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Visual.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
            var recipe = ScriptableObject.CreateInstance<TripoCharacterComposition>();
            recipe.library = library;
            recipe.head = head;
            recipe.dressedBody = body;
            recipe.preview = preview;
            recipe.assembledFingerprint = recipe.CurrentFingerprint;
            AssetDatabase.CreateAsset(recipe, folder + "/Composition.asset");
            AssetDatabase.SaveAssets();
            return recipe;
        }

        private static void RequireParts(
            TripoCharacterLibrary library,
            TripoCharacterPart head,
            TripoCharacterPart body
        )
        {
            if (library == null || !library.IsCurrent)
                throw new InvalidOperationException("공통 바디 기준이 없거나 변경되었습니다.");
            foreach (
                var pair in new[]
                {
                    (head, TripoCharacterRegion.Head),
                    (body, TripoCharacterRegion.DressedBody),
                }
            )
            {
                var part = pair.Item1;
                if (part == null || part.model == null || part.region != pair.Item2)
                    throw new InvalidOperationException("머리와 착의 바디 부품을 각각 선택하세요.");
                if (part.referenceFingerprint != library.referenceFingerprint)
                    throw new InvalidOperationException("다른 공통 기준으로 준비한 부품입니다.");
                var report = TripoMasterRig.Inspect(
                    part.model,
                    TripoMasterWorkflow.RequireMaster()
                );
                if (!report.Passed)
                    throw new InvalidOperationException(string.Join("\n", report.Errors));
            }
        }

        private static void Attach(Animator target, TripoCharacterPart part)
        {
            var source = part.model.GetComponentInChildren<Animator>();
            foreach (var skin in part.model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var holder = new GameObject(part.region + "_" + skin.name);
                holder.transform.SetParent(target.transform, false);
                holder.transform.localPosition = source.transform.InverseTransformPoint(
                    skin.transform.position
                );
                holder.transform.localRotation =
                    Quaternion.Inverse(source.transform.rotation) * skin.transform.rotation;
                holder.transform.localScale = new Vector3(
                    skin.transform.lossyScale.x / source.transform.lossyScale.x,
                    skin.transform.lossyScale.y / source.transform.lossyScale.y,
                    skin.transform.lossyScale.z / source.transform.lossyScale.z
                );
                var replacement = holder.AddComponent<SkinnedMeshRenderer>();
                replacement.bones = skin
                    .bones.Select(b =>
                        target.transform.Find(
                            AnimationUtility.CalculateTransformPath(b, source.transform)
                        )
                    )
                    .ToArray();
                if (replacement.bones.Any(b => b == null))
                    throw new InvalidOperationException("공통 바디에 없는 부품 본 경로입니다.");
                var poses = skin.sharedMesh.bindposes;
                for (int i = 0; i < poses.Length; i++)
                {
                    var expected =
                        replacement.bones[i].worldToLocalMatrix
                        * holder.transform.localToWorldMatrix;
                    for (int cell = 0; cell < 16; cell++)
                        if (Mathf.Abs(expected[cell] - poses[i][cell]) > .0001f)
                            throw new InvalidOperationException(
                                "부품의 기준 자세 또는 배율이 다릅니다."
                            );
                }
                replacement.sharedMesh = skin.sharedMesh;
                replacement.sharedMaterials = skin.sharedMaterials;
                replacement.rootBone = target.GetBoneTransform(HumanBodyBones.Hips);
                replacement.localBounds = skin.localBounds;
                replacement.quality = SkinQuality.Bone4;
                replacement.updateWhenOffscreen = true;
            }
        }
    }
}
