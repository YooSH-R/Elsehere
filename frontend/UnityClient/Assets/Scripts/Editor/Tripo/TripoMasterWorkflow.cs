using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoMasterWorkflow
    {
        public const string VisualTemplate = "Assets/Prefabs/Player/Characters/Female.prefab";

        public static GameObject RequireMaster()
        {
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            TripoModelReview.RequireHumanoid(master);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VisualTemplate) == null)
                throw new InvalidOperationException("서비스 공통 골격 부모 프리팹이 없습니다.");
            foreach (
                var motion in new[]
                {
                    TripoReviewMotion.Idle,
                    TripoReviewMotion.Walk,
                    TripoReviewMotion.Run,
                    TripoReviewMotion.Rise,
                    TripoReviewMotion.Fall,
                    TripoReviewMotion.Land,
                }
            )
                TripoMotionPreview.LoadPlayerClip(motion);
            return master;
        }

        public static GameObject Apply(TripoAssetRecord record)
        {
            if (
                record == null
                || record.kind != TripoAssetKind.Character
                || !record.requireHumanoid
            )
                throw new InvalidOperationException("리깅된 서비스 캐릭터를 선택하세요.");
            var master = RequireMaster();
            if (record.commonBody != null && record.commonBody != master)
                throw new InvalidOperationException(
                    "선택한 마스터는 이 서비스 공통 골격 템플릿과 다릅니다."
                );
            string sourcePath = AssetDatabase.GetAssetPath(record.sourceModel);
            string folder = AssetDatabase.GenerateUniqueAssetPath(
                Path.GetDirectoryName(sourcePath).Replace('\\', '/') + "/MasterRig"
            );
            var model = TripoMasterRig.Build(record.sourceModel, master, folder);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (TripoSurfaceCleanup.RemoveAttachedSlivers(skin.sharedMesh) > 0)
                    EditorUtility.SetDirty(skin.sharedMesh);
            AssetDatabase.SaveAssets();
            var visual = BuildVisual(model, folder + "/" + record.DisplayName + "_Visual.prefab");
            record.commonBody = master;
            record.referenceHeightMeters = 0;
            record.uniformScale = 1;
            record.revision.candidatePath = AssetDatabase.GetAssetPath(visual);
            record.masterPreparedPath = record.revision.candidatePath;
            record.masterBinding = TripoMasterBindingStamp.Capture(record);
            record.revision.Invalidate();
            EditorUtility.SetDirty(record);
            AssetDatabase.SaveAssets();
            TripoWorkbench.Validate(record);
            return visual;
        }

        public static GameObject BuildVisual(GameObject model, string path)
        {
            if (File.Exists(path))
                throw new IOException("기존 적용본을 덮어쓰지 않습니다: " + path);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(VisualTemplate);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                visual.name = Path.GetFileNameWithoutExtension(path);
                var target = visual.GetComponentInChildren<Animator>();
                var source = model.GetComponentInChildren<Animator>();
                // 컨트롤러 교체가 Humanoid 포즈를 초기화하므로 기준 본 복사보다 먼저 연결한다.
                target.runtimeAnimatorController = source.runtimeAnimatorController;
                target.avatar = source.avatar;
                var bones = target
                    .GetBoneTransform(HumanBodyBones.Hips)
                    .GetComponentsInChildren<Transform>()
                    .ToDictionary(t => t.name);
                foreach (
                    var bone in source
                        .GetBoneTransform(HumanBodyBones.Hips)
                        .GetComponentsInChildren<Transform>()
                )
                {
                    if (!bones.TryGetValue(bone.name, out var replacement))
                        throw new InvalidOperationException(
                            "공통 골격 부모 템플릿 본 누락: " + bone.name
                        );
                    replacement.localPosition = bone.localPosition;
                    replacement.localRotation = bone.localRotation;
                    replacement.localScale = bone.localScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
                }
                foreach (var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Object.DestroyImmediate(skin);
                foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var holder = new GameObject(skin.name);
                    holder.transform.SetParent(target.transform, false);
                    holder.transform.localPosition = skin.transform.localPosition;
                    holder.transform.localRotation = skin.transform.localRotation;
                    holder.transform.localScale = skin.transform.localScale;
                    var replacement = holder.AddComponent<SkinnedMeshRenderer>();
                    replacement.sharedMesh = skin.sharedMesh;
                    replacement.sharedMaterials = skin.sharedMaterials;
                    replacement.bones = skin.bones.Select(b => bones[b.name]).ToArray();
                    replacement.rootBone = target.GetBoneTransform(HumanBodyBones.Hips);
                    replacement.localBounds = skin.localBounds;
                    replacement.quality = SkinQuality.Bone4;
                    replacement.updateWhenOffscreen = true;
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                return PrefabUtility.SaveAsPrefabAsset(visual, path);
            }
            finally
            {
                Object.DestroyImmediate(visual);
            }
        }

        [MenuItem("Tools/A305/Development/Tripo/Apply Service Master To Selected Record")]
        public static void ApplySelected()
        {
            if (!BuilderTools.CanBuild())
                return;
            var record = Selection.activeObject as TripoAssetRecord;
            if (record == null)
                throw new InvalidOperationException("Tripo 작업 기록을 선택하세요.");
            Selection.activeObject = Apply(record);
        }
    }
}
