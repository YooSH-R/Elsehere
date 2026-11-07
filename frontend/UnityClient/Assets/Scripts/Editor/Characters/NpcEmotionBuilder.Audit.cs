using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Characters
{
    public static partial class NpcEmotionBuilder
    {
        [Serializable]
        private sealed class Preservation
        {
            public string name;
            public int checkedMeshes,
                checkedVisemeFrames;
            public bool neutralVertices = true,
                normals = true,
                uv = true,
                weights = true,
                bindPoses = true,
                textures = true,
                speech = true;
            public string[] missingEmotions;
        }

        [Serializable]
        private sealed class PreservationReport
        {
            public List<Preservation> characters = new();
        }

        public static void Audit()
        {
            var report = new PreservationReport();
            foreach (string name in Names)
            {
                var original = JsonUtility.FromJson<Source>(
                    File.ReadAllText(Path.Combine(Output, name, "Source.json"))
                );
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
                var result = new Preservation
                {
                    name = name,
                    missingEmotions = CharacterFaceController.MissingEmotionChannels(
                        root.transform
                    ),
                };
                foreach (var source in original.meshes)
                {
                    if (source.name is "ClosedLeftLid" or "ClosedRightLid")
                        continue;
                    var current = root
                        .transform.Find(source.path)
                        .GetComponent<SkinnedMeshRenderer>();
                    var old = AssetDatabase.LoadAssetAtPath<Mesh>(source.meshPath);
                    var mesh = current.sharedMesh;
                    result.checkedMeshes++;
                    result.neutralVertices &= old.vertices.SequenceEqual(mesh.vertices);
                    result.normals &= old.normals.SequenceEqual(mesh.normals);
                    result.uv &= old.uv.SequenceEqual(mesh.uv);
                    result.weights &= old.boneWeights.SequenceEqual(mesh.boneWeights);
                    result.bindPoses &= old.bindposes.SequenceEqual(mesh.bindposes);
                    var texture = current.sharedMaterial.GetTexture("_BaseMap");
                    result.textures &= string.IsNullOrEmpty(source.texture)
                        ? texture == null
                        : Path.GetFullPath(AssetDatabase.GetAssetPath(texture)) == source.texture;
                    for (int shape = 0; shape < old.blendShapeCount; shape++)
                    {
                        string channel = old.GetBlendShapeName(shape);
                        if (!channel.StartsWith("viseme_", StringComparison.Ordinal))
                            continue;
                        int next = mesh.GetBlendShapeIndex(channel);
                        if (
                            next < 0
                            || mesh.GetBlendShapeFrameCount(next)
                                != old.GetBlendShapeFrameCount(shape)
                        )
                        {
                            result.speech = false;
                            continue;
                        }
                        for (int f = 0; f < old.GetBlendShapeFrameCount(shape); f++)
                        {
                            var before = new Vector3[old.vertexCount];
                            var after = new Vector3[mesh.vertexCount];
                            var beforeNormal = new Vector3[old.vertexCount];
                            var afterNormal = new Vector3[mesh.vertexCount];
                            old.GetBlendShapeFrameVertices(shape, f, before, beforeNormal, null);
                            mesh.GetBlendShapeFrameVertices(next, f, after, afterNormal, null);
                            result.speech &=
                                before.SequenceEqual(after)
                                && beforeNormal.SequenceEqual(afterNormal)
                                && old.GetBlendShapeFrameWeight(shape, f)
                                    == mesh.GetBlendShapeFrameWeight(next, f);
                            result.checkedVisemeFrames++;
                        }
                    }
                }
                report.characters.Add(result);
            }
            File.WriteAllText(
                Path.Combine(Output, "Preservation.json"),
                JsonUtility.ToJson(report, true)
            );
            if (
                report.characters.Any(c =>
                    !c.neutralVertices
                    || !c.normals
                    || !c.uv
                    || !c.weights
                    || !c.bindPoses
                    || !c.textures
                    || !c.speech
                    || c.missingEmotions.Length > 0
                )
            )
                throw new InvalidOperationException(
                    "캐릭터 보존 또는 감정 연결 검사 실패. Preservation.json을 확인하세요."
                );
        }
    }
}
