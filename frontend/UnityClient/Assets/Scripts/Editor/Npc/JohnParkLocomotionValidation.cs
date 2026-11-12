using System;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    public static class JohnParkLocomotionValidation
    {
        [Serializable]
        private sealed class Report
        {
            public int frames;
            public float handPositionErrorMeters,
                handRotationErrorDegrees,
                upperBodyRotationErrorDegrees;
            public float lowestSoleOffsetMeters,
                largestPlantedSoleErrorMeters,
                largestLoopVertexErrorMeters;
            public bool passed;
            public float walkLegMotionDegrees,
                runLegMotionDegrees;
        }

        public static void BuildAndCapture()
        {
            JohnParkLocomotionBuilder.Build();
            Capture();
        }

        [MenuItem("Tools/A305/Npc/John Park/Validate Lower Body And Capture")]
        public static void Capture()
        {
            string output = Path.GetFullPath("../../artifacts/JohnLocomotion/Visual");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(JohnParkLocomotionBuilder.ScenePath);
            var actors = scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<JohnParkLocomotion>())
                .OrderBy(m => m.transform.position.x)
                .ToArray();
            var camera = scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Camera>())
                .Single();
            camera.orthographicSize = 1.15f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var report = new Report();
            var baked = new Mesh();
            // 편집기의 지연된 스키닝 캐시 대신 검사한 실제 정점으로 렌더한다.
            var snapshots = actors
                .SelectMany(m => m.GetComponentsInChildren<SkinnedMeshRenderer>())
                .Select(skin =>
                {
                    var part = new GameObject("LocomotionPreviewSnapshot");
                    part.transform.SetParent(skin.transform, false);
                    var mesh = new Mesh();
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    var properties = new MaterialPropertyBlock();
                    skin.GetPropertyBlock(properties);
                    renderer.SetPropertyBlock(properties);
                    bool wasEnabled = skin.enabled;
                    skin.enabled = false;
                    return (skin, mesh, part, wasEnabled);
                })
                .ToArray();
            try
            {
                var feet = actors.Select(m => CreateFootSamples(m, baked)).ToArray();
                var firstKnees = new Quaternion[actors.Length];
                for (int frame = 0; frame <= 96; frame++)
                {
                    float seconds = frame / 24f;
                    for (int actor = 0; actor < actors.Length; actor++)
                    {
                        var motion = actors[actor];
                        var animator = motion.GetComponentInChildren<Animator>();
                        var rhythm = animator.runtimeAnimatorController.animationClips.Single();
                        rhythm.SampleAnimation(animator.gameObject, seconds % rhythm.length);
                        var transforms = animator.GetComponentsInChildren<Transform>();
                        var chest = transforms.Single(b => b.name == "Chest");
                        var hand = transforms.Single(b => b.name == "Right_Hand");
                        var upper = transforms
                            .Where(b => b == chest || b.IsChildOf(chest))
                            .ToArray();
                        var rotations = upper.Select(b => b.localRotation).ToArray();
                        var grip = chest.InverseTransformPoint(hand.position);
                        var gripRotation = Quaternion.Inverse(chest.rotation) * hand.rotation;
                        motion.SampleAt(seconds, actor);
                        var knee = transforms.Single(b => b.name == "Left_LowerLeg");
                        if (frame == 0)
                            firstKnees[actor] = knee.localRotation;
                        float legMotion = Quaternion.Angle(firstKnees[actor], knee.localRotation);
                        if (actor == 1)
                            report.walkLegMotionDegrees = Mathf.Max(
                                report.walkLegMotionDegrees,
                                legMotion
                            );
                        if (actor == 2)
                            report.runLegMotionDegrees = Mathf.Max(
                                report.runLegMotionDegrees,
                                legMotion
                            );
                        for (int i = 0; i < upper.Length; i++)
                            report.upperBodyRotationErrorDegrees = Mathf.Max(
                                report.upperBodyRotationErrorDegrees,
                                Quaternion.Angle(rotations[i], upper[i].localRotation)
                            );
                        report.handPositionErrorMeters = Mathf.Max(
                            report.handPositionErrorMeters,
                            Vector3.Distance(grip, chest.InverseTransformPoint(hand.position))
                        );
                        report.handRotationErrorDegrees = Mathf.Max(
                            report.handRotationErrorDegrees,
                            Quaternion.Angle(
                                gripRotation,
                                Quaternion.Inverse(chest.rotation) * hand.rotation
                            )
                        );
                        feet[actor].Skin.BakeMesh(baked, true);
                        var vertices = baked
                            .vertices.Select(feet[actor].Skin.transform.TransformPoint)
                            .ToArray();
                        for (int side = 0; side < 2; side++)
                        {
                            float sole = feet[actor].Indices[side].Min(i => vertices[i].y);
                            float offset = sole - feet[actor].Ground[side];
                            report.lowestSoleOffsetMeters = Mathf.Min(
                                report.lowestSoleOffsetMeters,
                                offset
                            );
                            float duration =
                                actor == 2
                                    ? motion.Settings.Run.length
                                    : motion.Settings.Walk.length;
                            float phase = Mathf.Repeat(seconds / duration + side * .5f, 1);
                            if (actor == 0 || phase < (actor == 2 ? .44f : .62f))
                                report.largestPlantedSoleErrorMeters = Mathf.Max(
                                    report.largestPlantedSoleErrorMeters,
                                    Mathf.Abs(offset)
                                );
                        }
                        if (frame == 0)
                            feet[actor].FirstPose = vertices;
                        if (frame == 96)
                            for (int i = 0; i < vertices.Length; i++)
                                report.largestLoopVertexErrorMeters = Mathf.Max(
                                    report.largestLoopVertexErrorMeters,
                                    Vector3.Distance(feet[actor].FirstPose[i], vertices[i])
                                );
                    }
                    if (frame < 96)
                    {
                        foreach (var snapshot in snapshots)
                            snapshot.skin.BakeMesh(snapshot.mesh, true);
                        foreach (string view in new[] { "Front", "Oblique" })
                        {
                            Directory.CreateDirectory(Path.Combine(output, view));
                            var target = new Vector3(0, .85f, 0);
                            camera.transform.position =
                                target
                                + Quaternion.Euler(0, view == "Front" ? 0 : -25, 0)
                                    * new Vector3(0, .25f, 6);
                            camera.transform.LookAt(target);
                            CaptureFrame(camera, Path.Combine(output, view, $"{frame:D4}.png"));
                        }
                    }
                    report.frames++;
                }
                report.passed =
                    report.walkLegMotionDegrees > 10
                    && report.runLegMotionDegrees > 10
                    && report.handPositionErrorMeters < .0005f
                    && report.handRotationErrorDegrees < .1f
                    && report.upperBodyRotationErrorDegrees < .1f
                    && report.lowestSoleOffsetMeters > -.01f
                    && report.largestPlantedSoleErrorMeters < .012f
                    && report.largestLoopVertexErrorMeters < .001f;
                File.WriteAllText(
                    Path.Combine(output, "Validation.json"),
                    JsonUtility.ToJson(report, true)
                );
                if (!report.passed)
                    throw new InvalidOperationException(
                        "John 하반신 검수 실패: " + JsonUtility.ToJson(report)
                    );
                Debug.Log("John 하반신·잡기·접지·반복 경계 검수 통과: " + output);
            }
            finally
            {
                foreach (var snapshot in snapshots)
                {
                    snapshot.skin.enabled = snapshot.wasEnabled;
                    Object.DestroyImmediate(snapshot.part);
                    Object.DestroyImmediate(snapshot.mesh);
                }
                Object.DestroyImmediate(baked);
            }
        }

        private sealed class FootSamples
        {
            public SkinnedMeshRenderer Skin;
            public int[][] Indices;
            public float[] Ground;
            public Vector3[] FirstPose;
        }

        private static FootSamples CreateFootSamples(JohnParkLocomotion motion, Mesh baked)
        {
            var animator = motion.GetComponentInChildren<Animator>();
            animator
                .runtimeAnimatorController.animationClips.Single()
                .SampleAnimation(animator.gameObject, 0);
            var skin = motion
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(s => s.bones.Any(b => b.name == "Left_Foot"))
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            skin.BakeMesh(baked, true);
            var vertices = baked.vertices.Select(skin.transform.TransformPoint).ToArray();
            var weights = skin.sharedMesh.boneWeights;
            var indices = new[] { "Left", "Right" }
                .Select(side =>
                    Enumerable
                        .Range(0, weights.Length)
                        .Where(i =>
                            skin.bones[weights[i].boneIndex0].name == side + "_Foot"
                            && weights[i].weight0 > .9f
                        )
                        .ToArray()
                )
                .ToArray();
            return new FootSamples
            {
                Skin = skin,
                Indices = indices,
                Ground = indices.Select(ids => ids.Min(i => vertices[i].y)).ToArray(),
            };
        }

        private static void CaptureFrame(Camera camera, string path)
        {
            var target = new RenderTexture(1500, 640, 24);
            var image = new Texture2D(1500, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1500, 640), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
    }
}
