using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Tripo;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Offline evidence only. Assets are instantiated and never saved by this exporter.
    public static class BusinessCharacterReview
    {
        private const string Root = "Assets/Art/Characters/Tripo/NPC/LittleMung_Business_Male";

        [Serializable]
        private sealed class Surface
        {
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
        }

        [Serializable]
        private sealed class Frame
        {
            public string motion;
            public float phase;
            public Vector3 forward;
            public Surface[] surfaces;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string source;
            public Vector3 forward;
            public List<Frame> frames = new();
        }

        [Serializable]
        private sealed class Audit
        {
            public bool passed;
            public string[] errors,
                manualChecks;
        }

        public static void Export()
        {
            string output = Environment.GetEnvironmentVariable("BUSINESS_REVIEW_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new ArgumentException("Set BUSINESS_REVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var saved = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(
                "Assets/Data/Tripo/LittleMung_Business_Male_Review.asset"
            );
            var copy = Object.Instantiate(saved);
            try
            {
                var report = TripoWorkbench.Validate(copy);
                File.WriteAllText(
                    Path.Combine(output, "UnityAudit.json"),
                    JsonUtility.ToJson(
                        new Audit
                        {
                            passed = report.Passed,
                            errors = report.Errors.ToArray(),
                            manualChecks = copy.manualChecks.ToArray(),
                        },
                        true
                    )
                );
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
            ExportModel(Root + "/LittleMung_Business_Male.fbx", output, "Original");
            ExportModel(
                Root + "/MasterRig/LittleMung_Business_Male_Visual.prefab",
                output,
                "Master"
            );
            if (File.Exists(BusinessFacePreviewBuilder.Prefab))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(
                    BusinessFacePreviewBuilder.Prefab
                );
                var report = TripoMasterMotionAudit.Inspect(candidate);
                File.WriteAllText(
                    Path.Combine(output, "RetouchedMotionAudit.json"),
                    JsonUtility.ToJson(
                        new Audit
                        {
                            passed = report.Passed,
                            errors = report.Errors.ToArray(),
                            manualChecks = report.ManualChecks.ToArray(),
                        },
                        true
                    )
                );
                ExportModel(BusinessFacePreviewBuilder.Prefab, output, "Retouched");
            }
        }

        public static void ExportModel(string path, string output, string label)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var evidence = new Evidence
                {
                    source = path,
                    forward = TripoModelPreview.GetViewDirection(instance, 0),
                };
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (label == "Master")
                {
                    var skin = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                    var mesh = skin.sharedMesh;
                    File.WriteAllText(
                        Path.Combine(output, "MasterSurface.json"),
                        JsonUtility.ToJson(
                            new MannequinMasterBuilder.Surface
                            {
                                vertices = mesh.vertices,
                                normals = mesh.normals,
                                uv = mesh.uv,
                                triangles = mesh.triangles,
                                weights = mesh.boneWeights,
                                bones = skin.bones.Select(b => b.name).ToArray(),
                                bindposes = mesh.bindposes,
                            }
                        )
                    );
                }
                void Sample(string motion, float phase)
                {
                    var surfaces = new List<Surface>();
                    foreach (var skin in skins)
                    {
                        var baked = new Mesh();
                        try
                        {
                            skin.BakeMesh(baked, true);
                            var matrix = skin.transform.localToWorldMatrix;
                            var normalMatrix = matrix.inverse.transpose;
                            surfaces.Add(
                                new Surface
                                {
                                    vertices = baked
                                        .vertices.Select(matrix.MultiplyPoint3x4)
                                        .ToArray(),
                                    normals = baked
                                        .normals.Select(n =>
                                            normalMatrix.MultiplyVector(n).normalized
                                        )
                                        .ToArray(),
                                    uv = baked.uv,
                                    triangles = baked.triangles,
                                }
                            );
                        }
                        finally
                        {
                            Object.DestroyImmediate(baked);
                        }
                    }
                    evidence.frames.Add(
                        new Frame
                        {
                            motion = motion,
                            phase = phase,
                            forward = TripoModelPreview.GetViewDirection(instance, 0),
                            surfaces = surfaces.ToArray(),
                        }
                    );
                }
                Sample("Rest", 0);
                using (var preview = new TripoMotionPreview(instance))
                {
                    foreach (
                        var motion in new[]
                        {
                            TripoReviewMotion.Idle,
                            TripoReviewMotion.Walk,
                            TripoReviewMotion.Run,
                            TripoReviewMotion.Rise,
                            TripoReviewMotion.Fall,
                            TripoReviewMotion.Land,
                        }
                    )
                        for (int frame = 0; frame < 12; frame++)
                        {
                            float phase = frame / 12f;
                            preview.Apply(motion, phase);
                            Sample(motion.ToString(), phase);
                        }
                }
                File.WriteAllText(
                    Path.Combine(output, label + "Poses.json"),
                    JsonUtility.ToJson(evidence)
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
