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
            if (root == null)
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
                BuilderTools.SaveScene(scene, TownProductionBuilder.ScenePath);
            }
            if (root.childCount != 2)
                throw new InvalidOperationException("개발 예정 지역은 두 곳이어야 합니다.");
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
            foreach (TextMeshPro label in root.GetComponentsInChildren<TextMeshPro>())
            {
                if (!label.font.HasCharacters(label.text, out uint[] missing, true, true))
                    throw new InvalidOperationException("안내판 한글 글리프가 누락되었습니다.");
            }
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Capture(new Vector3(0, 2.6f, 4), new Vector3(-18.5f, 4.3f, 10), "airport");
            Capture(new Vector3(0, 2.6f, 4), new Vector3(18.5f, 4.3f, 10), "park");
            Capture(new Vector3(0, 3.5f, -1), new Vector3(0, 4.4f, 20), "north");
            Debug.Log("구름 경계 적용 완료: 안내판 2개, 한글 및 경계 충돌 확인.");
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
            Transform sign = Group("FloatingNotice", root.transform, new Vector3(0, 7.2f, 1));
            Material cream = Color("BoundaryNoticeCream", "#FFF0D3");
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
            return root;
        }

        private static void Capture(Vector3 position, Vector3 lookAt, string name)
        {
            string output = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/cloud-boundaries")
            );
            Directory.CreateDirectory(output);
            GameObject cameraObject = new GameObject("CloudPreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(lookAt - position)
            );
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
