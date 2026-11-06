using System.IO;
using System.Linq;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.World
{
    /// <summary>직원의 상반신이 드러나도록 서비스 매대와 그 위 소품을 함께 낮춘다.</summary>
    public static class ServiceCounterHeightBuilder
    {
        private const float Height = .45f;

        [MenuItem("Tools/A305/World/Lower Service Counters")]
        public static void Apply()
        {
            foreach (string path in Directory.GetFiles("Assets/Scenes/Production", "*.unity"))
            {
                var scene = EditorSceneManager.OpenScene(path);
                var roots = scene.GetRootGameObjects();
                var counters = roots
                    .SelectMany(root => root.GetComponentsInChildren<HoverableObject>(true))
                    .Where(item => item.UnityKey == "counter" || item.UnityKey == "reception_desk")
                    .ToArray();
                bool changed = false;
                foreach (var counter in counters)
                {
                    var renderers = counter.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0)
                        continue;
                    Bounds bounds = BoundsOf(counter.transform);
                    // 직원의 응대 자리 앞 매대만 바꾼다. 주방·커피 머신 진열장은 제외한다.
                    bool hasStaff = roots
                        .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                        .Any(npc =>
                            Vector3.Distance(
                                bounds.ClosestPoint(
                                    new Vector3(
                                        npc.transform.position.x,
                                        bounds.center.y,
                                        npc.transform.position.z
                                    )
                                ),
                                new Vector3(
                                    npc.transform.position.x,
                                    bounds.center.y,
                                    npc.transform.position.z
                                )
                            ) <= 1.5f
                        );
                    if (!hasStaff)
                        continue;
                    if (bounds.size.y <= Height + .01f)
                        continue;
                    // 상판에 놓인 별도 소품은 크기를 유지하고 상판이 낮아진 만큼 내린다.
                    var props = roots
                        .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                        .Where(renderer =>
                            !renderer.transform.IsChildOf(counter.transform)
                            && OnTop(renderer.bounds, bounds)
                        )
                        .Select(renderer => PropRoot(renderer.transform, bounds))
                        .Distinct()
                        .ToArray();
                    float delta = Height - bounds.size.y;
                    foreach (Transform prop in props)
                    {
                        prop.position += Vector3.up * delta;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
                    }
                    Transform target = counter.transform;
                    float ratio = Height / bounds.size.y;
                    Vector3 scale = target.localScale;
                    scale.y *= ratio;
                    target.localScale = scale;
                    target.position += Vector3.up * (bounds.min.y - BoundsOf(target).min.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    Debug.Log(
                        $"매대 낮춤: {path}/{counter.name}, {bounds.size.y:F2}m → {Height:F2}m, 소품 {props.Length}개"
                    );
                    changed = true;
                }
                if (changed)
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        public static void Capture()
        {
            string folder = Path.GetFullPath("../../artifacts/counter-664");
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { "CafeScene", "ShopScene", "HotelScene" })
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Production/{name}.unity");
                var roots = scene.GetRootGameObjects();
                foreach (
                    Canvas canvas in roots.SelectMany(root =>
                        root.GetComponentsInChildren<Canvas>(true)
                    )
                )
                    canvas.gameObject.SetActive(false);
                foreach (
                    PlayerMover player in roots.SelectMany(root =>
                        root.GetComponentsInChildren<PlayerMover>(true)
                    )
                )
                foreach (Renderer part in player.GetComponentsInChildren<Renderer>(true))
                    part.forceRenderingOff = true;
                var npc = roots
                    .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>())
                    .First();
                var camera = roots
                    .SelectMany(root => root.GetComponentsInChildren<Camera>())
                    .First();
                Vector3 face = npc.LookTarget.position;
                // 승인된 !501 카메라 구도에서 상반신 가림을 함께 확인한다.
                Pose pose = DialogueFraming.Pose(
                    face + npc.transform.forward * 2.4f,
                    face,
                    camera.fieldOfView,
                    16f / 9f,
                    new Vector2(.5f, .66f)
                );
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                var target = new RenderTexture(1600, 900, 24);
                camera.targetTexture = target;
                camera.aspect = 16f / 9f;
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
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
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
