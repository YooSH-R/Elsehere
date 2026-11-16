using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.World
{
    public static class BarberChairFitBuilder
    {
        public const string ScenePath =
            "Assets/Scenes/ShopInteriors/MainStreetBarberInterior.unity";
        private const float ModelCushionHeight = .48f;
        private const float CushionHeight = .64f;

        [MenuItem("Tools/A305/World/Fit Barber Chairs To Little Mung")]
        public static void Build()
        {
            BuilderTools.UpdatePrefabIfExists(
                "Assets/Prefabs/World/TownStorefronts/MainStreetBarber.prefab",
                root =>
                {
                    foreach (
                        Transform chair in root.GetComponentsInChildren<Transform>()
                            .Where(node =>
                                node.name == "BarberChair" && node.Find("TripoVisual") != null
                            )
                    )
                        Fit(chair);
                }
            );
            var scene = EditorSceneManager.OpenScene(ScenePath);
            BuilderTools.SaveScene(scene, ScenePath);
            NpcLifeBuilder.BuildScene(ScenePath);
            AssetDatabase.SaveAssets();
        }

        internal static void Fit(Transform chair)
        {
            Transform visual = chair.Find("TripoVisual");
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            float height = CushionHeight / ModelCushionHeight * Mathf.Abs(chair.lossyScale.y);
            visual.localScale *= height / bounds.size.y;
            Vector3 ground = chair.position - Vector3.up * .55f * Mathf.Abs(chair.lossyScale.y);
            visual.position = ground - visual.rotation * new Vector3(-.000565f, 0, -.009f) * height;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            if (chair.Find("ChairBodyCollision") != null)
                return;
            var collision = new GameObject("ChairBodyCollision").transform;
            collision.SetParent(chair, false);
            collision.localPosition = Vector3.down * .55f;
            collision.localRotation = Quaternion.Euler(0, -90, 0);
            Box(collision, "Back", new Vector3(0, .98f, -.32f), new Vector3(.75f, .65f, .14f));
            foreach (int side in new[] { -1, 1 })
                Box(
                    collision,
                    side < 0 ? "LeftArm" : "RightArm",
                    new Vector3(side * .4f, .82f, 0),
                    new Vector3(.12f, .12f, .6f)
                );
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var collider = new GameObject(name).AddComponent<BoxCollider>();
            collider.transform.SetParent(parent, false);
            collider.center = position;
            collider.size = size;
        }
    }
}
