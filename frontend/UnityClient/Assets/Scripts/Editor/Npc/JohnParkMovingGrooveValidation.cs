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
    public static class JohnParkMovingGrooveValidation
    {
        [Serializable]
        private sealed class Report
        {
            public string mode;
            public bool passed;
            public int frames;
            public float footTargetErrorMeters,
                footRotationErrorDegrees,
                gripErrorMeters,
                handToChestErrorMeters,
                maximumKneeBendDegrees,
                soleDifferenceMeters,
                hipTravelMeters,
                hipChangeMeters,
                lowestSoleOffsetMeters,
                largestPlantedSoleErrorMeters;
        }

        [MenuItem("Tools/A305/Npc/John Park/Validate Moving Groove And Capture")]
        public static void Capture() => Run(true);

        public static void Validate() => Run(false);

        private static void Run(bool render)
        {
            string output = Path.GetFullPath("../../artifacts/JohnLocomotion/MovingGroove");
            Directory.CreateDirectory(output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(JohnParkEmotionBuilder.ScenePath);
                var performer = Object.FindFirstObjectByType<RecordedNpcPerformance>();
                var motion = performer.GetComponent<JohnParkLocomotion>();
                var speech = performer.GetComponent<CharacterSpeechPlayer>();
                var animator = performer.GetComponentInChildren<Animator>();
                var rhythm = animator.runtimeAnimatorController.animationClips.Single();
                var current = motion.Settings;
                var baseline = Object.Instantiate(current);
                baseline.ConfigureGroove(null, 0, Vector3.zero);
                var link = new GameObject("보행·랩 비교").AddComponent<DialogueServerLink>();
                link.SetSpeaker(speech);
                performer.GetComponent<AudioSource>().mute = true;
                animator.Rebind();
                rhythm.SampleAnimation(animator.gameObject, 0);
                var body = performer
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var bones = body.bones;
                var hips = bones.Single(b => b.name == "Hips");
                var hand = bones.Single(b => b.name == "Right_Hand");
                var chest = bones.Single(b => b.name == "Chest");
                var feet = new[] { "Left", "Right" }
                    .Select(side => bones.Single(b => b.name == side + "_Foot"))
                    .ToArray();
                var weights = body.sharedMesh.boneWeights;
                var soles = feet.Select(foot =>
                        Enumerable
                            .Range(0, weights.Length)
                            .Where(i =>
                                bones[weights[i].boneIndex0] == foot && weights[i].weight0 > .9f
                            )
                            .ToArray()
                    )
                    .ToArray();
                var held = Enumerable
                    .Range(0, weights.Length)
                    .Where(i =>
                        bones[weights[i].boneIndex0] == hand && weights[i].weight0 > .99999f
                    )
                    .ToArray();
                var mesh = new Mesh();
                body.BakeMesh(mesh, true);
                var vertices = mesh.vertices.Select(body.transform.TransformPoint).ToArray();
                float ground = soles.Min(ids => ids.Min(i => vertices[i].y));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.position = new Vector3(0, ground, 0);
                floor.GetComponent<Renderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        JohnParkRhythmBuilder.ArtFolder + "/PreviewFloor.mat"
                    );
                AddGroundGrid(floor.transform, ground);
                var camera = Object.FindFirstObjectByType<Camera>();
                Vector3 focus = new Vector3(0, .85f, .02f);
                camera.transform.position =
                    focus + Vector3.up * .65f + Quaternion.Euler(0, -25, 0) * Vector3.forward * 3;
                camera.transform.LookAt(focus);
                camera.orthographicSize = 1.03f;
                float duration = DialogueSpeechWav
                    .Read(performer.Definition.Voice.bytes)
                    .Timeline.Duration;
                File.WriteAllBytes(
                    Path.Combine(output, "Speech.wav"),
                    performer.Definition.Voice.bytes
                );
                foreach (bool rap in new[] { true, false })
                {
                    if (rap)
                    {
                        if (!performer.Play(link))
                            throw new InvalidDataException("저장 랩 재생 실패");
                        speech.Pause();
                    }
                    else
                        performer.Stop(link);
                    foreach (float speed in new[] { 1f, 2f })
                    {
                        string mode = (rap ? "Rap" : "Silent") + (speed == 1 ? "Walk" : "Run");
                        string beforeFolder = Path.Combine(output, mode + "Before");
                        string afterFolder = Path.Combine(output, mode + "After");
                        Directory.CreateDirectory(beforeFolder);
                        Directory.CreateDirectory(afterFolder);
                        var report = new Report
                        {
                            mode = mode,
                            frames = Mathf.CeilToInt(duration * 24),
                        };
                        float minHip = float.PositiveInfinity,
                            maxHip = float.NegativeInfinity;
                        var trace = new System.Text.StringBuilder("seconds,beforeHip,afterHip\n");
                        for (int frame = 0; frame < report.frames; frame++)
                        {
                            float time = frame / 24f;
                            Evaluate(baseline);
                            float oldHip = hips.position.y;
                            var targets = feet.Select(f => f.position).ToArray();
                            var rotations = feet.Select(f => f.rotation).ToArray();
                            var grip = chest.InverseTransformPoint(hand.position);
                            body.BakeMesh(mesh, true);
                            vertices = mesh
                                .vertices.Select(body.transform.TransformPoint)
                                .ToArray();
                            var oldSoles = soles
                                .Select(ids => ids.Min(i => vertices[i].y))
                                .ToArray();
                            var handPoints = held.Select(i =>
                                    hand.InverseTransformPoint(vertices[i])
                                )
                                .ToArray();
                            if (render)
                                JohnParkFaceValidation.Capture(
                                    camera,
                                    Path.Combine(beforeFolder, $"frame_{frame:D4}.png")
                                );
                            Evaluate(current);
                            minHip = Mathf.Min(minHip, hips.position.y);
                            maxHip = Mathf.Max(maxHip, hips.position.y);
                            report.hipChangeMeters = Mathf.Max(
                                report.hipChangeMeters,
                                Mathf.Abs(oldHip - hips.position.y)
                            );
                            trace.AppendLine(
                                FormattableString.Invariant(
                                    $"{time:F4},{oldHip:F6},{hips.position.y:F6}"
                                )
                            );
                            body.BakeMesh(mesh, true);
                            vertices = mesh
                                .vertices.Select(body.transform.TransformPoint)
                                .ToArray();
                            for (int side = 0; side < 2; side++)
                            {
                                float soleOffset = soles[side].Min(i => vertices[i].y) - ground;
                                report.lowestSoleOffsetMeters = Mathf.Min(
                                    report.lowestSoleOffsetMeters,
                                    soleOffset
                                );
                                float cycleLength =
                                    speed == 1 ? current.Walk.length : current.Run.length;
                                float phase = Mathf.Repeat(time / cycleLength + side * .5f, 1);
                                if (phase < (speed == 1 ? .62f : .44f))
                                    report.largestPlantedSoleErrorMeters = Mathf.Max(
                                        report.largestPlantedSoleErrorMeters,
                                        Mathf.Abs(soleOffset)
                                    );
                                report.footTargetErrorMeters = Mathf.Max(
                                    report.footTargetErrorMeters,
                                    Vector3.Distance(targets[side], feet[side].position)
                                );
                                report.footRotationErrorDegrees = Mathf.Max(
                                    report.footRotationErrorDegrees,
                                    Quaternion.Angle(rotations[side], feet[side].rotation)
                                );
                                report.soleDifferenceMeters = Mathf.Max(
                                    report.soleDifferenceMeters,
                                    Mathf.Abs(oldSoles[side] - soles[side].Min(i => vertices[i].y))
                                );
                                string prefix = side == 0 ? "Left" : "Right";
                                var thigh = bones.Single(b => b.name == prefix + "_UpperLeg");
                                var shin = bones.Single(b => b.name == prefix + "_LowerLeg");
                                report.maximumKneeBendDegrees = Mathf.Max(
                                    report.maximumKneeBendDegrees,
                                    Vector3.Angle(
                                        shin.position - thigh.position,
                                        feet[side].position - shin.position
                                    )
                                );
                            }
                            report.handToChestErrorMeters = Mathf.Max(
                                report.handToChestErrorMeters,
                                Vector3.Distance(grip, chest.InverseTransformPoint(hand.position))
                            );
                            for (int i = 0; i < held.Length; i++)
                                report.gripErrorMeters = Mathf.Max(
                                    report.gripErrorMeters,
                                    Vector3.Distance(
                                        handPoints[i],
                                        hand.InverseTransformPoint(vertices[held[i]])
                                    )
                                );
                            if (render)
                                JohnParkFaceValidation.Capture(
                                    camera,
                                    Path.Combine(afterFolder, $"frame_{frame:D4}.png")
                                );

                            void Evaluate(JohnParkLocomotionSettings settings)
                            {
                                if (rap)
                                {
                                    performer.SampleMotion(time);
                                    speech.ApplyAtAudioTime(time);
                                }
                                else
                                    rhythm.SampleAnimation(
                                        animator.gameObject,
                                        time % rhythm.length
                                    );
                                motion.Configure(animator, settings);
                                motion.SampleAt(time, speed);
                            }
                        }
                        report.hipTravelMeters = maxHip - minHip;
                        report.passed =
                            report.footTargetErrorMeters < .001f
                            && report.footRotationErrorDegrees < .1f
                            && report.gripErrorMeters < .0005f
                            && report.handToChestErrorMeters < .0005f
                            && report.soleDifferenceMeters < .002f
                            && report.maximumKneeBendDegrees < 120.5f
                            && report.hipChangeMeters > .015f
                            && report.lowestSoleOffsetMeters > -.01f
                            && report.largestPlantedSoleErrorMeters < .012f;
                        File.WriteAllText(
                            Path.Combine(output, mode + ".json"),
                            JsonUtility.ToJson(report, true)
                        );
                        File.WriteAllText(Path.Combine(output, mode + ".csv"), trace.ToString());
                        if (!report.passed)
                            throw new InvalidDataException(
                                "보행 리듬 검수 실패: " + JsonUtility.ToJson(report)
                            );
                    }
                }
                performer.Stop(link);
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(mesh);
                Debug.Log("John 보행 음성 강세·무음 리듬 비교 완료: " + output);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Any(s => s.isLoaded && s.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void AddGroundGrid(Transform parent, float height)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(.38f, .44f, .5f));
            for (int line = -10; line <= 10; line++)
            for (int axis = 0; axis < 2; axis++)
            {
                var marker = new GameObject("10cm 접지 기준선");
                marker.transform.SetParent(parent, false);
                var renderer = marker.AddComponent<LineRenderer>();
                renderer.sharedMaterial = material;
                renderer.useWorldSpace = true;
                renderer.startWidth = renderer.endWidth = .0015f;
                renderer.positionCount = 2;
                for (int end = 0; end < 2; end++)
                    renderer.SetPosition(
                        end,
                        axis == 0
                            ? new Vector3(line * .1f, height + .0002f, end == 0 ? -1 : 1)
                            : new Vector3(end == 0 ? -1 : 1, height + .0002f, line * .1f)
                    );
            }
        }
    }
}
