using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Characters
{
    // Applies authored eye textures to review copies without changing the source material.
    public static class ServiceNpcEyeReviewBuilder
    {
        public static void RenderReview()
        {
            ServiceNpcReviewBuilder.RenderFaces();
            ServiceNpcComparisonRenderer.Render();
            ServiceNpcReviewBuilder.ValidateAndScenes();
        }

        public static void Apply()
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
                string texturePath = paths.Art + "/CharacterColor.png";
                string materialPath = paths.Art + "/CharacterColor.mat";
                if (File.Exists(texturePath) || File.Exists(materialPath))
                    throw new IOException("Preserve the existing authored eye copy: " + name);
                File.Copy(Path.Combine(paths.Output, "CharacterColor.png"), texturePath, false);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var instance = PrefabUtility.LoadPrefabContents(paths.Prefab);
                try
                {
                    var body = instance
                        .GetComponentsInChildren<SkinnedMeshRenderer>()
                        .OrderByDescending(s => s.sharedMesh.vertexCount)
                        .First();
                    var material = new Material(body.sharedMaterial)
                    {
                        name = name + "_CharacterColor",
                    };
                    material.SetTexture(
                        "_BaseMap",
                        AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                    );
                    AssetDatabase.CreateAsset(material, materialPath);
                    body.sharedMaterial = material;
                    PrefabUtility.SaveAsPrefabAsset(instance, paths.Prefab);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
            AssetDatabase.SaveAssets();
            ServiceNpcReviewBuilder.RenderFaces();
        }
    }
}
