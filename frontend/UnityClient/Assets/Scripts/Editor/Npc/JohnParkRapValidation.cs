using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    public static class JohnParkRapValidation
    {
        private static string Output =>
            Path.GetFullPath("../../artifacts/JohnParkRhythm/OrderRap/StandingGroove");

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public int frames,
                rigidGripVertices;
            public float duration,
                gripErrorMeters,
                handToChestErrorMeters,
                handToChestAngle,
                leftHandTravelMeters,
                mouthPeak,
                lipCornerWeightDifference;
            public string audio = "am_onyx · 1.5배 · 121 BPM · 저장 음성";
            public float hipTravelMeters,
                maximumKneeBendDegrees,
                maximumSoleDriftMeters;
        }

        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Path.Combine(Output, "Full"));
            Directory.CreateDirectory(Path.Combine(Output, "Face"));
            Directory.CreateDirectory(Path.Combine(Output, "Side"));
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(JohnParkEmotionBuilder.ScenePath);
                var face = Object.FindFirstObjectByType<CharacterFaceController>();
                var performer = face.GetComponent<RecordedNpcPerformance>();
                var speech = face.GetComponent<CharacterSpeechPlayer>();
                var animator = face.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Play(JohnParkRhythmBuilder.StateName, 0, 0);
                animator.Update(0);
                var link = new GameObject("저장 랩 검수").AddComponent<DialogueServerLink>();
                link.SetSpeaker(speech);
                if (!performer.Play(link))
                    throw new InvalidDataException("저장 랩 재생 실패");
                speech.Pause();
                if (
                    !string.IsNullOrEmpty(link.LipSyncNotice)
                    || !string.IsNullOrEmpty(link.EmotionNotice)
                )
                    throw new InvalidDataException("발음·감정 시간표 오류");
                var decoded = DialogueSpeechWav.Read(performer.Definition.Voice.bytes);
                var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                var lips = skins.Single(s => s.name == "SpeakingLips");
                int smileLeft = lips.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
                int smileRight = lips.sharedMesh.GetBlendShapeIndex("mouthSmileRight");
                var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                var hand = body.bones.Single(b => b.name == "Right_Hand");
                var left = body.bones.Single(b => b.name == "Left_Hand");
                var chest = body.bones.Single(b => b.name == "Chest");
                var hips = body.bones.Single(b => b.name == "Hips");
                float minimumHips = hips.position.y,
                    maximumHips = hips.position.y;
                int handIndex = Array.IndexOf(body.bones, hand);
                var weights = body.sharedMesh.boneWeights;
                int[] held = Enumerable
                    .Range(0, weights.Length)
                    .Where(i => weights[i].boneIndex0 == handIndex && weights[i].weight0 > .99999f)
                    .ToArray();
                var mesh = new Mesh();
                body.BakeMesh(mesh, true);
                var original = mesh.vertices;
                float ground = original.Min(v => body.transform.TransformPoint(v).y);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "임시 랩 접지 검수 바닥";
                floor.transform.position = new Vector3(0, ground, 0);
                floor.GetComponent<Renderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        JohnParkRhythmBuilder.ArtFolder + "/PreviewFloor.mat"
                    );
                var grip = held.Select(i =>
                        hand.InverseTransformPoint(body.transform.TransformPoint(original[i]))
                    )
                    .ToArray();
                var handPosition = chest.InverseTransformPoint(hand.position);
                var handRotation = Quaternion.Inverse(chest.rotation) * hand.rotation;
                var initialLeft = left.position;
                var report = new Report
                {
                    duration = decoded.Timeline.Duration,
                    rigidGripVertices = held.Length,
                };
                var camera = Object.FindFirstObjectByType<Camera>();
                var trace = new System.Text.StringBuilder("seconds,AA,FV,TH,PP,E,OH,OU,I,EU\n");
                report.frames = Mathf.CeilToInt(report.duration * 24);
                for (int frame = 0; frame < report.frames; frame++)
                {
                    float time = frame / 24f;
                    performer.SampleMotion(time);
                    speech.ApplyAtAudioTime(time);
                    report.lipCornerWeightDifference = Mathf.Max(
                        report.lipCornerWeightDifference,
                        Mathf.Abs(
                            lips.GetBlendShapeWeight(smileLeft)
                                - lips.GetBlendShapeWeight(smileRight)
                        )
                    );
                    body.BakeMesh(mesh, true);
                    var points = mesh.vertices;
                    minimumHips = Mathf.Min(minimumHips, hips.position.y);
                    maximumHips = Mathf.Max(maximumHips, hips.position.y);
                    report.maximumSoleDriftMeters = Mathf.Max(
                        report.maximumSoleDriftMeters,
                        Mathf.Abs(points.Min(v => body.transform.TransformPoint(v).y) - ground)
                    );
                    foreach (string side in new[] { "Left", "Right" })
                    {
                        var upper = body.bones.Single(b => b.name == side + "_UpperLeg");
                        var lower = body.bones.Single(b => b.name == side + "_LowerLeg");
                        var foot = body.bones.Single(b => b.name == side + "_Foot");
                        report.maximumKneeBendDegrees = Mathf.Max(
                            report.maximumKneeBendDegrees,
                            Vector3.Angle(
                                lower.position - upper.position,
                                foot.position - lower.position
                            )
                        );
                    }
                    for (int i = 0; i < held.Length; i++)
                        report.gripErrorMeters = Mathf.Max(
                            report.gripErrorMeters,
                            Vector3.Distance(
                                body.transform.TransformPoint(points[held[i]]),
                                hand.TransformPoint(grip[i])
                            )
                        );
                    report.handToChestErrorMeters = Mathf.Max(
                        report.handToChestErrorMeters,
                        Vector3.Distance(handPosition, chest.InverseTransformPoint(hand.position))
                    );
                    report.handToChestAngle = Mathf.Max(
                        report.handToChestAngle,
                        Quaternion.Angle(
                            handRotation,
                            Quaternion.Inverse(chest.rotation) * hand.rotation
                        )
                    );
                    report.leftHandTravelMeters = Mathf.Max(
                        report.leftHandTravelMeters,
                        Vector3.Distance(initialLeft, left.position)
                    );
                    trace.Append(
                        time.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                    );
                    foreach (
                        string channel in new[]
                        {
                            "AA",
                            "FF",
                            "TH",
                            "PP",
                            "E",
                            "OH",
                            "OU",
                            "I",
                            "EU",
                        }
                    )
                    {
                        float weight = skins
                            .Where(s => s.sharedMesh.GetBlendShapeIndex("viseme_" + channel) >= 0)
                            .Max(s =>
                                s.GetBlendShapeWeight(
                                    s.sharedMesh.GetBlendShapeIndex("viseme_" + channel)
                                )
                            );
                        report.mouthPeak = Mathf.Max(report.mouthPeak, weight);
                        trace
                            .Append(',')
                            .Append(
                                weight.ToString(
                                    "F4",
                                    System.Globalization.CultureInfo.InvariantCulture
                                )
                            );
                    }
                    trace.Append('\n');
                    Frame(camera, new Vector3(0, .88f, .02f), 1.02f, "Full", frame);
                    Frame(camera, new Vector3(0, 1.04f, .07f), .60f, "Face", frame);
                    Frame(camera, new Vector3(0, .88f, .02f), 1.02f, "Side", frame);
                }
                Object.DestroyImmediate(mesh);
                link.SetSpeaker(null);
                report.hipTravelMeters = maximumHips - minimumHips;
                if (
                    report.rigidGripVertices < 1000
                    || report.gripErrorMeters > .0005f
                    || report.handToChestErrorMeters > .0005f
                    || report.handToChestAngle > .1f
                    || report.leftHandTravelMeters < .03f
                    || report.mouthPeak < 20
                    || report.lipCornerWeightDifference > .01f
                    || report.hipTravelMeters < .065f
                    || report.maximumKneeBendDegrees > 125
                    || report.maximumSoleDriftMeters > .015f
                )
                    throw new InvalidDataException(
                        "랩 동작·오른손·입모양 검수 실패: " + JsonUtility.ToJson(report)
                    );
                report.passed = true;
                File.WriteAllText(
                    Path.Combine(Output, "RapReport.json"),
                    JsonUtility.ToJson(report, true)
                );
                File.WriteAllText(Path.Combine(Output, "MouthWeights.csv"), trace.ToString());
                File.WriteAllBytes(
                    Path.Combine(Output, "Speech.wav"),
                    performer.Definition.Voice.bytes
                );
                Debug.Log("존박 1.5배 랩·입꼬리 균형·오른손 유지 검수 완료: " + Output);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(s => s.isLoaded && s.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void Frame(Camera camera, Vector3 focus, float size, string view, int frame)
        {
            camera.transform.position =
                focus + Quaternion.Euler(0, view == "Side" ? -35 : 0, 0) * Vector3.forward * 3;
            camera.transform.LookAt(focus);
            camera.orthographicSize = size;
            JohnParkFaceValidation.Capture(
                camera,
                Path.Combine(Output, view, $"frame_{frame:D4}.png")
            );
        }
    }
}
