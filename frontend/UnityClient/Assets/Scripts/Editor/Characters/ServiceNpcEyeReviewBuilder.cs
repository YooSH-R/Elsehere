using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Characters
{
    // Restores the original eye appearance on the authored review prefabs.
    public static class ServiceNpcEyeReviewBuilder
    {
        public static void RenderReview()
        {
            ServiceNpcReviewBuilder.RenderFaces();
            ServiceNpcComparisonRenderer.Render();
            ServiceNpcReviewBuilder.ValidateAndScenes();
        }

        public static void RestoreOriginalEyes()
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
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(paths.Source);
                var original = source
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var instance = PrefabUtility.LoadPrefabContents(paths.Prefab);
                try
                {
                    var body = instance
                        .GetComponentsInChildren<SkinnedMeshRenderer>()
                        .OrderByDescending(s => s.sharedMesh.vertexCount)
                        .First();
                    var mesh = body.sharedMesh;
                    body.sharedMaterials = original.sharedMaterials;
                    if (body.sharedMesh != mesh)
                        throw new IOException("Restoring eyes must not replace the face mesh.");
                    PrefabUtility.SaveAsPrefabAsset(instance, paths.Prefab);
                    Debug.Log(
                        name + ": original eye material restored; face mesh and mouth retained."
                    );
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
