using System;
using System.IO;
using A305.Characters;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoPrefabBuilder
    {
        public static GameObject Build(GameObject model, string prefabPath, float heightMeters)
        {
            BuilderTools.ThrowIfPlaying();
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(Path.GetDirectoryName(prefabPath));
            Scene preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                GameObject instance = AssetDatabase.Contains(model)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(model, visual.transform)
                    : Object.Instantiate(model, visual.transform);
                instance.name = "Model";
                string modelPath = AssetDatabase.GetAssetPath(model);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    var correctiveProfile = AssetDatabase.LoadAssetAtPath<PoseCorrectiveProfile>(
                        Path.ChangeExtension(modelPath, ".correctives.asset")
                    );
                    if (correctiveProfile != null)
                    {
                        var driver = instance.GetComponent<PoseCorrectiveDriver>();
                        if (driver == null)
                            driver = instance.AddComponent<PoseCorrectiveDriver>();
                        driver.Configure(correctiveProfile);
                    }
                }
                Bounds bounds = GetBounds(root);
                if (
                    bounds.size.y <= 0f
                    || float.IsNaN(bounds.size.y)
                    || float.IsInfinity(bounds.size.y)
                )
                    throw new InvalidOperationException("모델 높이를 계산할 수 없습니다.");
                visual.transform.localScale = Vector3.one * (heightMeters / bounds.size.y);
                bounds = GetBounds(root);
                visual.transform.position -= new Vector3(
                    bounds.center.x,
                    bounds.min.y,
                    bounds.center.z
                );
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Bounds GetBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("모델에 표시할 메시가 없습니다.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
