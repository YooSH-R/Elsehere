using System;
using System.Linq;
using A305.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Tripo
{
    public enum TripoReviewMotion
    {
        Rest,
        Arms,
        Elbows,
        Knees,
        Walk,
        Run,
        Rise,
        Fall,
        Land,
    }

    public sealed class TripoMotionPreview : IDisposable
    {
        private readonly HumanPoseHandler handler;
        private readonly LittleMungArmClearance armCorrection;
        private readonly GameObject correctionSpace;
        private HumanPose rest;
        private readonly Transform[] bones;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private bool disposed;
        private readonly Animator animator;
        private readonly bool wasEnabled;
        private readonly bool appliedRootMotion;
        private readonly AnimatorCullingMode cullingMode;
        private readonly RuntimeAnimatorController controller;
        private readonly Vector3[] scales;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private AnimationClip activeClip;
        public float Duration => activeClip == null ? 4f : activeClip.length;

        public TripoMotionPreview(GameObject instance)
        {
            if (EditorUtility.IsPersistent(instance))
                throw new ArgumentException("미리보기 복사본에서만 동작을 확인할 수 있습니다.");
            TripoModelReview.RequireHumanoid(instance);
            animator = instance.GetComponentInChildren<Animator>(true);
            wasEnabled = animator.enabled;
            appliedRootMotion = animator.applyRootMotion;
            cullingMode = animator.cullingMode;
            controller = animator.runtimeAnimatorController;
            bones = instance.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length];
            scales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                positions[i] = bones[i].localPosition;
                rotations[i] = bones[i].localRotation;
                scales[i] = bones[i].localScale;
            }
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref rest);
            armCorrection = animator.GetComponent<LittleMungArmClearance>();
            if (armCorrection != null)
            {
                var controller = instance.GetComponentInParent<CharacterController>();
                if (controller == null)
                {
                    correctionSpace = new GameObject("Master preview reference")
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    SceneManager.MoveGameObjectToScene(correctionSpace, instance.scene);
                    controller = correctionSpace.AddComponent<CharacterController>();
                    controller.enabled = false;
                }
                armCorrection.Initialize(controller);
            }
        }

        public void Apply(TripoReviewMotion motion, float phase)
        {
            if (disposed)
                return;
            if (motion >= TripoReviewMotion.Walk)
            {
                ApplyClip(LoadPlayerClip(motion), phase);
                return;
            }
            StopClip();
            var pose = rest;
            pose.muscles = SampleMuscles(rest.muscles, motion, phase);
            handler.SetHumanPose(ref pose);
        }

        public static AnimationClip LoadPlayerClip(TripoReviewMotion motion)
        {
            const string folder = "Assets/Art/Player/LittleMung/";
            string path =
                motion == TripoReviewMotion.Walk || motion == TripoReviewMotion.Run
                    ? folder + motion + ".anim"
                    : folder + "Motions/Jump.fbx";
            var clip = AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(item => item.name == motion.ToString());
            if (clip == null || !clip.humanMotion || clip.length <= 0f)
                throw new InvalidOperationException(
                    "플레이어 Humanoid 동작 클립이 없습니다: " + motion + " / " + path
                );
            return clip;
        }

        private void ApplyClip(AnimationClip clip, float phase)
        {
            if (activeClip != clip)
            {
                StopClip();
                RestoreTransforms();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                graph = PlayableGraph.Create("Tripo local motion review");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput
                    .Create(graph, "Player motion", animator)
                    .SetSourcePlayable(playable);
                activeClip = clip;
                graph.Play();
            }
            playable.SetTime(Mathf.Clamp01(phase) * clip.length);
            graph.Evaluate(0f);
            if (armCorrection != null)
                armCorrection.CorrectPose();
        }

        private void StopClip()
        {
            if (!graph.IsValid())
                return;
            graph.Destroy();
            activeClip = null;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = appliedRootMotion;
            animator.cullingMode = cullingMode;
            animator.enabled = wasEnabled;
        }

        private void RestoreTransforms()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                    continue;
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
                bones[i].localScale = scales[i];
            }
        }

        public static float[] SampleMuscles(float[] rest, TripoReviewMotion motion, float phase)
        {
            var result = (float[])rest.Clone();
            float weight = 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(phase) * Mathf.PI * 2f);
            foreach (string side in new[] { "Left", "Right" })
            {
                if (motion == TripoReviewMotion.Arms)
                    Blend(result, side + " Arm Down-Up", 0.65f, weight);
                if (motion == TripoReviewMotion.Elbows)
                    Blend(result, side + " Forearm Stretch", -0.8f, weight);
                if (motion == TripoReviewMotion.Knees)
                {
                    Blend(result, side + " Upper Leg Front-Back", 0.45f, weight);
                    Blend(result, side + " Lower Leg Stretch", -0.8f, weight);
                }
            }
            return result;
        }

        private static void Blend(float[] muscles, string name, float target, float weight)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index < 0)
                throw new InvalidOperationException("Humanoid 관절 채널이 없습니다: " + name);
            muscles[index] = Mathf.Lerp(muscles[index], target, weight);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            StopClip();
            handler.Dispose();
            RestoreTransforms();
            if (correctionSpace != null)
                UnityEngine.Object.DestroyImmediate(correctionSpace);
        }
    }
}
