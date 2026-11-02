using System.IO;
using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.World
{
    public static class VehicleDriverPreview
    {
        [MenuItem("Tools/A305/World/Capture Vehicle Drivers")]
        public static void Capture()
        {
            if (!BuilderTools.CanBuild())
                return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (Camera existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;

            var camera = new GameObject("DriverReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 50;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 100;
            string folder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/vehicle-drivers")
            );
            Directory.CreateDirectory(folder);
            foreach (LoopingCar car in BuilderTools.FindAllInScene<LoopingCar>(scene))
            {
                foreach (Animator animator in car.GetComponentsInChildren<Animator>(true))
                {
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    Debug.Log(
                        $"Driver review: {car.name}, {animator.name}, enabled={animator.enabled}, head={car.transform.InverseTransformPoint(head.position)}"
                    );
                }
                CaptureView(
                    camera,
                    car.transform,
                    new Vector3(-3.2f, 1.6f, 2.8f),
                    folder,
                    car.name + "-front.png"
                );
                CaptureView(
                    camera,
                    car.transform,
                    new Vector3(-2.6f, 1.3f, .1f),
                    folder,
                    car.name + "-side.png"
                );
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        }

        private static void CaptureView(
            Camera camera,
            Transform car,
            Vector3 position,
            string folder,
            string filename
        )
        {
            camera.transform.position = car.TransformPoint(position);
            camera.transform.LookAt(car.TransformPoint(new Vector3(-.2f, .95f, .1f)));
            var target = new RenderTexture(1200, 800, 24);
            var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
