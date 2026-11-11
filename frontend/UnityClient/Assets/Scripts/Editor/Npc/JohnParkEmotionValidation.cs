using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>공통 감정·실제 WAV 수신 경로·기존 잡기 동작을 함께 확인한다.</summary>
    public static class JohnParkEmotionValidation
    {
        private static string Output => Path.GetFullPath("../../artifacts/JohnParkRhythm/Emotions");

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public int emotions,
                speechFrames;
            public float gripErrorMeters,
                nonHeadDeltaMeters,
                preservedVisemeError;
            public float protectedAccessoryDeltaMeters;
            public string[] sharedProfileCharacters;
            public string voice = "기존 로컬 검수 음성 · 존박 실제 음성 아님";
        }

        [Serializable]
        private sealed class BoundaryProtection
        {
            public int[] protectedVertexIndices;
        }

        public static void ValidateAndCapture()
        {
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(JohnParkEmotionBuilder.ScenePath);
                var face = Object.FindFirstObjectByType<CharacterFaceController>();
                var speech = face.GetComponent<CharacterSpeechPlayer>();
                var animator = face.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Play(JohnParkRhythmBuilder.StateName, 0, 0);
                animator.Update(0);
                var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                    NpcEmotionBuilder.ProfilePath
                );
                var camera = Object.FindFirstObjectByType<Camera>();
                var focus = new Vector3(0, 1.04f, .03f);
                camera.transform.position = focus + Vector3.forward * 3;
                camera.transform.LookAt(focus);
                camera.orthographicSize = .78f;
                var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                var report = new Report { sharedProfileCharacters = AuditSharedProfiles() };
                VerifyPreservedFace(skins, report);
                if (CharacterFaceController.MissingEmotionChannels(face.transform).Length != 0)
                    throw new InvalidDataException("존박 공통 감정 채널이 빠졌습니다.");

                foreach (CharacterEmotion emotion in Enum.GetValues(typeof(CharacterEmotion)))
                {
                    face.ApplySpeech(SpeechMouthSample.Rest, profile.Sample(emotion, 1), 0);
                    JohnParkFaceValidation.Capture(camera, Path.Combine(Output, emotion + ".png"));
                    report.emotions++;
                }
                foreach (
                    var emotion in new[]
                    {
                        CharacterEmotion.Angry,
                        CharacterEmotion.Sad,
                        CharacterEmotion.Surprised,
                    }
                )
                foreach (float angle in new[] { -30f, 30f })
                {
                    face.ApplySpeech(SpeechMouthSample.Rest, profile.Sample(emotion, 1), 0);
                    camera.transform.position =
                        focus + Quaternion.Euler(0, angle, 0) * Vector3.forward * 3;
                    camera.transform.LookAt(focus);
                    JohnParkFaceValidation.Capture(
                        camera,
                        Path.Combine(Output, $"{emotion}_{angle}.png")
                    );
                }
                camera.transform.position = focus + Vector3.forward * 3;
                camera.transform.LookAt(focus);
                if (Environment.GetEnvironmentVariable("JOHN_PARK_STILLS_ONLY") == "1")
                    return;
                var link = new GameObject(
                    "저장된 서버 응답 검수"
                ).AddComponent<DialogueServerLink>();
                var settings = new SerializedObject(link);
                settings.FindProperty("useServer").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var hand = body.bones.Single(b => b.name == "Right_Hand");
                int handIndex = Array.IndexOf(body.bones, hand);
                var weights = body.sharedMesh.boneWeights;
                int[] held = Enumerable
                    .Range(0, weights.Length)
                    .Where(i => weights[i].boneIndex0 == handIndex && weights[i].weight0 > .99999f)
                    .ToArray();
                face.SetPose(CharacterFacePose.Neutral);
                var initial = Bake(body);
                var grip = held.Select(i =>
                        hand.InverseTransformPoint(body.transform.TransformPoint(initial[i]))
                    )
                    .ToArray();
                if (held.Length < 1000)
                    throw new InvalidDataException("오른손·앰프 정점을 찾지 못했습니다.");
                foreach (string emotion in new[] { "Angry", "Sad", "Surprised" })
                {
                    byte[] wav = File.ReadAllBytes(
                        "Assets/Audio/Characters/JohnParkDialogue/" + emotion + ".bytes"
                    );
                    var decoded = DialogueSpeechWav.Read(wav);
                    link.SetSpeaker(speech);
                    if (
                        !link.PlayRecordedSpeech(wav)
                        || decoded.Timeline == null
                        || decoded.Emotions == null
                    )
                        throw new InvalidDataException("저장된 음성·발음·감정 시간표 재생 실패");
                    speech.Pause();
                    var trace = new System.Text.StringBuilder(
                        "seconds,AA,FV,TH,PP,E,OH,OU,I,EU,browDown,browInnerUp,browOuterUp\n"
                    );
                    int frames = Mathf.CeilToInt(decoded.Timeline.Duration * 24);
                    string directory = Path.Combine(Output, emotion);
                    Directory.CreateDirectory(directory);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        animator.Update(1f / 24);
                        speech.ApplyAtAudioTime(frame / 24f);
                        var points = Bake(body);
                        for (int i = 0; i < held.Length; i++)
                            report.gripErrorMeters = Mathf.Max(
                                report.gripErrorMeters,
                                Vector3.Distance(
                                    body.transform.TransformPoint(points[held[i]]),
                                    hand.TransformPoint(grip[i])
                                )
                            );
                        trace.Append(
                            (frame / 24f).ToString(
                                "F4",
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                        );
                        foreach (
                            string name in new[]
                            {
                                "viseme_AA",
                                "viseme_FF",
                                "viseme_TH",
                                "viseme_PP",
                                "viseme_E",
                                "viseme_OH",
                                "viseme_OU",
                                "viseme_I",
                                "viseme_EU",
                                "browDownLeft",
                                "browInnerUpLeft",
                                "browOuterUpLeft",
                            }
                        )
                        {
                            float value = skins
                                .Where(s => s.sharedMesh.GetBlendShapeIndex(name) >= 0)
                                .Max(s =>
                                    s.GetBlendShapeWeight(s.sharedMesh.GetBlendShapeIndex(name))
                                );
                            trace
                                .Append(',')
                                .Append(
                                    value.ToString(
                                        "F4",
                                        System.Globalization.CultureInfo.InvariantCulture
                                    )
                                );
                        }
                        trace.Append('\n');
                        JohnParkFaceValidation.Capture(
                            camera,
                            Path.Combine(directory, $"frame_{frame:D4}.png")
                        );
                        report.speechFrames++;
                    }
                    File.WriteAllText(Path.Combine(directory, "Weights.csv"), trace.ToString());
                    File.WriteAllBytes(Path.Combine(directory, "Speech.wav"), wav);
                }
                link.SetSpeaker(null);
                if (
                    report.gripErrorMeters > .0005f
                    || report.nonHeadDeltaMeters > .000001f
                    || report.preservedVisemeError > .000001f
                    || report.protectedAccessoryDeltaMeters > .000001f
                )
                    throw new InvalidDataException(
                        "감정 적용 후 기존 입모양 또는 잡기 자세가 변경됐습니다."
                    );
                report.passed = true;
                File.WriteAllText(
                    Path.Combine(Output, "EmotionReport.json"),
                    JsonUtility.ToJson(report, true)
                );
                Debug.Log("존박 공통 감정·대화 음성·잡기 유지 검증 통과: " + Output);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(s => s.isLoaded && s.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static Vector3[] Bake(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                return mesh.vertices;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static void VerifyPreservedFace(SkinnedMeshRenderer[] skins, Report report)
        {
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                if (skin.name == "SpeakingLips" && mesh.GetBlendShapeIndex("eyeWideLeft") >= 0)
                    throw new InvalidDataException("눈 넓힘이 입 접합 피부까지 움직입니다.");
                string originalPath = AssetDatabase
                    .GetAssetPath(mesh)
                    .Replace("/Emotions/", "/Face/");
                if (skin.name == "LittleMung_JohnPark_Male_Body")
                    originalPath =
                        JohnParkRhythmBuilder.ArtFolder + "/Face/JohnParkFaceSurface.asset";
                var original = AssetDatabase.LoadAssetAtPath<Mesh>(originalPath);
                if (original == null)
                    throw new InvalidDataException(
                        "표정 이전 메시를 찾지 못했습니다: " + originalPath
                    );
                if (
                    !mesh.vertices.SequenceEqual(original.vertices)
                    || !mesh.uv.SequenceEqual(original.uv)
                    || !mesh.boneWeights.SequenceEqual(original.boneWeights)
                    || !mesh.bindposes.SequenceEqual(original.bindposes)
                    || !mesh.triangles.SequenceEqual(original.triangles)
                )
                    throw new InvalidDataException(
                        "기존 중립 메시·UV·가중치·골격이 바뀌었습니다: " + skin.name
                    );
                var delta = new Vector3[mesh.vertexCount];
                var previous = new Vector3[mesh.vertexCount];
                var weights = mesh.boneWeights;
                var protectedIndices =
                    skin.name == "LittleMung_JohnPark_Male_Body"
                        ? JsonUtility
                            .FromJson<BoundaryProtection>(
                                File.ReadAllText(
                                    JohnParkEmotionBuilder.Authoring + "/BoundaryProtection.json"
                                )
                            )
                            .protectedVertexIndices
                        : Array.Empty<int>();
                int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                for (int channel = 0; channel < mesh.blendShapeCount; channel++)
                {
                    string name = mesh.GetBlendShapeName(channel);
                    int old = original.GetBlendShapeIndex(name);
                    for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(channel); frame++)
                    {
                        mesh.GetBlendShapeFrameVertices(channel, frame, delta, null, null);
                        if (name.StartsWith("viseme_", StringComparison.Ordinal))
                        {
                            if (
                                old < 0
                                || original.GetBlendShapeFrameCount(old)
                                    != mesh.GetBlendShapeFrameCount(channel)
                            )
                                throw new InvalidDataException("발음 프레임이 바뀌었습니다.");
                            original.GetBlendShapeFrameVertices(old, frame, previous, null, null);
                            for (int i = 0; i < delta.Length; i++)
                                report.preservedVisemeError = Mathf.Max(
                                    report.preservedVisemeError,
                                    Vector3.Distance(delta[i], previous[i])
                                );
                        }
                        if (old >= 0)
                            continue;
                        foreach (int index in protectedIndices)
                            report.protectedAccessoryDeltaMeters = Mathf.Max(
                                report.protectedAccessoryDeltaMeters,
                                delta[index].magnitude
                            );
                        for (int i = 0; i < delta.Length; i++)
                            if (weights[i].boneIndex0 != head || weights[i].weight0 < .8f)
                                report.nonHeadDeltaMeters = Mathf.Max(
                                    report.nonHeadDeltaMeters,
                                    delta[i].magnitude
                                );
                    }
                }
            }
        }

        private static string[] AuditSharedProfiles()
        {
            var names = NpcEmotionBuilder
                .Names.Concat(new[] { "LittleMung_Cafe_Female" })
                .ToArray();
            var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                NpcEmotionBuilder.ProfilePath
            );
            foreach (string name in names)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                    NpcEmotionBuilder.PrefabPath(name)
                );
                if (
                    root == null
                    || CharacterFaceController.MissingEmotionChannels(root.transform).Length > 0
                )
                    throw new InvalidDataException("기존 NPC 감정 채널 누락: " + name);
                var player = root.GetComponentInChildren<CharacterSpeechPlayer>(true);
                if (
                    player == null
                    || new SerializedObject(player)
                        .FindProperty("emotionProfile")
                        .objectReferenceValue != profile
                )
                    throw new InvalidDataException("공통 감정 프로필 연결 누락: " + name);
            }
            return names;
        }
    }
}
