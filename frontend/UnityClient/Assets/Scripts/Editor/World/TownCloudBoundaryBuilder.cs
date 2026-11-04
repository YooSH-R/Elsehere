using System;
using System.IO;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>동서 확장 경계를 가리는 구름과 공간 안내판. 기존 통행 충돌체를 유지한다.</summary>
    public static class TownCloudBoundaryBuilder
    {
        private const string Folder = "Assets/Prefabs/World/CloudBoundaries";
        private const string RootName = "FutureDistrictClouds";

        [MenuItem("Tools/A305/World/Apply Cloud Boundaries")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.EnsureFolder(Folder);
            GameObject clouds = BuilderTools.CreatePrefabIfMissing(
                Folder + "/CloudBank.prefab",
                CreateClouds
            );
            GameObject airport = BuilderTools.CreatePrefabIfMissing(
                Folder + "/AirportBoundary.prefab",
                () => CreateBoundary(clouds, "공항", true)
            );
            GameObject park = BuilderTools.CreatePrefabIfMissing(
                Folder + "/ParkBoundary.prefab",
                () => CreateBoundary(clouds, "놀이공원", false)
            );
            var scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Transform environment = TownProductionBuilder.Root(scene, "Environment");
            Transform root = environment.Find(RootName);
            bool created = root == null;
            if (created)
            {
                root = Group(RootName, environment, Vector3.zero);
                BuilderTools.InstantiatePrefab(
                    airport,
                    root,
                    new Vector3(-18.5f, .1f, 10),
                    Quaternion.Euler(0, 90, 0)
                );
                BuilderTools.InstantiatePrefab(
                    park,
                    root,
                    new Vector3(18.5f, .1f, 10),
                    Quaternion.Euler(0, 270, 0)
                );
            }
            Transform airportBoundary = root.Find("AirportBoundary");
            Transform parkBoundary = root.Find("ParkBoundary");
            if (airportBoundary == null || parkBoundary == null)
                throw new InvalidOperationException("개발 예정 지역은 두 곳이어야 합니다.");

            bool expanded = root.Find("DistrictCloudCoverage") == null;
            if (expanded)
                AddDistrictCoverage(root, airportBoundary, parkBoundary);
            bool addedSkyNotices = root.Find("DistrictSkyNotices") == null;
            if (addedSkyNotices)
                AddSkyNotices(root);
            if (created || expanded || addedSkyNotices)
                BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);

            Physics.SyncTransforms();
            foreach (int side in new[] { -1, 1 })
            {
                if (
                    !Physics.Raycast(
                        new Vector3(side * 14, 1, 10),
                        Vector3.right * side,
                        7,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    )
                )
                    throw new InvalidOperationException("동서 경계 충돌체가 없습니다.");
            }
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.font.HasCharacters(label.text, out uint[] missing, true, true))
                    throw new InvalidOperationException("안내판 한글 글리프가 누락되었습니다.");
            }
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Capture(new Vector3(0, 3.3f, 10), Vector3.left, "airport");
            Capture(new Vector3(0, 3.3f, 10), Vector3.right, "park");
            Capture(new Vector3(2, 3.1f, -17), new Vector3(-1, .34f, 0), "home-airport");
            Capture(new Vector3(2, 3.1f, -17), new Vector3(1, .34f, 0), "home-park");
            Capture(new Vector3(0, 3.1f, 47), new Vector3(-1, .72f, .7f), "north-airport");
            Capture(new Vector3(0, 3.1f, 47), new Vector3(1, .72f, 0), "north-park");
            Capture(new Vector3(0, 3.5f, -1), Vector3.forward, "north");
            Capture(new Vector3(0, 50, -44), new Vector3(0, -.75f, 1), "coverage");
            Debug.Log("확장 구름, 공중 안내판 한글 및 기존 경계 충돌 확인 완료.");
        }

        private static void AddDistrictCoverage(Transform root, Transform airport, Transform park)
        {
            Transform coverage = Group("DistrictCloudCoverage", root, Vector3.zero);
            GameObject tile = BuilderTools.CreatePrefabIfMissing(
                Folder + "/DistrictCloudTile.prefab",
                CreateCloudTile
            );
            foreach (int side in new[] { -1, 1 })
            {
                Transform boundary = side < 0 ? airport : park;
                Transform sign = boundary.Find("FloatingNotice");
                sign.localPosition = new Vector3(0, 9.3f, -1f);
                sign.localScale = Vector3.one * 1.4f;

                Transform district = Group(boundary.name + "Coverage", coverage, Vector3.zero);
                // SideBoundary 길이(-32~70m)를 덮고, 뒤에 한 겹 더 두어 빈 지평선을 가린다.
                for (int index = 0; index < 6; index++)
                {
                    float z = -27 + index * 19;
                    BuilderTools.InstantiatePrefab(
                        tile,
                        district,
                        new Vector3(side * 27, 0, z),
                        Quaternion.Euler(0, side * 90, 0)
                    );
                    Transform rear = BuilderTools
                        .InstantiatePrefab(
                            tile,
                            district,
                            new Vector3(side * 41, .7f, z + 5),
                            Quaternion.Euler(0, -side * 90, 0)
                        )
                        .transform;
                    rear.localScale = new Vector3(1.18f, 1.48f, 1.1f);
                }
            }
        }

        private static GameObject CreateCloudTile()
        {
            // 기존 cloud 연결 이름을 사용하고, 타일당 한 메시로 묶어 반복 배치한다.
            var tile = new GameObject("CloudBank");
            Material white = Color("BoundaryCloudWhite", "#F4F6FF");
            SmoothBall(
                "LowBank",
                tile.transform,
                new Vector3(0, 1.7f, 0),
                new Vector3(25, 5.5f, 15.2f),
                white
            );
            SmoothBall(
                "WestPuff",
                tile.transform,
                new Vector3(-6.3f, 2.9f, .4f),
                new Vector3(12, 7, 13),
                white
            );
            SmoothBall(
                "HighPuff",
                tile.transform,
                new Vector3(1.3f, 4, -1.6f),
                new Vector3(11, 8.8f, 12),
                white
            );
            SmoothBall(
                "EastPuff",
                tile.transform,
                new Vector3(7.8f, 2.8f, -.3f),
                new Vector3(10.2f, 7, 12.8f),
                white
            );
            CombineParts(tile, "DistrictCloudTile");
            foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return tile;
        }

        private static void AddSkyNotices(Transform root)
        {
            Transform notices = Group("DistrictSkyNotices", root, Vector3.zero);
            foreach (int side in new[] { -1, 1 })
            {
                bool airport = side < 0;
                string name = airport ? "AirportSkyNotice" : "ParkSkyNotice";
                string district = airport ? "공항" : "놀이공원";
                GameObject prefab = BuilderTools.CreatePrefabIfMissing(
                    Folder + "/" + name + ".prefab",
                    () =>
                        CreateNotice(
                            null,
                            Vector3.zero,
                            district,
                            airport,
                            SkyNoticeBacking()
                        ).gameObject
                );
                float yaw = airport ? 90 : 270;
                // 집 앞과 북쪽 거리에서도 가까운 구간의 구름 위로 안내판을 볼 수 있게 한다.
                foreach (
                    Vector3 position in new[]
                    {
                        new Vector3(side * 23, 15, -17),
                        new Vector3(side * 20, 29, airport ? 61 : 47),
                    }
                )
                {
                    Quaternion faceTown = Quaternion.Euler(position.z > 40 ? 25 : 0, yaw, 0);
                    GameObject notice = BuilderTools.InstantiatePrefab(
                        prefab,
                        notices,
                        position,
                        faceTown
                    );
                    notice.transform.localScale = Vector3.one * 1.65f;
                }
            }
        }

        private static GameObject CreateClouds()
        {
            var root = new GameObject("CloudBank");
            Material white = Color("BoundaryCloudWhite", "#F4F6FF");
            white.EnableKeyword("_EMISSION");
            white.SetColor("_EmissionColor", new UnityEngine.Color(.25f, .27f, .3f));
            // 큰 덩어리를 겹쳐 지면부터 가린다. 충돌은 기존 경계가 맡는다.
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 2.2f;
                float height = 4.2f + (i * 3 % 5) * .35f;
                SmoothBall(
                    "CloudVolume",
                    root.transform,
                    new Vector3(x, height * .36f, (i % 3) * .45f),
                    new Vector3(5.2f, height, 5),
                    white
                );
                SmoothBall(
                    "CloudCrown",
                    root.transform,
                    new Vector3(x + .6f, 2.9f + (i * 2 % 5) * .35f, -.8f),
                    new Vector3(4.4f, 3.6f + (i % 3) * .55f, 4.5f),
                    white
                );
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return root;
        }

        private static GameObject CreateBoundary(GameObject clouds, string district, bool airport)
        {
            var root = new GameObject(airport ? "AirportBoundary" : "ParkBoundary");
            BuilderTools.InstantiatePrefab(
                clouds,
                root.transform,
                Vector3.zero,
                Quaternion.identity
            );
            CreateNotice(root.transform, new Vector3(0, 7.2f, 1), district, airport);
            return root;
        }

        private static Material SkyNoticeBacking()
        {
            const string name = "SkyNoticeCream";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(
                ArtFolder + "/" + name + ".mat"
            );
            if (existing != null)
                return existing;
            Material material = Color(name, "#FFF0D3");
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new UnityEngine.Color(.48f, .4f, .29f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CreateNotice(
            Transform parent,
            Vector3 position,
            string district,
            bool airport,
            Material backing = null
        )
        {
            Transform sign = Group("FloatingNotice", parent, position);
            Material cream = backing == null ? Color("BoundaryNoticeCream", "#FFF0D3") : backing;
            Material ink = Color("BoundaryNoticeInk", "#185F64");
            Block("NoticeBacking", sign, Vector3.zero, new Vector3(6.4f, 2.7f, .35f), cream, .16f);
            TextMeshPro label = Text(
                "DistrictLabel",
                district + "\n개발중",
                sign,
                new Vector3(-.85f, 0, .2f),
                .72f,
                cream
            );
            label.color = ink.color;
            label.rectTransform.sizeDelta = new Vector2(4.2f, 2.3f);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Jua-Regular SDF.asset"
            );
            Transform icon = Group("DistrictIcon", sign, new Vector3(2, 0, .27f));
            if (airport)
            {
                Block("PlaneBody", icon, Vector3.zero, new Vector3(.22f, 1.7f, .14f), ink);
                Transform wing = Block(
                    "PlaneWings",
                    icon,
                    new Vector3(0, .15f, 0),
                    new Vector3(1.55f, .25f, .14f),
                    ink
                );
                wing.localEulerAngles = new Vector3(0, 0, -12);
                Block(
                    "PlaneTail",
                    icon,
                    new Vector3(0, -.6f, 0),
                    new Vector3(.7f, .18f, .14f),
                    ink
                );
                icon.localEulerAngles = new Vector3(0, 0, -35);
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = (i + .5f) * Mathf.PI / 8;
                    Transform rim = Block(
                        "WheelRim",
                        icon,
                        new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .72f,
                        new Vector3(.08f, .3f, .08f),
                        ink
                    );
                    rim.localEulerAngles = new Vector3(0, 0, (i + .5f) * 22.5f);
                }
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .72f;
                    Transform spoke = Block(
                        "WheelSpoke",
                        icon,
                        point * .5f,
                        new Vector3(.075f, .72f, .08f),
                        ink
                    );
                    spoke.localEulerAngles = new Vector3(0, 0, i * 45 - 90);
                    SmoothBall("WheelCabin", icon, point, new Vector3(.25f, .25f, .15f), ink);
                }
                Block(
                    "WheelStand",
                    icon,
                    new Vector3(0, -.65f, 0),
                    new Vector3(.15f, .8f, .12f),
                    ink
                );
            }
            return sign;
        }

        private static void Capture(Vector3 position, Vector3 direction, string name)
        {
            string output = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/cloud-boundaries")
            );
            Directory.CreateDirectory(output);
            GameObject cameraObject = new GameObject("CloudPreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            camera.fieldOfView = 60;
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
