using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>존박 얼굴 자산의 실제 변형과 기존 잡기 동작을 오프라인으로 확인한다.</summary>
    public static class JohnParkFaceValidation
    {
        private static string Output => Path.GetFullPath("../../artifacts/JohnParkRhythm/Face");

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string[] missingChannels;
            public string[] verifiedPoses;
            public float[] poseDisplacementMeters;
            public float nonHeadVertexError;
            public float rightGripErrorMeters;
            public int evaluatedSpeechFrames;
            public string speechFixture;
        }

        public static void ApplyAndValidate()
        {
            JohnParkFaceBuilder.Apply();
            ValidateAndCapture();
            JohnParkRhythmValidation.ValidateAndCapture();
        }

        [MenuItem("Tools/A305/Npc/John Park/Validate Face And Capture")]
        public static void ValidateAndCapture()
        {
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(JohnParkFaceBuilder.ScenePath);
                var roots = scene.GetRootGameObjects();
                var face = roots
                    .SelectMany(root => root.GetComponentsInChildren<CharacterFaceController>())
                    .Single();
                var animator = face.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Play(JohnParkRhythmBuilder.StateName, 0, 0);
                animator.Update(0);
                var camera = roots
                    .SelectMany(root => root.GetComponentsInChildren<Camera>())
                    .Single();
                var focus = new Vector3(0, 1.1f, .03f);
                camera.transform.position = focus + Vector3.forward * 3;
                camera.transform.LookAt(focus);
                camera.orthographicSize = .70f;
                var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                var body = skins.OrderByDescending(skin => skin.sharedMesh.vertexCount).First();
                var source = AssetDatabase
                    .LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.ModelPath)
                    .GetComponentInChildren<SkinnedMeshRenderer>()
                    .sharedMesh;
                var report = new Report
                {
                    missingChannels = CharacterFaceController.MissingChannels(face.transform),
                };
                if (report.missingChannels.Length > 0)
                    throw new InvalidDataException("공통 표정 13개 채널이 누락됐습니다.");
                var original = source.vertices;
                var changed = body.sharedMesh.vertices;
                var weights = source.boneWeights;
                int headIndex = Array.FindIndex(body.bones, bone => bone.name == "Head");
                for (int i = 0; i < weights.Length; i++)
                    if (weights[i].boneIndex0 != headIndex || weights[i].weight0 < .8f)
                        report.nonHeadVertexError = Mathf.Max(
                            report.nonHeadVertexError,
                            Vector3.Distance(original[i], changed[i])
                        );
                if (
                    report.nonHeadVertexError > .000001f
                    || !weights.SequenceEqual(body.sharedMesh.boneWeights)
                    || !source.bindposes.SequenceEqual(body.sharedMesh.bindposes)
                )
                    throw new InvalidDataException(
                        "얼굴 이외의 메시·가중치·바인드 포즈가 달라졌습니다."
                    );

                face.SetPose(CharacterFacePose.Neutral);
                var edited = body.sharedMesh;
                body.sharedMesh = source;
                foreach (var skin in skins)
                    if (skin != body)
                        skin.enabled = false;
                Capture(camera, Path.Combine(Output, "Before.png"));
                body.sharedMesh = edited;
                foreach (var skin in skins)
                    skin.enabled = true;
                var neutral = ReadVertices(skins);
                var poses = (CharacterFacePose[])Enum.GetValues(typeof(CharacterFacePose));
                var displacement = new List<float>();
                foreach (var pose in poses)
                {
                    face.SetPose(pose);
                    var points = ReadVertices(skins);
                    float distance = points.Zip(neutral, Vector3.Distance).Max();
                    if (pose != CharacterFacePose.Neutral && distance < .0001f)
                        throw new InvalidDataException("실제 메시 변형이 없습니다: " + pose);
                    displacement.Add(distance);
                    Capture(camera, Path.Combine(Output, pose + ".png"));
                }
                report.verifiedPoses = poses.Select(pose => pose.ToString()).ToArray();
                report.poseDisplacementMeters = displacement.ToArray();
                face.SetPose(CharacterFacePose.Neutral);
                foreach (float angle in new[] { -30f, 30f })
                {
                    camera.transform.position =
                        focus + Quaternion.Euler(0, angle, 0) * Vector3.forward * 3;
                    camera.transform.LookAt(focus);
                    Capture(camera, Path.Combine(Output, "Oblique" + angle + ".png"));
                }
                camera.transform.position = focus + Vector3.forward * 3;
                camera.transform.LookAt(focus);

                var fixture = AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                    "Assets/Data/Characters/CafeFemaleExpressiveSpeech.asset"
                );
                var speech = face.GetComponent<CharacterSpeechPlayer>();
                speech.Configure(face, face.GetComponent<AudioSource>(), fixture);
                report.speechFixture = AssetDatabase.GetAssetPath(fixture);
                var hand = body.bones.Single(bone => bone.name == "Right_Hand");
                int handIndex = Array.IndexOf(body.bones, hand);
                int[] held = Enumerable
                    .Range(0, weights.Length)
                    .Where(i => weights[i].boneIndex0 == handIndex && weights[i].weight0 > .99999f)
                    .ToArray();
                var initial = ReadVertices(new[] { body });
                var grip = held.Select(i => hand.InverseTransformPoint(initial[i])).ToArray();
                int frames = Mathf.CeilToInt(fixture.Audio.length * 24);
                for (int frame = 0; frame < frames; frame++)
                {
                    if (frame > 0)
                        animator.Update(1f / 24);
                    speech.ApplyAtAudioTime(frame / 24f);
                    var points = ReadVertices(new[] { body });
                    for (int i = 0; i < held.Length; i++)
                        report.rightGripErrorMeters = Mathf.Max(
                            report.rightGripErrorMeters,
                            Vector3.Distance(points[held[i]], hand.TransformPoint(grip[i]))
                        );
                    Capture(camera, Path.Combine(Output, $"speech_{frame:D4}.png"));
                    report.evaluatedSpeechFrames++;
                }
                if (report.rightGripErrorMeters > .0005f)
                    throw new InvalidDataException(
                        "표정·리듬을 함께 재생하면 오른손·앰프가 변형됩니다."
                    );
                report.passed = true;
                File.WriteAllText(
                    Path.Combine(Output, "FaceReport.json"),
                    JsonUtility.ToJson(report, true)
                );
                Debug.Log("존박 표정·입모양·잡기 유지 검증 통과: " + Output);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(scene => scene.isLoaded && scene.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static Vector3[] ReadVertices(SkinnedMeshRenderer[] skins)
        {
            var points = new List<Vector3>();
            var mesh = new Mesh();
            try
            {
                foreach (var skin in skins)
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var point in mesh.vertices)
                    {
                        var world = skin.transform.TransformPoint(point);
                        if (
                            !float.IsFinite(world.x)
                            || !float.IsFinite(world.y)
                            || !float.IsFinite(world.z)
                        )
                            throw new InvalidDataException("표정 메시 좌표가 유효하지 않습니다.");
                        points.Add(world);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
            return points.ToArray();
        }

        internal static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(640, 640, 24);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var snapshots = new List<(SkinnedMeshRenderer skin, GameObject part, Mesh mesh)>();
            try
            {
                // batchmode 한 프레임 안에서 여러 표정을 렌더하므로 GPU 스킨 캐시 대신
                // 현재 본·블렌드셰이프를 BakeMesh로 평가한 동일한 메시를 그린다.
                foreach (
                    var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(
                        FindObjectsSortMode.None
                    )
                )
                {
                    if (!skin.enabled)
                        continue;
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    var part = new GameObject("FaceCaptureSnapshot");
                    part.transform.SetParent(skin.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = skin.shadowCastingMode;
                    renderer.receiveShadows = skin.receiveShadows;
                    snapshots.Add((skin, part, mesh));
                    skin.enabled = false;
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                foreach (var snapshot in snapshots)
                {
                    snapshot.skin.enabled = true;
                    Object.DestroyImmediate(snapshot.part);
                    Object.DestroyImmediate(snapshot.mesh);
                }
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
