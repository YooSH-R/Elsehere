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
            var legs = new[] { new Leg(bones, "Left"), new Leg(bones, "Right") };
            var forward = Vector3
                .ProjectOnPlane(legs[0].Toe.position - legs[0].Foot.position, Vector3.up)
                .normalized;
            float length = legs.Average(l => l.Length);
            float stride = length * (running ? .9f : .6f);
            float stance = running ? .44f : .62f;
            float lift = length * (running ? .33f : .18f);
            // 무릎을 뻣뻣하게 편 채 다리만 회전하면 발이 땅 아래로 들어간다.
            // 골반 높이를 조금 낮춰 접지 발의 IK 도달 범위를 확보한다. 상반신 회전 곡선은 만들지 않는다.
            float pelvisDrop = length * (running ? .14f : .10f);
            for (int frame = 0; frame <= frames; frame++)
            {
                rhythm.SampleAnimation(animator.gameObject, 0);
                hips.position += Vector3.down * pelvisDrop;
                for (int side = 0; side < legs.Length; side++)
                {
                    float phase = Mathf.Repeat(frame / (float)frames + side * .5f, 1);
                    float forwardOffset,
                        height;
                    if (phase < stance)
                    {
                        forwardOffset = Mathf.Lerp(stride * .5f, -stride * .5f, phase / stance);
                        height = 0;
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
                        height = lift * Mathf.Pow(Mathf.Sin(Mathf.PI * swing), 2);
                    }
                    legs[side]
                        .Place(
                            legs[side].RestAnkle + forward * forwardOffset + Vector3.up * height,
                            forward
                        );
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

        private sealed class Leg
        {
            public readonly Transform Upper,
                Lower,
                Foot,
                Toe;
            public readonly Vector3 RestAnkle;
            private readonly Quaternion footRotation;
            private readonly float upperLength,
                lowerLength;
            public float Length => upperLength + lowerLength;

            public Leg(Transform[] bones, string side)
            {
                Upper = bones.Single(b => b.name == side + "_UpperLeg");
                Lower = bones.Single(b => b.name == side + "_LowerLeg");
                Foot = bones.Single(b => b.name == side + "_Foot");
                Toe = bones.Single(b => b.name == side + "_Toes");
                RestAnkle = Foot.position;
                footRotation = Foot.rotation;
                upperLength = Vector3.Distance(Upper.position, Lower.position);
                lowerLength = Vector3.Distance(Lower.position, Foot.position);
            }

            public void Place(Vector3 target, Vector3 forward)
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
                Foot.rotation = footRotation;
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
