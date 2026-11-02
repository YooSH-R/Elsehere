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
    /// <summary>Exports posed surfaces for offline garment fitting, without saving the prefab.</summary>
    public static class PasserbyOutfitFitSamples
    {
        [Serializable]
        private sealed class Sample
        {
            public string motion;
            public float phase;
            public Vector3[] body;
            public Vector3[] upper;
            public float[] boneMatrices;
        }

        [Serializable]
        private sealed class Samples
        {
            public string[] boneNames;
            public int[] bodyTriangles;
            public List<Sample> poses = new List<Sample>();
        }

        public static void Export()
        {
            string folder = Environment.GetEnvironmentVariable("PASSERBY_REVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Set PASSERBY_REVIEW_OUTPUT.");
            Directory.CreateDirectory(folder);
            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PasserbyOutfitBuilder.NpcPath)
                );
            try
            {
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var body = skins.Single(s => s.sharedMesh.vertexCount == 8782);
                var upper = skins.Single(s => s.name == "Suit_Navy_Upper");
                var referenceInverse = upper.transform.worldToLocalMatrix;
                var result = new Samples
                {
                    boneNames = upper.bones.Select(b => b.name).ToArray(),
                    bodyTriangles = body.sharedMesh.triangles,
                };
                void AddSample(string name, float phase)
                {
                    var matrices = new float[upper.bones.Length * 16];
                    for (int bone = 0; bone < upper.bones.Length; bone++)
                    {
                        var matrix =
                            upper.bones[bone].localToWorldMatrix
                            * upper.sharedMesh.bindposes[bone]
                            * referenceInverse;
                        for (int row = 0; row < 4; row++)
                        for (int column = 0; column < 4; column++)
                            matrices[bone * 16 + row * 4 + column] = matrix[row, column];
                    }
                    result.poses.Add(
                        new Sample
                        {
                            motion = name,
                            phase = phase,
                            body = BakeWorld(body),
                            upper = BakeWorld(upper),
                            boneMatrices = matrices,
                        }
                    );
                }
                AddSample("Rest", 0);
                using (
                    var preview = new TripoMotionPreview(
                        instance.transform.Find("Visual").gameObject
                    )
                )
                {
                    foreach (
                        var motion in new[]
                        {
                            TripoReviewMotion.Idle,
                            TripoReviewMotion.Walk,
                            TripoReviewMotion.Run,
                        }
                    )
                        for (int frame = 0; frame < 12; frame++)
                        {
                            float phase = frame / 12f;
                            preview.Apply(motion, phase);
                            AddSample(motion.ToString(), phase);
                        }
                }
                File.WriteAllText(
                    Path.Combine(folder, "pose-surfaces.json"),
                    JsonUtility.ToJson(result)
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Vector3[] BakeWorld(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true);
                return mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
