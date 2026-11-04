using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Tripo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Renders disposable copies with the same camera and actual shared motion clips.
    public static class ServiceNpcComparisonRenderer
    {
        public static void Render()
        {
            string output = Environment.GetEnvironmentVariable("NPC_REVIEW_OUTPUT");
            foreach (string name in ServiceNpcReviewBuilder.Names)
            {
                string before =
                    $"Assets/Art/Characters/Tripo/NPC/{name}/MasterRig/{name}_Visual.prefab";
                RenderModel(before, Path.Combine(output, name, "Before"));
                RenderModel(
                    ServiceNpcReviewBuilder.Paths(name).Prefab,
                    Path.Combine(output, name, "After")
                );
            }
        }

        private static void RenderModel(string path, string output)
        {
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.position = Vector3.zero;
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshes = skins.Select(_ => new Mesh()).ToArray();
            Bounds Measure()
            {
                Bounds bounds = default;
                bool first = true;
                for (int i = 0; i < skins.Length; i++)
                {
                    skins[i].BakeMesh(meshes[i], true);
                    foreach (var vertex in meshes[i].vertices)
                    {
                        var point = skins[i].transform.TransformPoint(vertex);
                        if (first)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            first = false;
                        }
                        else
                            bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
            var bounds = Measure();
            instance.transform.localScale *= 1.7f / bounds.size.y;
            bounds = Measure();
            instance.transform.position -= Vector3.up * bounds.min.y;
            Vector3 front = TripoModelPreview.GetViewDirection(instance, 0);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            Vector3 focus = Vector3.up * .86f;
            camera.transform.position = focus + front * 5;
            camera.transform.LookAt(focus);
            camera.orthographic = true;
            camera.orthographicSize = 1.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.19f, .21f, .23f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            var light = new GameObject("Key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .6f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var surfaces = skins
                .Select(skin =>
                {
                    var go = new GameObject("Baked review surface");
                    go.transform.SetParent(skin.transform, false);
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    return go;
                })
                .ToArray();
            var target = new RenderTexture(480, 560, 24);
            var image = new Texture2D(480, 560, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            var previous = RenderTexture.active;
            void Save(string filename)
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    skins[i].BakeMesh(meshes[i], true);
                    surfaces[i].GetComponent<MeshFilter>().sharedMesh = meshes[i];
                    skins[i].enabled = false;
                }
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 480, 560), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
            }
            try
            {
                Save("Rest.png");
                using var preview = new TripoMotionPreview(instance);
                foreach (
                    var motion in new[]
                    {
                        TripoReviewMotion.Idle,
                        TripoReviewMotion.Walk,
                        TripoReviewMotion.Run,
                        TripoReviewMotion.Fall,
                    }
                )
                {
                    preview.Apply(motion, 0);
                    int count = Math.Max(2, Mathf.CeilToInt(preview.Duration * 24));
                    for (int frame = 0; frame < count; frame++)
                    {
                        preview.Apply(motion, frame / (float)count);
                        Save($"{motion}_{frame:D3}.png");
                    }
                    File.WriteAllText(
                        Path.Combine(output, motion + ".txt"),
                        $"Clip duration={preview.Duration:R}; samples={count}; playback=24fps"
                    );
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                foreach (var mesh in meshes)
                    Object.DestroyImmediate(mesh);
            }
        }
    }
}
