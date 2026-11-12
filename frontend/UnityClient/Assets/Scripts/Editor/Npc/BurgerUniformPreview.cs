using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    public static class BurgerUniformPreview
    {
        private static string Output =>
            Environment.GetEnvironmentVariable("BURGER_UNIFORM_OUTPUT")
            ?? Path.GetFullPath("../../artifacts/unity/burger-uniform");

        public static void RefitAndCapture()
        {
            BurgerStaffUniformBuilder.RefitMeshes();
            BurgerStaffUniformBuilder.Build();
            Capture();
            CaptureRestaurant();
        }

        public static void Capture()
        {
            string output = Output;
            Directory.CreateDirectory(output);
            foreach (string code in new[] { "DINER_STAFF", "FIRE_CAPTAIN" })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                GameObject actor = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(
                            NpcCast.Find(code).Look.PrefabPath
                        )
                    );
                NpcCastDresser.Dress(actor, NpcCast.Find(code));
                actor
                    .GetComponentInChildren<A305.Characters.CharacterFaceController>()
                    ?.SetPose(A305.Characters.CharacterFacePose.Neutral);
                Bounds bounds = new Bounds();
                bool first = true;
                var snapshots = new List<(SkinnedMeshRenderer skin, Mesh mesh)>();
                foreach (
                    SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()
                )
                {
                    if (!skin.enabled)
                        continue;
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    var snapshot = new GameObject("Snapshot");
                    snapshot.transform.SetParent(skin.transform, false);
                    snapshot.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = snapshot.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    snapshots.Add((skin, mesh));
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 world = skin.transform.TransformPoint(vertex);
                        if (first)
                            bounds = new Bounds(world, Vector3.zero);
                        else
                            bounds.Encapsulate(world);
                        first = false;
                    }
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.18f, .2f, .23f);
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.y * 1.32f;
                var light = new GameObject("KeyLight").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(30, 200, 0);
                UnityEngine.Rendering.AmbientMode mode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
                foreach (int angle in new[] { 0, 45, 90, 180 })
                {
                    Vector3 front = A305.EditorTools.Tripo.TripoModelPreview.GetViewDirection(
                        actor,
                        0
                    );
                    camera.transform.position =
                        bounds.center
                        + Quaternion.Euler(0, angle, 0) * front * 4
                        + Vector3.up * .12f;
                    camera.transform.LookAt(bounds.center);
                    light.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .6f);
                    Save(camera, Path.Combine(output, code + "-" + angle + ".png"), 900, 900);
                }
                using (var preview = new TripoMotionPreview(actor))
                {
                    foreach (
                        var pose in new[]
                        {
                            (TripoReviewMotion.Idle, .5f),
                            (TripoReviewMotion.Walk, .2f),
                            (TripoReviewMotion.Walk, .7f),
                        }
                    )
                    {
                        preview.Apply(pose.Item1, pose.Item2);
                        foreach (var snapshot in snapshots)
                            snapshot.skin.BakeMesh(snapshot.mesh, true);
                        Vector3 front = BuilderTools.GetCharacterViewDirection(actor, 0);
                        camera.transform.position =
                            bounds.center + Quaternion.Euler(0, 30, 0) * front * 4;
                        camera.transform.LookAt(bounds.center);
                        light.transform.rotation = Quaternion.LookRotation(
                            -front + Vector3.down * .6f
                        );
                        Save(
                            camera,
                            Path.Combine(
                                output,
                                code + "-" + pose.Item1 + "-" + pose.Item2 + ".png"
                            ),
                            900,
                            900
                        );
                    }
                }
                foreach (var snapshot in snapshots)
                    Object.DestroyImmediate(snapshot.mesh);
            }
        }

        public static void CaptureRestaurant()
        {
            string output = Output;
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/ShopInteriors/MapleDinerInterior.unity"
            );
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(false);
            foreach (var player in BuilderTools.FindAllInScene<A305.Player.PlayerMover>(scene))
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
            var staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Where(npc => BurgerStaffUniformBuilder.StaffCodes.Contains(npc.Code))
                .ToArray();
            foreach (var npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                if (!staff.Contains(npc))
                    npc.gameObject.SetActive(false);
            var building = BuilderTools
                .FindAllInScene<Transform>(scene)
                .Single(item => item.name == "MapleDiner" && item.Find("Interior") != null);
            Camera camera = BuilderTools
                .FindAllInScene<Camera>(scene)
                .First(item => item.CompareTag("MainCamera"));
            camera.fieldOfView = 44;
            foreach (var actor in staff)
            {
                foreach (var worker in staff)
                    worker.gameObject.SetActive(worker == actor);
                Animator animator = actor.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Update(0);
                var snapshots = new List<GameObject>();
                var meshes = new List<Mesh>();
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled)
                        continue;
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    var snapshot = new GameObject("UniformPreviewSnapshot");
                    snapshot.transform.SetParent(skin.transform, false);
                    snapshot.AddComponent<MeshFilter>().sharedMesh = mesh;
                    snapshot.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                    snapshots.Add(snapshot);
                    meshes.Add(mesh);
                }
                camera.transform.position = building.TransformPoint(new Vector3(.4f, 2.25f, -3.8f));
                camera.transform.LookAt(actor.transform.position + Vector3.up * 1.05f);
                Save(camera, Path.Combine(output, actor.Code + "-Counter.png"), 1600, 1000);
                Save(camera, Path.Combine(output, actor.Code + "-Counter-Mobile.png"), 844, 390);
                foreach (var snapshot in snapshots)
                    Object.DestroyImmediate(snapshot);
                foreach (var mesh in meshes)
                    Object.DestroyImmediate(mesh);
            }
        }

        internal static void Save(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            // batchmode 첫 프레임의 재질 업로드가 끝난 뒤 같은 장면을 읽는다.
            camera.Render();
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }
}
