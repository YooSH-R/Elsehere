#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    /// <summary>명시적 캡처에서만 실제 신발 메시의 지면 높이와 본 기울기를 기록한다.</summary>
    internal sealed class FootContactCapture : IDisposable
    {
        private readonly Animator animator;
        private readonly StreamWriter output;
        private readonly List<(SkinnedMeshRenderer skin, int[][] indices)> samples = new();
        private readonly Mesh baked = new();
        private readonly Vector3[] localUp = new Vector3[2];
        private readonly HumanBodyBones[] feet =
        {
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };
        private int frame;

        public FootContactCapture(Animator animator, string modelPath, string folder)
        {
            this.animator = animator;
            Directory.CreateDirectory(folder);
            output = new StreamWriter(Path.Combine(folder, "foot-contact.csv"));
            output.WriteLine("frame,phase,root_y,left_min_y,right_min_y,left_up_dot,right_up_dot");
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)
            );
            try
            {
                var rest = reference.GetComponent<Animator>();
                for (int side = 0; side < 2; side++)
                    localUp[side] = rest.GetBoneTransform(feet[side])
                        .InverseTransformDirection(Vector3.up);
            }
            finally
            {
                Object.DestroyImmediate(reference);
            }

            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var weights = skin.sharedMesh.boneWeights;
                var indices = new[] { new List<int>(), new List<int>() };
                for (int side = 0; side < 2; side++)
                {
                    string prefix = side == 0 ? "Left_" : "Right_";
                    var footBones = skin
                        .bones.Select(b =>
                            b.name.StartsWith(prefix + "Foot") || b.name.StartsWith(prefix + "Toes")
                        )
                        .ToArray();
                    for (int vertex = 0; vertex < weights.Length; vertex++)
                    {
                        var w = weights[vertex];
                        float sum =
                            (footBones[w.boneIndex0] ? w.weight0 : 0)
                            + (footBones[w.boneIndex1] ? w.weight1 : 0)
                            + (footBones[w.boneIndex2] ? w.weight2 : 0)
                            + (footBones[w.boneIndex3] ? w.weight3 : 0);
                        if (sum >= 0.5f)
                            indices[side].Add(vertex);
                    }
                }
                samples.Add((skin, indices.Select(list => list.ToArray()).ToArray()));
            }
            if (Enumerable.Range(0, 2).Any(side => samples.Sum(s => s.indices[side].Length) == 0))
                throw new InvalidOperationException(
                    "발 메시를 식별하지 못해 접지를 측정할 수 없습니다."
                );
        }

        public void Record(string phase, float rootHeight)
        {
            var heights = new[] { float.PositiveInfinity, float.PositiveInfinity };
            foreach (var sample in samples)
            {
                // BakeMesh(true)는 부모 배율을 보상한다. TransformPoint에서 배율을 한 번만 적용한다.
                sample.skin.BakeMesh(baked, true);
                var vertices = baked.vertices;
                for (int side = 0; side < 2; side++)
                    foreach (int index in sample.indices[side])
                        heights[side] = Mathf.Min(
                            heights[side],
                            sample.skin.transform.TransformPoint(vertices[index]).y
                        );
            }
            float left = animator.GetBoneTransform(feet[0]).TransformDirection(localUp[0]).y;
            float right = animator.GetBoneTransform(feet[1]).TransformDirection(localUp[1]).y;
            output.WriteLine(
                string.Join(
                    ",",
                    frame++,
                    phase,
                    rootHeight.ToString("R", CultureInfo.InvariantCulture),
                    heights[0].ToString("R", CultureInfo.InvariantCulture),
                    heights[1].ToString("R", CultureInfo.InvariantCulture),
                    left.ToString("R", CultureInfo.InvariantCulture),
                    right.ToString("R", CultureInfo.InvariantCulture)
                )
            );
        }

        public void Dispose()
        {
            output.Dispose();
            Object.DestroyImmediate(baked);
        }
    }
}
#endif
