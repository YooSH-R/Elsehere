using System;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace A305.EditorTools.Player
{
    /// <summary>전용 이동 클립과 상태 전환을 생성한다. 이미 만든 에셋은 보존한다.</summary>
    public static class LittleMungAnimationBuilder
    {
        public const string Folder = "Assets/Art/Player/LittleMung";
        public const string ControllerPath = Folder + "/Locomotion.controller";
        public const float WalkSpeed = 0.8f;
        public const float RunSpeed = 1.7f;
        private const string MotionFolder = LittleMungMotionAvatar.Folder;

        public static RuntimeAnimatorController Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(Folder);
            LittleMungMotionAvatar.Prepare();
            ImportMotion("Walk", 0, 29, true);
            ImportMotion("Run", 0, -1, true);
            ImportMotion("Jump", 0, -1, false);
            var walk = CopyMotionClip("Walk");
            var run = CopyMotionClip("Run");
            var jump = LoadClip("Jump");
            var idle = CreateIdle(jump);
            ImportJumpPhases();
            var jumpClips = AssetDatabase
                .LoadAllAssetsAtPath(MotionFolder + "/Jump.fbx")
                .OfType<AnimationClip>()
                .ToArray();
            var rise = jumpClips.First(c => c.name == "Rise");
            var fall = jumpClips.First(c => c.name == "Fall");
            var land = jumpClips.First(c => c.name == "Land");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;
            parameters[1].defaultBool = true;
            controller.parameters = parameters;
            var machine = controller.layers[0].stateMachine;
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkSpeed);
            tree.AddChild(run, RunSpeed);
            machine.defaultState = locomotion;
            var rising = machine.AddState("Rise");
            rising.motion = rise;
            var falling = machine.AddState("Fall");
            falling.motion = fall;
            var landing = machine.AddState("Land");
            landing.motion = land;
            Transition(
                locomotion,
                rising,
                false,
                "VerticalSpeed",
                AnimatorConditionMode.Greater,
                0.1f
            );
            Transition(
                locomotion,
                falling,
                false,
                "VerticalSpeed",
                AnimatorConditionMode.Less,
                0.1f
            );
            var apex = rising.AddTransition(falling);
            Setup(apex);
            apex.AddCondition(AnimatorConditionMode.Less, 0.1f, "VerticalSpeed");
            Transition(rising, landing, true);
            Transition(falling, landing, true);
            Transition(
                landing,
                rising,
                false,
                "VerticalSpeed",
                AnimatorConditionMode.Greater,
                0.1f
            );
            Transition(landing, falling, false, "VerticalSpeed", AnimatorConditionMode.Less, 0.1f);
            var recover = landing.AddTransition(locomotion);
            Setup(recover);
            recover.hasExitTime = true;
            recover.exitTime = 0.7f;
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void Transition(
            AnimatorState from,
            AnimatorState to,
            bool grounded,
            string parameter = null,
            AnimatorConditionMode mode = AnimatorConditionMode.Greater,
            float value = 0f
        )
        {
            var transition = from.AddTransition(to);
            Setup(transition);
            transition.AddCondition(
                grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                "IsGrounded"
            );
            if (parameter != null)
                transition.AddCondition(mode, value, parameter);
        }

        private static void Setup(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
        }

        private static void ImportMotion(string name, float first, float last, bool loop)
        {
            string path = MotionFolder + "/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new InvalidOperationException("이동 클립이 없습니다: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var clip = importer.defaultClipAnimations[0];
            clip.name = name;
            float offset = clip.firstFrame;
            clip.firstFrame = offset + first;
            if (last >= 0)
                clip.lastFrame = offset + last;
            clip.loopTime = loop;
            clip.loopPose = loop;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.heightFromFeet = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string name)
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(MotionFolder + "/" + name + ".fbx")
                .OfType<AnimationClip>()
                .First(clip => !clip.name.StartsWith("__preview__"));
        }

        private static AnimationClip CopyMotionClip(string name)
        {
            var copy = UnityEngine.Object.Instantiate(LoadClip(name));
            copy.name = name;
            copy.hideFlags = HideFlags.None;
            return SaveClip(copy, Folder + "/" + name + ".anim");
        }

        private static AnimationClip SaveClip(AnimationClip clip, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            EditorUtility.CopySerialized(clip, existing);
            existing.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(clip);
            return existing;
        }

        [MenuItem("Tools/A305/Player/Reimport Little Mung Motions")]
        public static void ReimportMotions()
        {
            BuilderTools.ThrowIfPlaying();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Build();
                return;
            }
            LittleMungMotionAvatar.Prepare();
            ImportMotion("Walk", 0, 29, true);
            ImportMotion("Run", 0, -1, true);
            ImportMotion("Jump", 0, -1, false);
            var walk = CopyMotionClip("Walk");
            var run = CopyMotionClip("Run");
            var idle = CreateIdle(LoadClip("Jump"));
            ImportJumpPhases();
            var jumps = AssetDatabase
                .LoadAllAssetsAtPath(MotionFolder + "/Jump.fbx")
                .OfType<AnimationClip>()
                .ToArray();
            foreach (var entry in controller.layers[0].stateMachine.states)
            {
                var state = entry.state;
                if (state.name == "Locomotion" && state.motion is BlendTree tree)
                {
                    var children = tree.children;
                    foreach (int i in Enumerable.Range(0, children.Length))
                        children[i].motion =
                            children[i].threshold == 0 ? idle
                            : children[i].threshold == WalkSpeed ? walk
                            : children[i].threshold == RunSpeed ? run
                            : children[i].motion;
                    tree.children = children;
                    EditorUtility.SetDirty(tree);
                }
                else if (state.name == "Rise" || state.name == "Fall" || state.name == "Land")
                {
                    state.motion = jumps.Single(c => c.name == state.name);
                    EditorUtility.SetDirty(state);
                }
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip CreateIdle(AnimationClip reference)
        {
            var clip = new AnimationClip { name = "Idle", frameRate = 30f };
            foreach (var binding in AnimationUtility.GetCurveBindings(reference))
            {
                if (binding.type != typeof(Animator))
                    continue;
                float value = AnimationUtility
                    .GetEditorCurve(reference, binding)
                    .Evaluate(reference.length);
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationCurve.Constant(0, 2.4f, value)
                );
            }
            var breath = new AnimationCurve(
                new Keyframe(0, 0),
                new Keyframe(1.2f, 0.025f),
                new Keyframe(2.4f, 0)
            );
            clip.SetCurve("", typeof(Animator), "Chest Front-Back", breath);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return SaveClip(clip, Folder + "/Idle.anim");
        }

        private static void ImportJumpPhases()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(MotionFolder + "/Jump.fbx");
            var clips = new ModelImporterClipAnimation[3];
            string[] names = { "Rise", "Fall", "Land" };
            float[] starts = { 27, 42, 68 };
            float[] ends = { 39, 55, 78 };
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = importer.defaultClipAnimations[0];
                clip.name = names[i];
                float offset = clip.firstFrame;
                clip.firstFrame = offset + starts[i];
                clip.lastFrame = offset + ends[i];
                clip.loopTime = i < 2;
                clip.loopPose = i < 2;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.heightFromFeet = true;
                clips[i] = clip;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
