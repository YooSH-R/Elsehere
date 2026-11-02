using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Visual authoring only. Clones the face and its static mouth seam; preserves the original texture.
    public static class ServiceNpcEyeShapeBuilder
    {
        [Serializable]
        private sealed class Surface
        {
            public string name;
            public Vector3[] originalVertices,
                vertices,
                normals;
            public Surface[] parts;
        }

        public static void Build()
        {
            foreach (
                string name in new[]
                {
                    "LittleMung_MiddleAge_Male",
                    "LittleMung_MiddleAge_Female",
                    "LittleMung_BlackYoung_Male",
                }
            )
            {
                var paths = ServiceNpcReviewBuilder.Paths(name);
                var authored = JsonUtility.FromJson<Surface>(
                    File.ReadAllText(Path.Combine(paths.Output, "EyeSurface.json"))
                );
                string folder = paths.Art + "/EyeShape";
                if (AssetDatabase.IsValidFolder(folder))
                    throw new IOException("Preserve the existing eye-shape copy: " + name);
                var instance = PrefabUtility.LoadPrefabContents(paths.Prefab);
                try
                {
                    var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                    Validate(body.sharedMesh, authored);
                    foreach (var part in authored.parts)
                        Validate(skins.Single(s => s.name == part.name).sharedMesh, part);
                    A305.EditorTools.Common.BuilderTools.EnsureFolder(folder);
                    Apply(body, authored, folder + "/FaceSurface.asset");
                    foreach (var part in authored.parts)
                        Apply(
                            skins.Single(s => s.name == part.name),
                            part,
                            folder + "/" + part.name + ".asset"
                        );
                    PrefabUtility.SaveAsPrefabAsset(instance, paths.Prefab);
                    File.WriteAllText(
                        Path.Combine(paths.Output, "EyeImport.txt"),
                        "Original textures retained. Vertex order, triangles, UVs, bones, skin weights and blendshape deltas preserved. Eye geometry and static mouth seam moved together."
                    );
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
            AssetDatabase.SaveAssets();
            ServiceNpcReviewBuilder.RenderFaces();
            ServiceNpcReviewBuilder.ValidateAndScenes();
        }

        private static void Validate(Mesh mesh, Surface surface)
        {
            if (
                !mesh.vertices.SequenceEqual(surface.originalVertices)
                || surface.vertices.Length != mesh.vertexCount
                || surface.normals.Length != mesh.vertexCount
            )
                throw new InvalidOperationException(
                    "Eye authoring data does not match the current review mesh."
                );
            if (surface.vertices.Concat(surface.normals).Any(v => !float.IsFinite(v.sqrMagnitude)))
                throw new InvalidOperationException("Non-finite authored geometry.");
            var delta = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
            {
                mesh.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                for (int i = 0; i < delta.Length; i++)
                    if (
                        (surface.vertices[i] - surface.originalVertices[i]).sqrMagnitude > 1e-14f
                        && delta[i].sqrMagnitude > 1e-12f
                    )
                        throw new InvalidOperationException(
                            "Eye edit would affect an animated mouth vertex."
                        );
            }
        }

        private static void Apply(SkinnedMeshRenderer skin, Surface surface, string path)
        {
            var original = skin.sharedMesh;
            var mesh = Object.Instantiate(original);
            mesh.name = original.name + "_EyeShape";
            mesh.vertices = surface.vertices;
            mesh.normals = surface.normals;
            mesh.RecalculateBounds();
            if (
                !mesh.uv.SequenceEqual(original.uv)
                || !mesh.triangles.SequenceEqual(original.triangles)
                || !mesh.boneWeights.SequenceEqual(original.boneWeights)
                || !mesh.bindposes.SequenceEqual(original.bindposes)
                || mesh.blendShapeCount != original.blendShapeCount
            )
                throw new InvalidOperationException(
                    "The eye copy changed source topology or rig data."
                );
            AssetDatabase.CreateAsset(mesh, path);
            skin.sharedMesh = mesh;
        }
    }
}
