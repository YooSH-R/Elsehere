using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    // 로컬 복사본의 손을 같은 구도로 비교한다. 원본 에셋을 저장하거나 변경하지 않는다.
    public static class TripoHandReview
    {
        [Serializable]
        private sealed class Sample
        {
            public string model,
                motion,
                side;
            public Vector3[] vertices,
                joints;
            public string[] bones;
            public Quaternion[] rotations;
            public BoneWeight[] weights;
            public int[] selected;
        }

        [Serializable]
        private sealed class Result
        {
            public List<Sample> samples = new();
        }

        public static void Render()
        {
            string output = Environment.GetEnvironmentVariable("TRIPO_HAND_OUTPUT");
            string[] paths = Environment.GetEnvironmentVariable("TRIPO_HAND_MODELS").Split('|');
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
            var camera = new GameObject("Hand review camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.17f, .19f, .21f);
            camera.nearClipPlane = .001f;
            camera.farClipPlane = 20;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .6f;
            var result = new Result();
            foreach (string path in paths)
            foreach (
                var motion in new[]
                {
                    TripoReviewMotion.Rest,
                    TripoReviewMotion.Idle,
                    TripoReviewMotion.Run,
                }
            )
            foreach (bool left in new[] { true, false })
            {
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var animator = model.GetComponentInChildren<Animator>();
                float height = Vector3.Distance(
                    animator.GetBoneTransform(HumanBodyBones.Head).position,
                    animator.GetBoneTransform(HumanBodyBones.Hips).position
                );
                model.transform.localScale *= .55f / height;
                var meshes = new List<Mesh>();
                try
                {
                    var hand = animator.GetBoneTransform(
                        left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
                    );
                    var index = animator.GetBoneTransform(
                        left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal
                    );
                    var little = animator.GetBoneTransform(
                        left
                            ? HumanBodyBones.LeftLittleProximal
                            : HumanBodyBones.RightLittleProximal
                    );
                    var group = hand.GetComponentsInChildren<Transform>().ToHashSet();
                    using var preview = new TripoMotionPreview(model);
                    if (motion != TripoReviewMotion.Rest)
                        preview.Apply(motion, .35f);
                    var all = new List<Vector3>();
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh = Object.Instantiate(skin.sharedMesh);
                        meshes.Add(mesh);
                        var weights = mesh.boneWeights;
                        var selected = Enumerable
                            .Range(0, weights.Length)
                            .Where(i => Influence(weights[i], skin, group) > .5f)
                            .ToHashSet();
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            var triangles = mesh.GetTriangles(sub);
                            var kept = new List<int>();
                            for (int i = 0; i < triangles.Length; i += 3)
                                if (
                                    selected.Contains(triangles[i])
                                    && selected.Contains(triangles[i + 1])
                                    && selected.Contains(triangles[i + 2])
                                )
                                    kept.AddRange(
                                        new[] { triangles[i], triangles[i + 1], triangles[i + 2] }
                                    );
                            if (Environment.GetEnvironmentVariable("TRIPO_HAND_FULL") != "1")
                                mesh.SetTriangles(kept, sub);
                        }
                        skin.sharedMesh = mesh;
                        skin.updateWhenOffscreen = true;
                        var matrices = skin
                            .bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i])
                            .ToArray();
                        var points = mesh
                            .vertices.Select(
                                (v, i) =>
                                {
                                    var w = weights[i];
                                    return matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                                        + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                                        + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                                        + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                                }
                            )
                            .ToArray();
                        skin.enabled = false;
                        var surface = new GameObject("Evaluated hand surface");
                        surface.transform.SetParent(model.transform, true);
                        mesh.vertices = points;
                        var normalMatrices = matrices
                            .Select(matrix => matrix.inverse.transpose)
                            .ToArray();
                        mesh.normals = mesh
                            .normals.Select(
                                (normal, i) =>
                                {
                                    var w = weights[i];
                                    return (
                                        normalMatrices[w.boneIndex0].MultiplyVector(normal)
                                            * w.weight0
                                        + normalMatrices[w.boneIndex1].MultiplyVector(normal)
                                            * w.weight1
                                        + normalMatrices[w.boneIndex2].MultiplyVector(normal)
                                            * w.weight2
                                        + normalMatrices[w.boneIndex3].MultiplyVector(normal)
                                            * w.weight3
                                    ).normalized;
                                }
                            )
                            .ToArray();
                        mesh.RecalculateBounds();
                        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                        surface.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                        all.AddRange(selected.Select(i => points[i]));
                        result.samples.Add(
                            new Sample
                            {
                                model = path,
                                motion = motion.ToString(),
                                side = left ? "Left" : "Right",
                                vertices = points,
                                bones = skin.bones.Select(b => b.name).ToArray(),
                                joints = skin.bones.Select(b => b.position).ToArray(),
                                rotations = skin.bones.Select(b => b.rotation).ToArray(),
                                weights = weights,
                                selected = selected.OrderBy(i => i).ToArray(),
                            }
                        );
                    }
                    if (all.Count < 3)
                        throw new InvalidOperationException("손 정점이 부족합니다: " + path);
                    var bounds = new Bounds(all[0], Vector3.zero);
                    foreach (var point in all)
                        bounds.Encapsulate(point);
                    Vector3 up = (
                        (index.position + little.position) * .5f - hand.position
                    ).normalized;
                    Vector3 normal = Vector3.Cross(index.position - little.position, up).normalized;
                    if (!left)
                        normal = -normal;
                    for (int view = 0; view < 3; view++)
                    {
                        Vector3 direction =
                            Quaternion.AngleAxis(
                                view == 0 ? 0
                                    : view == 1 ? 180
                                    : 90,
                                up
                            ) * normal;
                        camera.orthographicSize = bounds.size.magnitude * .58f;
                        camera.transform.position = bounds.center + direction * 2;
                        camera.transform.rotation = Quaternion.LookRotation(-direction, up);
                        key.transform.rotation =
                            camera.transform.rotation * Quaternion.Euler(25, -30, 0);
                        fill.transform.rotation =
                            camera.transform.rotation * Quaternion.Euler(-20, 45, 0);
                        Save(
                            camera,
                            Path.Combine(
                                output,
                                $"{Array.IndexOf(paths, path)}-{motion}-{(left ? "Left" : "Right")}-{view}.png"
                            )
                        );
                    }
                }
                finally
                {
                    Object.DestroyImmediate(model);
                    foreach (var mesh in meshes)
                        Object.DestroyImmediate(mesh);
                }
            }
            File.WriteAllText(Path.Combine(output, "samples.json"), JsonUtility.ToJson(result));
        }

        private static float Influence(
            BoneWeight w,
            SkinnedMeshRenderer skin,
            HashSet<Transform> group
        ) =>
            (group.Contains(skin.bones[w.boneIndex0]) ? w.weight0 : 0)
            + (group.Contains(skin.bones[w.boneIndex1]) ? w.weight1 : 0)
            + (group.Contains(skin.bones[w.boneIndex2]) ? w.weight2 : 0)
            + (group.Contains(skin.bones[w.boneIndex3]) ? w.weight3 : 0);

        private static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(512, 512, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
