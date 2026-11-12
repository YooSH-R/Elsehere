using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>기존 모델·가중치를 바꾸지 않고 전용 Generic 하반신 곡선을 저작한다.</summary>
    public static class JohnParkLocomotionBuilder
    {
        public const string Folder = JohnParkRhythmBuilder.ArtFolder + "/Locomotion";
        public const string SettingsPath = "Assets/Data/Npc/JohnPark/Locomotion.asset";
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcJohnParkLocomotionSandbox.unity";
        private const int FramesPerSecond = 60;

        [MenuItem("Tools/A305/Npc/John Park/Build Lower Body Locomotion")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.EnsureFolder(Folder);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                JohnParkRhythmBuilder.PrefabPath
            );
            var root = Object.Instantiate(prefab);
            AnimationClip walk,
                run;
            string[] paths;
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var rhythm = animator.runtimeAnimatorController.animationClips.Single();
                rhythm.SampleAnimation(animator.gameObject, 0);
                var names = new[] { "Hips" }
                    .Concat(
                        new[] { "Left", "Right" }.SelectMany(side =>
                            new[] { "UpperLeg", "LowerLeg", "Foot", "Toes", "ToesEnd" }.Select(
                                part => side + "_" + part
                            )
                        )
                    )
                    .ToArray();
                var all = animator.GetComponentsInChildren<Transform>();
                var bones = names.Select(name => all.Single(b => b.name == name)).ToArray();
                paths = bones
                    .Select(b => AnimationUtility.CalculateTransformPath(b, animator.transform))
                    .ToArray();
                walk = CreateClip(animator, rhythm, bones, paths, false);
                run = CreateClip(animator, rhythm, bones, paths, true);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            BuilderTools.EnsureFolder(Path.GetDirectoryName(SettingsPath));
            var settings = AssetDatabase.LoadAssetAtPath<JohnParkLocomotionSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<JohnParkLocomotionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.Configure(walk, run, paths);
            EditorUtility.SetDirty(settings);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                JohnParkRhythmBuilder.ControllerPath
            );
            AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            var contents = PrefabUtility.LoadPrefabContents(JohnParkRhythmBuilder.PrefabPath);
            try
            {
                var animator = contents.GetComponentInChildren<Animator>();
                var driver =
                    contents.GetComponent<CharacterAnimationDriver>()
                    ?? contents.AddComponent<CharacterAnimationDriver>();
                driver.BindAnimator(animator);
                var motion =
                    contents.GetComponent<JohnParkLocomotion>()
                    ?? contents.AddComponent<JohnParkLocomotion>();
                motion.Configure(animator, settings);
                PrefabUtility.SaveAsPrefabAsset(contents, JohnParkRhythmBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            BuildSandbox(
                AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.PrefabPath)
            );
            Debug.Log("John 하반신 걷기·달리기 연결 완료: " + ScenePath);
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type
        )
        {
            var parameter = controller.parameters.FirstOrDefault(p => p.name == name);
            if (parameter != null && parameter.type != type)
                throw new InvalidOperationException("John 이동 파라미터 종류가 다릅니다: " + name);
            if (parameter == null)
                controller.AddParameter(name, type);
        }

        private static AnimationClip CreateClip(
            Animator animator,
            AnimationClip rhythm,
            Transform[] bones,
            string[] paths,
            bool running
        )
        {
            float duration = running ? .66f : 1f;
            int frames = Mathf.RoundToInt(duration * FramesPerSecond);
            duration = frames / (float)FramesPerSecond;
            var curves = new AnimationCurve[bones.Length, 7];
            for (int i = 0; i < bones.Length; i++)
            for (int channel = 0; channel < 7; channel++)
                curves[i, channel] = new AnimationCurve();
            rhythm.SampleAnimation(animator.gameObject, 0);
            var hips = bones[0];
            var legs = new[]
            {
                new Leg(animator, bones, "Left"),
                new Leg(animator, bones, "Right"),
            };
            var forward = Vector3
                .ProjectOnPlane(legs[0].Toe.position - legs[0].Foot.position, Vector3.up)
                .normalized;
            float length = legs.Average(l => l.Length);
            float stride = length * (running ? .9f : .65f);
            float stance = running ? .44f : .62f;
            float lift = length * (running ? .10f : .075f);
            // 발 회수는 뒤쪽에서 시작한다. 발을 몸 앞에서 높이 들던 대칭 사인 궤적을 쓰지 않는다.
            var clearance = running
                ? PoseCurve(0, 0, .24f, 1, .58f, .65f, 1, 0)
                : PoseCurve(0, 0, .4f, 1, .75f, .7f, 1, 0);
            var plantedPitch = running
                ? PoseCurve(0, -7, .2f, 0, .55f, 6, 1, 18)
                : PoseCurve(0, -12, .2f, 0, .6f, 0, 1, 24);
            var swingPitch = running
                ? PoseCurve(0, 18, .2f, 22, .6f, 5, .85f, -10, 1, -7)
                : PoseCurve(0, 24, .3f, 8, .7f, -15, 1, -12);
            var pelvisHeight = running
                ? PoseCurve(0, -.03f, .25f, -.14f, .55f, -.08f, .87f, .02f, 1, -.03f)
                : PoseCurve(0, -.055f, .2f, -.085f, .65f, -.025f, 1, -.055f);
            for (int frame = 0; frame <= frames; frame++)
            {
                rhythm.SampleAnimation(animator.gameObject, 0);
                float cycle = frame / (float)frames;
                Vector3 restHips = hips.position;
                hips.position +=
                    Vector3.up * length * pelvisHeight.Evaluate(Mathf.Repeat(cycle * 2, 1));
                var targets = new Vector3[2];
                var rotations = new Quaternion[2];
                for (int side = 0; side < legs.Length; side++)
                {
                    float phase = Mathf.Repeat(cycle + side * .5f, 1);
                    float forwardOffset,
                        height,
                        pitch;
                    if (phase < stance)
                    {
                        forwardOffset = Mathf.Lerp(stride * .5f, -stride * .5f, phase / stance);
                        height = 0;
                        pitch = plantedPitch.Evaluate(phase / stance);
                    }
                    else
                    {
                        float swing = (phase - stance) / (1 - stance);
                        float tangent = -stride * (1 - stance) / stance;
                        float square = swing * swing,
                            cube = square * swing;
                        forwardOffset =
                            (2 * cube - 3 * square + 1) * -stride * .5f
                            + (cube - 2 * square + swing) * tangent
                            + (-2 * cube + 3 * square) * stride * .5f
                            + (cube - square) * tangent;
                        height = lift * clearance.Evaluate(swing);
                        pitch = swingPitch.Evaluate(swing);
                    }
                    rotations[side] = Quaternion.AngleAxis(
                        pitch,
                        Vector3.Cross(Vector3.up, forward)
                    );
                    targets[side] = legs[side]
                        .ContactTarget(forward, forwardOffset, height, rotations[side]);
                }
                // 무릎을 끝까지 펴지 않고 실제 발바닥 접점까지 도달할 수 있는 골반 높이를 구한다.
                float reachableHeight = hips.position.y;
                for (int side = 0; side < legs.Length; side++)
                    reachableHeight = Mathf.Min(
                        reachableHeight,
                        legs[side].MaximumHipHeight(hips, targets[side])
                    );
                hips.position = new Vector3(restHips.x, reachableHeight, restHips.z);
                for (int side = 0; side < legs.Length; side++)
                {
                    legs[side].Place(targets[side], forward, rotations[side]);
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    Vector3 p = bones[i].localPosition;
                    Quaternion q = bones[i].localRotation;
                    float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                    for (int channel = 0; channel < 7; channel++)
                        curves[i, channel].AddKey(frame / (float)FramesPerSecond, values[channel]);
                }
            }
            var clip = new AnimationClip
            {
                name = running ? "JohnLowerBodyRun" : "JohnLowerBodyWalk",
                frameRate = FramesPerSecond,
            };
            string[] properties =
            {
                "localPosition.x",
                "localPosition.y",
                "localPosition.z",
                "localRotation.x",
                "localRotation.y",
                "localRotation.z",
                "localRotation.w",
            };
            for (int i = 0; i < bones.Length; i++)
            for (int channel = 0; channel < 7; channel++)
            {
                // 골반은 위치만, 다리는 회전만 기록한다. 본 길이와 상반신의 상대 자세는 유지한다.
                if ((i == 0 && channel >= 3) || (i > 0 && channel < 3))
                    continue;
                for (int key = 0; key < curves[i, channel].length; key++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(
                        curves[i, channel],
                        key,
                        AnimationUtility.TangentMode.Linear
                    );
                    AnimationUtility.SetKeyRightTangentMode(
                        curves[i, channel],
                        key,
                        AnimationUtility.TangentMode.Linear
                    );
                }
                clip.SetCurve(paths[i], typeof(Transform), properties[channel], curves[i, channel]);
            }
            clip.EnsureQuaternionContinuity();
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            string path = Folder + "/" + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
                AssetDatabase.CreateAsset(clip, path);
            else
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                clip = existing;
            }
            return clip;
        }

        private static AnimationCurve PoseCurve(params float[] pairs)
        {
            var keys = new Keyframe[pairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1], 0, 0);
            return new AnimationCurve(keys);
        }

        private sealed class Leg
        {
            public readonly Transform Upper,
                Lower,
                Foot,
                Toe;
            public readonly Vector3 RestAnkle;
            private readonly Quaternion footRotation;
            private readonly Vector3[] soleOffsets;
            private readonly float groundHeight;
            private readonly float upperLength,
                lowerLength;
            public float Length => upperLength + lowerLength;

            public Leg(Animator animator, Transform[] bones, string side)
            {
                Upper = bones.Single(b => b.name == side + "_UpperLeg");
                Lower = bones.Single(b => b.name == side + "_LowerLeg");
                Foot = bones.Single(b => b.name == side + "_Foot");
                Toe = bones.Single(b => b.name == side + "_Toes");
                RestAnkle = Foot.position;
                footRotation = Foot.rotation;
                upperLength = Vector3.Distance(Upper.position, Lower.position);
                lowerLength = Vector3.Distance(Lower.position, Foot.position);
                var skin = animator
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(s => s.bones.Contains(Foot))
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    var weights = skin.sharedMesh.boneWeights;
                    soleOffsets = Enumerable
                        .Range(0, vertices.Length)
                        .Where(i =>
                            weights[i].weight0 > .9f && skin.bones[weights[i].boneIndex0] == Foot
                        )
                        .Select(i => skin.transform.TransformPoint(vertices[i]) - RestAnkle)
                        .ToArray();
                    groundHeight = RestAnkle.y + soleOffsets.Min(p => p.y);
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            public Vector3 ContactTarget(
                Vector3 forward,
                float distance,
                float clearance,
                Quaternion roll
            )
            {
                // 뒤꿈치/발끝이 번갈아 지지점이 되어도 가장 낮은 신발 정점은 바닥 아래로 내려가지 않는다.
                Vector3 contact = soleOffsets.OrderBy(p => (roll * p).y).First();
                Vector3 pivotShift = roll * contact - contact;
                Vector3 target =
                    RestAnkle + forward * distance - Vector3.ProjectOnPlane(pivotShift, Vector3.up);
                target.y = groundHeight + clearance - (roll * contact).y;
                return target;
            }

            public float MaximumHipHeight(Transform hips, Vector3 target)
            {
                float horizontal = Vector3
                    .ProjectOnPlane(target - Upper.position, Vector3.up)
                    .sqrMagnitude;
                float reach = Length * .985f;
                return target.y
                    + Mathf.Sqrt(Mathf.Max(0, reach * reach - horizontal))
                    - (Upper.position.y - hips.position.y);
            }

            public void Place(Vector3 target, Vector3 forward, Quaternion roll)
            {
                Vector3 delta = target - Upper.position;
                float distance = Mathf.Clamp(
                    delta.magnitude,
                    Mathf.Abs(upperLength - lowerLength) + .00001f,
                    Length - .00001f
                );
                Vector3 direction = delta.normalized;
                float along =
                    (upperLength * upperLength - lowerLength * lowerLength + distance * distance)
                    / (2 * distance);
                float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
                Vector3 knee =
                    Upper.position
                    + direction * along
                    + Vector3.ProjectOnPlane(forward, direction).normalized * height;
                Upper.rotation =
                    Quaternion.FromToRotation(
                        Lower.position - Upper.position,
                        knee - Upper.position
                    ) * Upper.rotation;
                Lower.rotation =
                    Quaternion.FromToRotation(
                        Foot.position - Lower.position,
                        target - Lower.position
                    ) * Lower.rotation;
                Foot.rotation = roll * footRotation;
            }
        }

        private static void BuildSandbox(GameObject prefab)
        {
            if (File.Exists(ScenePath))
                return;
            var groups = BuilderTools.NewSceneWithGroups();
            for (int i = 0; i < 3; i++)
            {
                var actor = BuilderTools.InstantiatePrefab(
                    prefab,
                    groups.Characters,
                    new Vector3((i - 1) * 1.5f, 0, 0),
                    Quaternion.identity
                );
                actor.name = new[] { "John_Standing", "John_Walking", "John_Running" }[i];
                actor
                    .AddComponent<JohnParkLocomotionPreview>()
                    .Configure(actor.GetComponent<CharacterAnimationDriver>(), i);
            }
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(groups.Systems, false);
            camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 1.2f, 5);
            camera.transform.LookAt(new Vector3(0, .85f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.26f, .29f, .33f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(groups.Environment, false);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                JohnParkRhythmBuilder.ArtFolder + "/PreviewFloor.mat"
            );
            BuilderTools.SaveScene(groups.Scene, ScenePath);
        }
    }
}
