using System;
using System.Collections.Generic;
using System.IO;
using A305.EditorTools.Tripo;
using A305.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class BusinessMotionDiagnosis
    {
        private const string Root = "Assets/Art/Characters/Tripo/NPC/LittleMung_Business_Male/";

        [Serializable]
        private sealed class Sample
        {
            public string stage,
                motion;
            public float time,
                duration;
            public Vector3[] positions;
            public Quaternion[] localRotations;
            public float[] elbowAngles;
        }

        [Serializable]
        private sealed class Report
        {
            public List<Sample> samples = new();
        }

        private static readonly HumanBodyBones[] Joints =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };

        public static void Export()
        {
            var report = new Report();
            SampleModel(Root + "LittleMung_Business_Male.fbx", "Generated", false, report);
            SampleModel(
                Root + "MasterRig/LittleMung_Business_Male_Visual.prefab",
                "MasterWithoutClearance",
                false,
                report
            );
            SampleModel(
                Root + "MasterRig/LittleMung_Business_Male_Visual.prefab",
                "MasterWithClearance",
                true,
                report
            );
            string output = Environment.GetEnvironmentVariable("BUSINESS_CORRECTION_OUTPUT");
            File.WriteAllText(
                Path.Combine(output, "MotionStages.json"),
                JsonUtility.ToJson(report)
            );
        }

        private static void SampleModel(string path, string stage, bool correction, Report report)
        {
            var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = copy.GetComponentInChildren<Animator>();
                if (
                    !correction
                    && animator.TryGetComponent<LittleMungArmClearance>(out var clearance)
                )
                    Object.DestroyImmediate(clearance);
                using var preview = new TripoMotionPreview(copy);
                foreach (
                    var motion in new[]
                    {
                        TripoReviewMotion.Idle,
                        TripoReviewMotion.Walk,
                        TripoReviewMotion.Run,
                        TripoReviewMotion.Land,
                    }
                )
                {
                    preview.Apply(motion, 0);
                    float duration = preview.Duration;
                    int count = Mathf.CeilToInt(duration * 30);
                    for (int frame = 0; frame <= count; frame++)
                    {
                        float time = Mathf.Min(frame / 30f, duration);
                        preview.Apply(motion, time / duration);
                        var sample = new Sample
                        {
                            stage = stage,
                            motion = motion.ToString(),
                            time = time,
                            duration = duration,
                            positions = new Vector3[Joints.Length],
                            localRotations = new Quaternion[Joints.Length],
                            elbowAngles = new float[2],
                        };
                        for (int i = 0; i < Joints.Length; i++)
                        {
                            var bone = animator.GetBoneTransform(Joints[i]);
                            sample.positions[i] = bone.position;
                            sample.localRotations[i] = bone.localRotation;
                        }
                        for (int side = 0; side < 2; side++)
                        {
                            int start = 2 + side * 3;
                            sample.elbowAngles[side] = Vector3.Angle(
                                sample.positions[start] - sample.positions[start + 1],
                                sample.positions[start + 2] - sample.positions[start + 1]
                            );
                        }
                        report.samples.Add(sample);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }
}
