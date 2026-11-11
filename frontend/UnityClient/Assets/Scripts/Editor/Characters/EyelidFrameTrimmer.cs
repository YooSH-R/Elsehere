using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Characters
{
    /// <summary>
    /// NPC 눈꺼풀 메시(<see cref="EmotionFolder"/>의 Face/Blink)에서 깜빡임·찡그림의 중간 단계를 50·100 두 단계만 남긴다.
    /// 원본은 25·50·75·90·100 다섯 단계를 단계마다 정점 2,000~3,100개씩 따로 저장해, 웹 빌드 데이터에서 눈꺼풀이 크게 차지한다.
    /// 남긴 단계 사이는 Unity가 이어서 보간한다. 50·100 단계가 없는 채널은 무엇을 남길지 알 수 없어 그대로 둔다.
    /// </summary>
    public static class EyelidFrameTrimmer
    {
        public const string EmotionFolder = "Assets/Art/Npc/Emotions";

        private static readonly float[] KeptWeights = { 50f, 100f };

        private sealed class Frame
        {
            public float Weight;
            public Vector3[] Vertices,
                Normals,
                Tangents;
        }

        /// <summary>중간 단계를 줄였으면 true. 채널 순서와 단계 하나짜리 채널은 그대로 둔다.</summary>
        public static bool Trim(Mesh mesh)
        {
            var channels = new List<(string Name, List<Frame> Frames)>();
            bool trimmed = false;
            for (int channel = 0; channel < mesh.blendShapeCount; channel++)
            {
                List<Frame> frames = ReadFrames(mesh, channel);
                if (frames.Count > KeptWeights.Length && KeptWeights.All(w => HasWeight(frames, w)))
                {
                    frames = frames
                        .Where(frame => KeptWeights.Any(w => Mathf.Approximately(frame.Weight, w)))
                        .ToList();
                    trimmed = true;
                }
                channels.Add((mesh.GetBlendShapeName(channel), frames));
            }

            if (!trimmed)
            {
                return false;
            }

            mesh.ClearBlendShapes();
            foreach ((string name, List<Frame> frames) in channels)
            {
                foreach (Frame frame in frames)
                {
                    mesh.AddBlendShapeFrame(
                        name,
                        frame.Weight,
                        frame.Vertices,
                        frame.Normals,
                        frame.Tangents
                    );
                }
            }
            return true;
        }

        /// <summary>배포용 NPC 눈꺼풀 메시를 모두 줄여 저장한다. batchmode: -executeMethod A305.EditorTools.Characters.EyelidFrameTrimmer.TrimNpcEyelids</summary>
        [MenuItem("Tools/A305/Characters/Trim NPC Eyelid Frames")]
        public static void TrimNpcEyelids()
        {
            int trimmed = 0;
            int checkedMeshes = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { EmotionFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Face/Blink/"))
                {
                    continue;
                }

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                checkedMeshes++;
                if (Trim(mesh))
                {
                    EditorUtility.SetDirty(mesh);
                    trimmed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"npc_eyelid_frames_trimmed checked={checkedMeshes} trimmed={trimmed}");
        }

        private static List<Frame> ReadFrames(Mesh mesh, int channel)
        {
            var frames = new List<Frame>();
            for (int index = 0; index < mesh.GetBlendShapeFrameCount(channel); index++)
            {
                var vertices = new Vector3[mesh.vertexCount];
                var normals = new Vector3[mesh.vertexCount];
                var tangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(channel, index, vertices, normals, tangents);
                frames.Add(
                    new Frame
                    {
                        Weight = mesh.GetBlendShapeFrameWeight(channel, index),
                        Vertices = vertices,
                        // 노멀·탄젠트가 없던 단계는 없는 채로 다시 넣어 원래 저장 형태를 지킨다.
                        Normals = normals.Any(n => n != Vector3.zero) ? normals : null,
                        Tangents = tangents.Any(t => t != Vector3.zero) ? tangents : null,
                    }
                );
            }
            return frames;
        }

        private static bool HasWeight(List<Frame> frames, float weight) =>
            frames.Any(frame => Mathf.Approximately(frame.Weight, weight));
    }
}
