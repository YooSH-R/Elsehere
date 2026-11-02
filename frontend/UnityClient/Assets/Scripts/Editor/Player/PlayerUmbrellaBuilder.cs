using System.Collections.Generic;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Player
{
    /// <summary>
    /// 첫날 미션에서 산 우산을 마을 플레이어가 펴 들 수 있게 한다. 우산 모델 프리팹은 없을 때만 만들고,
    /// 마을 플레이어에 <see cref="UmbrellaHold"/>를 붙여 연결한다. 펴고 접는 일은 첫날 미션이 정한다.
    /// </summary>
    public static class PlayerUmbrellaBuilder
    {
        public const string MenuPath = "Tools/A305/Player/Add Umbrella Hold";
        public const string PrefabPath = "Assets/Prefabs/Player/HeldUmbrella.prefab";
        private const string CanopyMeshPath = "Assets/Art/Player/HeldUmbrellaCanopy.asset";
        private const int Ribs = 8;
        private const float CanopyRadius = .84f;
        private const float CanopyHeight = .3f;
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string MaterialFolder = "Assets/Art/World/Drugstore/Materials/";
        private const string MaterialBuilderMenu = "Tools/A305/World/Apply Drugstore Interior";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            GameObject prefab = BuilderTools.CreatePrefabIfMissing(PrefabPath, BuildUmbrella);
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            var hold = BuilderTools.GetOrAddComponent<UmbrellaHold>(player.gameObject);
            BuilderTools.SetReference(hold, "umbrellaPrefab", prefab.GetComponent<HeldUmbrella>());
            BuilderTools.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        // 드럭스토어 진열 우산과 같은 재질로 노란 우산을 만든다. 원점이 손잡이를 쥐는 자리다.
        private static GameObject BuildUmbrella()
        {
            var root = new GameObject("HeldUmbrella");
            Transform t = root.transform;
            Material cloth = Material("DrugstoreUmbrellaF2C230");
            Material steel = Material("DrugstoreDarkSteel");
            Material handle = Material("DrugstoreUmbrellaHandle");

            // 손잡이: 쥐는 자리 아래로 내려와 바깥으로 휘는 J자
            Shape(
                PrimitiveType.Cylinder,
                "Grip",
                t,
                new Vector3(0, -.03f, 0),
                new Vector3(.04f, .07f, .04f),
                handle
            );
            Transform hook = Shape(
                PrimitiveType.Cylinder,
                "Hook",
                t,
                new Vector3(.035f, -.1f, 0),
                new Vector3(.035f, .04f, .035f),
                handle
            );
            hook.localRotation = Quaternion.Euler(0, 0, 90);

            Transform shaft = Shape(
                PrimitiveType.Cylinder,
                "Shaft",
                t,
                new Vector3(0, .5f, 0),
                new Vector3(.028f, .5f, .028f),
                steel
            );

            Transform canopy = BuilderTools.CreateEmpty(
                "Canopy",
                t,
                Vector3.up,
                Quaternion.identity
            );
            var clothObject = new GameObject("Cloth", typeof(MeshFilter), typeof(MeshRenderer));
            clothObject.transform.SetParent(canopy, false);
            clothObject.GetComponent<MeshFilter>().sharedMesh = CanopyMesh();
            clothObject.GetComponent<MeshRenderer>().sharedMaterial = cloth;
            Shape(
                PrimitiveType.Sphere,
                "Tip",
                canopy,
                new Vector3(0, CanopyHeight + .04f, 0),
                new Vector3(.05f, .08f, .05f),
                steel
            );

            var umbrella = root.AddComponent<HeldUmbrella>();
            BuilderTools.SetReference(umbrella, "shaft", shaft);
            BuilderTools.SetReference(umbrella, "canopy", canopy);
            var serialized = new SerializedObject(umbrella);
            serialized.FindProperty("canopyHeight").floatValue = CanopyHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 살 8개 사이 천이 조금씩 처지는 돔. 위아래 모두 보이도록 양면으로 만든다. 원점이 살 끝 높이다.
        private static Mesh CanopyMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(CanopyMeshPath);
            if (existing != null)
                return existing;
            var top = new Vector3(0, CanopyHeight, 0);
            var points = new List<Vector3>();
            for (int i = 0; i < Ribs; i++)
            {
                Vector3 rib = Around(i, 1f);
                Vector3 nextRib = Around(i + 1, 1f);
                Vector3 shoulder = Around(i, .5f) + Vector3.up * CanopyHeight * .74f;
                Vector3 nextShoulder = Around(i + 1, .5f) + Vector3.up * CanopyHeight * .74f;
                // 살 사이 천의 가장자리는 살 끝보다 조금 안쪽·위로 말려 올라간다.
                Vector3 hem = Around(i + .5f, .9f) + Vector3.up * .05f;
                AddTriangle(points, top, nextShoulder, shoulder);
                AddTriangle(points, shoulder, nextShoulder, hem);
                AddTriangle(points, shoulder, hem, rib);
                AddTriangle(points, nextShoulder, nextRib, hem);
            }

            int upper = points.Count;
            for (int i = 0; i < upper; i += 3)
                AddTriangle(points, points[i], points[i + 2], points[i + 1]);
            var mesh = new Mesh { name = "HeldUmbrellaCanopy" };
            mesh.SetVertices(points);
            var triangles = new int[points.Count];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = i;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            BuilderTools.EnsureFolder("Assets/Art/Player");
            AssetDatabase.CreateAsset(mesh, CanopyMeshPath);
            return mesh;
        }

        private static Vector3 Around(float rib, float radiusShare)
        {
            float angle = rib * Mathf.PI * 2f / Ribs;
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle))
                * (CanopyRadius * radiusShare);
        }

        private static void AddTriangle(List<Vector3> points, Vector3 a, Vector3 b, Vector3 c)
        {
            points.Add(a);
            points.Add(b);
            points.Add(c);
        }

        private static Transform Shape(
            PrimitiveType type,
            string shapeName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material
        ) =>
            BuilderTools.CreateShape(
                type,
                shapeName,
                parent,
                position,
                scale,
                material,
                keepCollider: false
            );

        private static Material Material(string materialName) =>
            BuilderTools.LoadRequiredAsset<Material>(
                MaterialFolder + materialName + ".mat",
                MaterialBuilderMenu
            );
    }
}
