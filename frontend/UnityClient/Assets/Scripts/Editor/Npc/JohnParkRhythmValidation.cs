using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>전용 Controller를 실제 평가해 강체 잡기와 반복 동작을 검사한다.</summary>
    public static class JohnParkRhythmValidation
    {
        private static string Output =>
            Environment.GetEnvironmentVariable("JOHN_PARK_REVIEW_OUTPUT")
            ?? Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/JohnParkRhythm/Unity")
            );

        [Serializable]
        private sealed class Report
        {
            public bool avatarValid;
            public bool humanoid;
            public bool passed;
            public int evaluatedFrames;
            public int heldVertices;
            public int boneTransforms;
            public float clipSeconds;
            public float gripShapeErrorMeters;
            public float skinMatrixErrorMeters;
            public float rightHandPositionErrorMeters;
            public float rightHandRotationErrorDegrees;
            public float leftHandMotionMeters;
            public float chestMotionDegrees;
            public float rootDriftMeters;
            public float loopShapeErrorMeters;
            public string[] errors;
        }

        public static void BuildAndValidate()
        {
            JohnParkRhythmBuilder.Build();
            ValidateAndCapture();
        }

        [MenuItem("Tools/A305/Npc/John Park/Validate Rhythm And Capture")]
        public static void ValidateAndCapture()
        {
            if (!Application.isBatchMode && !A305.EditorTools.Common.BuilderTools.CanBuild())
                return;
            Directory.CreateDirectory(Output);
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(JohnParkRhythmBuilder.ScenePath);
                var characters = scene
                    .GetRootGameObjects()
                    .Single(item => item.name == "Characters");
                var animators = characters.GetComponentsInChildren<Animator>();
                if (animators.Length != 2)
                    throw new InvalidOperationException(
                        "정면·사선 검수 인스턴스 두 개가 필요합니다."
                    );
                foreach (var animator in animators)
                {
                    animator.Rebind();
                    animator.Play(JohnParkRhythmBuilder.StateName, 0, 0);
                    animator.Update(0);
                }
                var camera = scene
                    .GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Camera>())
                    .Single();
                Validate(animators, camera);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }

        private static void Validate(Animator[] animators, Camera camera)
        {
            var animator = animators[0];
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones = animator.GetComponentsInChildren<Transform>();
            var hand = bones.Single(item => item.name == "Right_Hand");
            var leftHand = bones.Single(item => item.name == "Left_Hand");
            var chest = bones.Single(item => item.name == "Chest");
            var root = animator.transform.parent;
            int handIndex = Array.IndexOf(skin.bones, hand);
            var weights = skin.sharedMesh.boneWeights;
            var bindVertices = skin.sharedMesh.vertices;
            int[] held = Enumerable
                .Range(0, weights.Length)
                .Where(i => weights[i].boneIndex0 == handIndex && weights[i].weight0 > .99999f)
                .ToArray();
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            var report = new Report
            {
                avatarValid = animator.avatar != null && animator.avatar.isValid,
                humanoid = animator.avatar != null && animator.avatar.isHuman,
                heldVertices = held.Length,
                boneTransforms = bones.Count(item =>
                    item.name == "Hips" || item.IsChildOf(bones.Single(bone => bone.name == "Hips"))
                ),
                clipSeconds = clip.length,
            };
            var errors = new System.Collections.Generic.List<string>();
            if (!report.avatarValid || report.humanoid)
                errors.Add("전용 Generic Avatar가 유효하지 않습니다.");
            if (held.Length < 1913)
                errors.Add("오른손과 앰프의 강체 연결 정점이 누락됐습니다.");
            if (!clip.isLooping || clip.length < 3.8f || clip.length > 4.1f)
                errors.Add("4초 반복 리듬 클립이 아닙니다.");
            if (
                skin.sharedMaterials.Any(material =>
                    material == null
                    || material.mainTexture == null
                    || material.shader.name != "Universal Render Pipeline/Lit"
                )
            )
                errors.Add("URP 색상 텍스처 연결이 누락됐습니다.");
            if (animator.applyRootMotion)
                errors.Add("루트 모션이 켜져 있습니다.");

            var baked = new Mesh();
            try
            {
                // useScale=true가 Transform 스케일 중복 적용을 보정한다(Unity 6000.3).
                skin.BakeMesh(baked, true);
                var initialVertices = baked.vertices;
                var initialWorld = initialVertices.Select(skin.transform.TransformPoint).ToArray();
                var handLocal = held.Select(i => hand.InverseTransformPoint(initialWorld[i]))
                    .ToArray();
                Vector3 handPosition = chest.InverseTransformPoint(hand.position);
                Quaternion handRotation = Quaternion.Inverse(chest.rotation) * hand.rotation;
                Vector3 leftPosition = chest.InverseTransformPoint(leftHand.position);
                Quaternion chestRotation = chest.rotation;
                Vector3 rootPosition = root.position;
                int cycleFrames = Mathf.RoundToInt(clip.length * 24);
                float stepSeconds = clip.length / cycleFrames;

                // Animator.Update로 Controller 기본 상태를 두 바퀴 재생한다. SampleAnimation 대체가 아니다.
                for (int frame = 0; frame <= cycleFrames * 2; frame++)
                {
                    if (frame > 0)
                        foreach (var actor in animators)
                            actor.Update(stepSeconds);
                    skin.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    for (int i = 0; i < held.Length; i++)
                    {
                        Vector3 actual = skin.transform.TransformPoint(vertices[held[i]]);
                        Vector3 expected = hand.TransformPoint(handLocal[i]);
                        Vector3 skinMatrixPosition = hand.localToWorldMatrix.MultiplyPoint3x4(
                            skin.sharedMesh.bindposes[handIndex]
                                .MultiplyPoint3x4(bindVertices[held[i]])
                        );
                        report.skinMatrixErrorMeters = Mathf.Max(
                            report.skinMatrixErrorMeters,
                            Vector3.Distance(actual, skinMatrixPosition)
                        );
                        report.gripShapeErrorMeters = Mathf.Max(
                            report.gripShapeErrorMeters,
                            Vector3.Distance(actual, expected)
                        );
                    }
                    report.rightHandPositionErrorMeters = Mathf.Max(
                        report.rightHandPositionErrorMeters,
                        Vector3.Distance(chest.TransformPoint(handPosition), hand.position)
                    );
                    report.rightHandRotationErrorDegrees = Mathf.Max(
                        report.rightHandRotationErrorDegrees,
                        Quaternion.Angle(
                            handRotation,
                            Quaternion.Inverse(chest.rotation) * hand.rotation
                        )
                    );
                    report.leftHandMotionMeters = Mathf.Max(
                        report.leftHandMotionMeters,
                        Vector3.Distance(chest.TransformPoint(leftPosition), leftHand.position)
                    );
                    report.chestMotionDegrees = Mathf.Max(
                        report.chestMotionDegrees,
                        Quaternion.Angle(chestRotation, chest.rotation)
                    );
                    report.rootDriftMeters = Mathf.Max(
                        report.rootDriftMeters,
                        Vector3.Distance(rootPosition, root.position)
                    );
                    if (frame == cycleFrames || frame == cycleFrames * 2)
                        for (int i = 0; i < vertices.Length; i++)
                            report.loopShapeErrorMeters = Mathf.Max(
                                report.loopShapeErrorMeters,
                                Vector3.Distance(
                                    initialWorld[i],
                                    skin.transform.TransformPoint(vertices[i])
                                )
                            );
                    if (frame < cycleFrames)
                        Capture(camera, Path.Combine(Output, $"frame_{frame:D4}.png"));
                    report.evaluatedFrames++;
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
            if (
                report.gripShapeErrorMeters > .0005f
                || report.skinMatrixErrorMeters > .0005f
                || report.rightHandPositionErrorMeters > .0005f
                || report.rightHandRotationErrorDegrees > .1f
            )
                errors.Add("오른손·앰프 잡는 자세가 재생 중 변합니다.");
            if (report.leftHandMotionMeters < .03f || report.chestMotionDegrees < 1f)
                errors.Add("왼팔·몸통의 실제 동작이 재생되지 않습니다.");
            if (report.rootDriftMeters > .0005f || report.loopShapeErrorMeters > .001f)
                errors.Add("루트 이동 또는 반복 경계의 자세 불일치가 있습니다.");
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            File.WriteAllText(
                Path.Combine(Output, "UnityRhythmReport.json"),
                JsonUtility.ToJson(report, true)
            );
            if (!report.passed)
                throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("존박 Avatar·Controller·오른손·앰프·반복 검사 통과: " + Output);
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1120, 640, 24);
            var pixels = new Texture2D(1120, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1120, 640), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
