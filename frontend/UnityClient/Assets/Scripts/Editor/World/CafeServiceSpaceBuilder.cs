using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Npc;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>카페 앞 매대를 옮겨 직원 두 명이 교차할 작업 통로를 확보한다.</summary>
    public static class CafeServiceSpaceBuilder
    {
        private const float AisleWidth = 2.4f;

        [MenuItem("Tools/A305/Cafe/Widen Staff Aisle")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(CafeInteriorBuilder.ScenePath);
            var roots = scene.GetRootGameObjects();
            var objects = roots
                .SelectMany(root => root.GetComponentsInChildren<HoverableObject>())
                .ToArray();
            Transform counter = objects.Single(item => item.name == "ServiceCounter").transform;
            Transform backBar = objects.Single(item => item.name == "BackBar").transform;
            Bounds before = BoundsOf(counter);
            float backEdge = BoundsOf(backBar).min.z;
            float shift = Mathf.Min(0f, backEdge - before.max.z - AisleWidth);
            if (shift < -.001f)
            {
                // 별도 배치된 상판 소품은 묶음 루트만 옮겨 중복 이동을 막는다.
                var props = roots
                    .SelectMany(root => root.GetComponentsInChildren<Renderer>())
                    .Where(part => !part.transform.IsChildOf(counter) && OnTop(part.bounds, before))
                    .Select(part => PropRoot(part.transform, before))
                    .Distinct()
                    .ToArray();
                foreach (Transform prop in props)
                    Move(prop, shift);
                Move(counter, shift);

                // 계산 담당의 기존 응대 위치도 앞 매대와 함께 이동한다. 양옆 직원 출입 위치는 유지한다.
                foreach (
                    var npc in roots.SelectMany(root => root.GetComponentsInChildren<NpcIdentity>())
                )
                {
                    Vector3 position = npc.transform.position;
                    if (
                        position.x > before.min.x
                        && position.x < before.max.x
                        && position.z > before.max.z
                        && position.z < backEdge
                    )
                        Move(npc.transform, shift);
                }
            }
            Physics.SyncTransforms();
            NpcLifeBuilder.RebakeWalkArea(scene);
            AssetDatabase.SaveAssets();
            ValidateLanes(BoundsOf(counter).max.z, backEdge);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"카페 직원 통로: {backEdge - before.max.z:F2}m → {backEdge - BoundsOf(counter).max.z:F2}m"
            );
        }

        private static void ValidateLanes(float front, float back)
        {
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(
                "Assets/Data/Npc/WalkAreas/CafeSceneWalkArea.asset"
            );
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                // 두 캐릭터 중심 사이 1.2m를 남긴 두 경로가 카운터 양 끝까지 끊기지 않는지 확인한다.
                foreach (float fraction in new[] { .25f, .75f })
                {
                    float z = Mathf.Lerp(front, back, fraction);
                    Vector3 start = new Vector3(-3f, .05f, z);
                    Vector3 end = new Vector3(3f, .05f, z);
                    if (
                        !NavMesh.SamplePosition(start, out var from, .2f, NavMesh.AllAreas)
                        || !NavMesh.SamplePosition(end, out var to, .2f, NavMesh.AllAreas)
                        || NavMesh.Raycast(from.position, to.position, out _, NavMesh.AllAreas)
                    )
                        throw new InvalidOperationException(
                            "직원 통로의 나란한 이동 경로가 가구에 막혔습니다."
                        );
                }
                Debug.Log("카페 직원 두 명의 나란한 6m 이동 경로 확인 완료");
            }
            finally
            {
                instance.Remove();
            }
        }

        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(CafeInteriorBuilder.ScenePath);
            var roots = scene.GetRootGameObjects();
            foreach (var canvas in roots.SelectMany(root => root.GetComponentsInChildren<Canvas>()))
                canvas.gameObject.SetActive(false);
            foreach (
                var player in roots.SelectMany(root => root.GetComponentsInChildren<PlayerMover>())
            )
            foreach (var renderer in player.GetComponentsInChildren<Renderer>())
                renderer.forceRenderingOff = true;
            Camera camera = roots
                .SelectMany(root => root.GetComponentsInChildren<Camera>())
                .First();
            Vector3 focus = new Vector3(0, .4f, 3.6f);
            Vector3 eye = new Vector3(.4f, 3.8f, -3.2f);
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                string folder = Path.GetFullPath("../../artifacts/counter-664");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(
                    Path.Combine(folder, "CafeStaffAisle.png"),
                    pixels.EncodeToPNG()
                );
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Move(Transform target, float shift)
        {
            target.position += Vector3.forward * shift;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static bool OnTop(Bounds prop, Bounds counter) =>
            Mathf.Abs(prop.min.y - counter.max.y) < .15f
            && prop.center.x >= counter.min.x
            && prop.center.x <= counter.max.x
            && prop.center.z >= counter.min.z
            && prop.center.z <= counter.max.z
            && prop.size.y < 1f;

        private static Transform PropRoot(Transform part, Bounds counter)
        {
            while (part.parent != null && OnTop(BoundsOf(part.parent), counter))
                part = part.parent;
            return part;
        }

        private static Bounds BoundsOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
