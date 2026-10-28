using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>생성 메시를 보존하며 서비스 마스터의 휴식 골격에 다시 바인딩한다.</summary>
    public static class TripoMasterRig
    {
        public const string MasterPath =
            "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx";

        public static GameObject Build(GameObject source, GameObject master, string folder)
        {
            BuilderTools.ThrowIfPlaying();
            folder = TripoJob.NormalizeOutputFolder(folder);
            if (Directory.Exists(folder))
                throw new IOException("기존 마스터 적용본을 덮어쓰지 않습니다: " + folder);
            TripoModelReview.RequireHumanoid(source);
            TripoModelReview.RequireHumanoid(master);
            GameObject input = Object.Instantiate(source);
            GameObject output = Object.Instantiate(master);
            var created = new List<Mesh>();
            try
            {
                output.name = "Model";
                var sourceAnimator = input.GetComponentInChildren<Animator>();
                var targetAnimator = output.GetComponentInChildren<Animator>();
                if (targetAnimator.gameObject != output)
                    throw new InvalidOperationException(
                        "마스터 모델 루트에 Animator가 필요합니다."
                    );
                var bones = BoneMap(targetAnimator);
                float bodyScale =
                    BodyFrame(targetAnimator).MultiplyVector(Vector3.up).magnitude
                    / BodyFrame(sourceAnimator).MultiplyVector(Vector3.up).magnitude;
                foreach (var renderer in output.GetComponentsInChildren<Renderer>())
                    Object.DestroyImmediate(renderer);
                foreach (var filter in output.GetComponentsInChildren<MeshFilter>())
                    Object.DestroyImmediate(filter);
                if (input.GetComponentsInChildren<MeshFilter>().Length != 0)
                    throw new InvalidOperationException(
                        "정적 메시가 섞인 캐릭터는 먼저 스킨 부품으로 연결해야 합니다."
                    );
                var skins = input.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length == 0)
                    throw new InvalidOperationException("마스터에 연결할 스킨 메시가 없습니다.");
                BuilderTools.EnsureFolder(folder);
                for (int index = 0; index < skins.Length; index++)
                {
                    var skin = skins[index];
                    var holder = new GameObject("Surface" + index);
                    holder.transform.SetParent(output.transform, false);
                    var replacement = holder.AddComponent<SkinnedMeshRenderer>();
                    replacement.bones = skin
                        .bones.Select(bone =>
                        {
                            if (bone == null || !bones.TryGetValue(bone.name, out var mapped))
                                throw new InvalidOperationException(
                                    "마스터에 없는 스킨 본: " + (bone == null ? "null" : bone.name)
                                );
                            return mapped;
                        })
                        .ToArray();
                    var mesh = CopySurface(skin, replacement, bodyScale);
                    created.Add(mesh);
                    mesh.bindposes = replacement
                        .bones.Select(bone =>
                            bone.worldToLocalMatrix * holder.transform.localToWorldMatrix
                        )
                        .ToArray();
                    replacement.sharedMesh = mesh;
                    replacement.sharedMaterials = skin.sharedMaterials;
                    replacement.rootBone = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    replacement.localBounds = mesh.bounds;
                    replacement.quality = SkinQuality.Bone4;
                    replacement.updateWhenOffscreen = true;
                    AssetDatabase.CreateAsset(mesh, folder + "/Surface" + index + ".asset");
                }
                targetAnimator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        LittleMungAnimationBuilder.ControllerPath
                    );
                if (targetAnimator.runtimeAnimatorController == null)
                    throw new InvalidOperationException(
                        "마스터 이동 애니메이션 컨트롤러가 없습니다."
                    );
                targetAnimator.applyRootMotion = false;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var report = Inspect(output, master);
                if (!report.Passed)
                    throw new InvalidOperationException(string.Join("\n", report.Errors));
                var result = PrefabUtility.SaveAsPrefabAsset(output, folder + "/Model.prefab");
                AssetDatabase.SaveAssets();
                return result;
            }
            catch
            {
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(output);
                foreach (var mesh in created)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh))
                        Object.DestroyImmediate(mesh);
            }
        }

        // Hips/Head로 크기를, 양쪽 허벅지로 좌우 방향을 맞춘다. 머리카락 높이는 체형 기준에 쓰지 않는다.
        internal static Matrix4x4 BodyFrame(Animator animator)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (hips == null || head == null || left == null || right == null)
                throw new InvalidOperationException(
                    "체형 정렬에 필요한 골반·머리·허벅지 본이 없습니다."
                );
            Vector3 up = head.position - hips.position;
            Vector3 forward = Vector3.Cross(right.position - left.position, up);
            if (up.magnitude < .0001f || forward.sqrMagnitude < .00000001f)
                throw new InvalidOperationException("체형 기준 축을 계산할 수 없습니다.");
            return Matrix4x4.TRS(
                hips.position,
                Quaternion.LookRotation(forward, up),
                Vector3.one * up.magnitude
            );
        }

        private static Mesh CopySurface(
            SkinnedMeshRenderer source,
            SkinnedMeshRenderer target,
            float bodyScale
        )
        {
            var original = source.sharedMesh;
            if (original == null || !original.isReadable)
                throw new InvalidOperationException("읽을 수 있는 스킨 메시가 필요합니다.");
            var transforms = new Matrix4x4[source.bones.Length];
            var normalTransforms = new Matrix4x4[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                var from = source.bones[i];
                var to = target.bones[i];
                // 메시의 각 관절 주변을 마스터의 기준 위치/축으로 옮긴 뒤 새 bind pose를 만든다.
                // 전신만 정렬하고 bind pose를 바꾸면 손가락 메시와 실제 회전 중심이 어긋난다.
                var sourceFrame = Matrix4x4.TRS(from.position, from.rotation, Vector3.one);
                var targetFrame = Matrix4x4.TRS(to.position, to.rotation, Vector3.one * bodyScale);
                transforms[i] =
                    target.transform.worldToLocalMatrix
                    * targetFrame
                    * sourceFrame.inverse
                    * from.localToWorldMatrix
                    * original.bindposes[i];
                normalTransforms[i] = transforms[i].inverse.transpose;
            }
            var mesh = Object.Instantiate(original);
            mesh.name = original.name + "_MasterBound";
            var weights = original.boneWeights;
            mesh.vertices = original
                .vertices.Select((v, i) => TransformPoint(v, weights[i], transforms))
                .ToArray();
            mesh.normals = original
                .normals.Select(
                    (v, i) => TransformVector(v, weights[i], normalTransforms).normalized
                )
                .ToArray();
            mesh.ClearBlendShapes();
            var vertices = new Vector3[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            var tangents = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < original.blendShapeCount; shape++)
            for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
            {
                original.GetBlendShapeFrameVertices(shape, frame, vertices, normals, tangents);
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = TransformVector(vertices[i], weights[i], transforms);
                    normals[i] = TransformVector(normals[i], weights[i], normalTransforms);
                    tangents[i] = TransformVector(tangents[i], weights[i], transforms);
                }
                mesh.AddBlendShapeFrame(
                    original.GetBlendShapeName(shape),
                    original.GetBlendShapeFrameWeight(shape, frame),
                    vertices,
                    normals,
                    tangents
                );
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Vector3 TransformPoint(
            Vector3 point,
            BoneWeight weight,
            Matrix4x4[] transforms
        ) =>
            transforms[weight.boneIndex0].MultiplyPoint3x4(point) * weight.weight0
            + transforms[weight.boneIndex1].MultiplyPoint3x4(point) * weight.weight1
            + transforms[weight.boneIndex2].MultiplyPoint3x4(point) * weight.weight2
            + transforms[weight.boneIndex3].MultiplyPoint3x4(point) * weight.weight3;

        private static Vector3 TransformVector(
            Vector3 vector,
            BoneWeight weight,
            Matrix4x4[] transforms
        ) =>
            transforms[weight.boneIndex0].MultiplyVector(vector) * weight.weight0
            + transforms[weight.boneIndex1].MultiplyVector(vector) * weight.weight1
            + transforms[weight.boneIndex2].MultiplyVector(vector) * weight.weight2
            + transforms[weight.boneIndex3].MultiplyVector(vector) * weight.weight3;

        private static Dictionary<string, Transform> BoneMap(Animator animator)
        {
            var result = new Dictionary<string, Transform>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach (var bone in hips.GetComponentsInChildren<Transform>(true))
                if (!result.TryAdd(bone.name, bone))
                    throw new InvalidOperationException("중복된 본 이름: " + bone.name);
            return result;
        }

        public static TripoAuditReport Inspect(GameObject model, GameObject master)
        {
            var report = new TripoAuditReport();
            if (model == null || master == null)
            {
                report.Errors.Add("마스터와 검사 모델이 필요합니다.");
                return report;
            }
            GameObject first = Object.Instantiate(master);
            GameObject second = Object.Instantiate(model);
            try
            {
                TripoModelReview.RequireHumanoid(first);
                TripoModelReview.RequireHumanoid(second);
                var expected = first.GetComponentInChildren<Animator>();
                var actual = second.GetComponentInChildren<Animator>();
                var originals = BoneMap(expected);
                var targets = BoneMap(actual);
                if (expected.avatar != actual.avatar)
                    report.Errors.Add("마스터 Avatar가 적용되지 않았습니다.");
                foreach (var pair in originals)
                {
                    if (!targets.TryGetValue(pair.Key, out var bone))
                    {
                        report.Errors.Add("마스터 본 누락: " + pair.Key);
                        continue;
                    }
                    var original = pair.Value;
                    if (
                        bone.parent.name != original.parent.name
                        && original != expected.GetBoneTransform(HumanBodyBones.Hips)
                    )
                        report.Errors.Add("마스터 본 부모 불일치: " + pair.Key);
                    if (
                        Vector3.Distance(bone.localPosition, original.localPosition) > .00001f
                        || Quaternion.Angle(bone.localRotation, original.localRotation) > .1f
                        || Vector3.Distance(bone.localScale, original.localScale) > .0001f
                    )
                        report.Errors.Add("마스터 관절 위치·축·길이 불일치: " + pair.Key);
                }
                report.Errors.AddRange(TripoModelReview.InspectRig(second).Errors);
            }
            catch (Exception exception)
            {
                report.Errors.Add(exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
            return report;
        }
    }
}
