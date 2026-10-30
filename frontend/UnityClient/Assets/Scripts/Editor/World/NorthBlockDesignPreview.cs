using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.World
{
    public static class NorthBlockDesignPreview
    {
        [MenuItem("Tools/A305/World/Capture North Block Interiors")]
        public static void CaptureInteriors()
        {
            var scene = EditorSceneManager.OpenScene(NorthBlockInteriorBuilder.SandboxPath);
            foreach (
                Canvas canvas in scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            )
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;
            var camera = new GameObject("InteriorReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 76;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 160;
            camera.backgroundColor = new Color(.76f, .85f, .86f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(1, 11, 14);
            camera.transform.LookAt(new Vector3(0, 3, 32));
            SaveImage(camera, "built-street.png");
            Transform background = TownProductionBuilder
                .Root(scene, "Environment")
                .Find("TownMainstreet/BackgroundBuildings");
            foreach (string name in new[] { "MapleBakery", "LittleArcade", "HomeLiving" })
            {
                Transform building = background.Find(name);
                camera.transform.position = building.TransformPoint(new Vector3(1, 5.8f, 10));
                camera.transform.LookAt(building.TransformPoint(new Vector3(0, 3.1f, 0)));
                SaveImage(camera, name + "-front.png");
                camera.transform.position = building.TransformPoint(new Vector3(-.5f, 2, -.6f));
                camera.transform.LookAt(building.TransformPoint(new Vector3(-2.2f, 1, -4.9f)));
                SaveImage(camera, name + "-ground.png");
                camera.transform.position = building.TransformPoint(new Vector3(-.5f, 5.9f, -.7f));
                camera.transform.LookAt(building.TransformPoint(new Vector3(-2.1f, 4.8f, -4.8f)));
                SaveImage(camera, name + "-upstairs.png");
            }
            Transform bakery = background.Find("MapleBakery");
            camera.transform.position = bakery.TransformPoint(new Vector3(-1.7f, 2.2f, -.9f));
            camera.transform.LookAt(bakery.TransformPoint(new Vector3(2, 2.3f, -4)));
            SaveImage(camera, "stairs.png");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void SaveImage(Camera camera, string filename)
        {
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            string folder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/north-block")
            );
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }

        [MenuItem("Tools/A305/World/Preview North Block Locations")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            foreach (
                Canvas canvas in scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            )
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;
            var camera = new GameObject("PlanCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 70, 0);
            camera.transform.LookAt(new Vector3(0, 0, 23));
            camera.orthographic = true;
            camera.orthographicSize = 38;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.backgroundColor = new Color(.86f, .89f, .86f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var canvasRoot = new GameObject("PlanLabels", typeof(Canvas), typeof(CanvasScaler));
            var overlay = canvasRoot.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = camera;
            overlay.planeDistance = 1;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var target = new RenderTexture(1600, 1200, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            Label(
                overlay.transform,
                "NORTH BLOCK / EXISTING BUILDINGS",
                new Vector2(30, 1140),
                new Vector2(900, 48),
                32,
                Color.white,
                new Color(.12f, .23f, .2f)
            );
            Label(
                overlay.transform,
                "THREE EXISTING BUILDINGS / THREE ENTRANCE TYPES",
                new Vector2(30, 1085),
                new Vector2(1030, 38),
                23,
                new Color(.12f, .23f, .2f),
                new Color(.96f, .95f, .89f)
            );
            Marker(
                camera,
                overlay.transform,
                new Vector3(-11, 8, 21),
                "1  BAKERY",
                new Color(.73f, .33f, .2f)
            );
            Marker(
                camera,
                overlay.transform,
                new Vector3(11, 8, 30),
                "2  ARCADE / BOOKS",
                new Color(.22f, .45f, .56f)
            );
            Marker(
                camera,
                overlay.transform,
                new Vector3(-11, 8, 39),
                "3  HOME / TEA",
                new Color(.32f, .47f, .28f)
            );
            Marker(
                camera,
                overlay.transform,
                new Vector3(-11, 8, -1),
                "RESERVED / STORE",
                new Color(.42f, .42f, .42f)
            );
            Marker(
                camera,
                overlay.transform,
                new Vector3(11, 9, -2),
                "HOTEL",
                new Color(.42f, .42f, .42f)
            );
            Label(
                overlay.transform,
                "1  MAPLE BAKERY\n1F: bakery / 2F: seating\nStairs INSIDE the shop\n\n2  ARCADE + BOOKSHOP\n1F: Little Arcade\n2F: Paper & Leaf Books\nSHARED stair hall\nSeparate shop doors\n\n3  HOME GOODS + TEA ROOM\n1F: Home & Living\n2F: Leaf Tea Room\nOUTSIDE stairs to 2F\nIndependent entrances\n\nExisting buildings retained.\nHotel-front drugstore:\nreserved and unchanged.",
                new Vector2(1170, 280),
                new Vector2(405, 770),
                23,
                new Color(.16f, .25f, .21f),
                new Color(.97f, .96f, .91f)
            );
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0);
            pixels.Apply();
            string output = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/north-block")
            );
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "locations.png"), pixels.EncodeToPNG());
            Debug.Log("North block location image: " + output);
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            // This is an unsaved preview. Opening a fresh scene discards all annotations.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Marker(
            Camera camera,
            Transform parent,
            Vector3 location,
            string text,
            Color color
        )
        {
            Vector3 viewport = camera.WorldToViewportPoint(location);
            Label(
                parent,
                text,
                new Vector2(viewport.x * 1600 - 115, viewport.y * 1200),
                new Vector2(230, 43),
                22,
                Color.white,
                color
            );
        }

        private static void Label(
            Transform parent,
            string text,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Color color,
            Color background
        )
        {
            var panel = new GameObject("Label", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = background;
            var child = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(panel.transform, false);
            var label = child.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.margin = new Vector4(12, 7, 10, 6);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
