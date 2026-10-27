using System;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public enum TripoReviewMotion
    {
        Rest,
        Arms,
        Elbows,
        Knees,
    }

    public sealed class TripoMotionPreview : IDisposable
    {
        private readonly HumanPoseHandler handler;
        private HumanPose rest;
        private readonly Transform[] bones;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private bool disposed;

        public TripoMotionPreview(GameObject instance)
        {
            if (EditorUtility.IsPersistent(instance))
                throw new ArgumentException("미리보기 복사본에서만 동작을 확인할 수 있습니다.");
            TripoModelReview.RequireHumanoid(instance);
            var animator = instance.GetComponentInChildren<Animator>(true);
            bones = instance.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[bones.Length];
            rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                positions[i] = bones[i].localPosition;
                rotations[i] = bones[i].localRotation;
            }
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref rest);
        }

        public void Apply(TripoReviewMotion motion, float phase)
        {
            if (disposed)
                return;
            var pose = rest;
            pose.muscles = SampleMuscles(rest.muscles, motion, phase);
            handler.SetHumanPose(ref pose);
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
            handler.Dispose();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                    continue;
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
            }
        }
    }
}
