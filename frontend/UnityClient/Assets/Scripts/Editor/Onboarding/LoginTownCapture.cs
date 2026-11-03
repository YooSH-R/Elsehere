using System.IO;
using A305.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 로그인 메인의 마을 배경(Art/UI/Login/LoginTown.png)을 지금 마을(TownScene)에서 다시 찍는다. 마을 모습이 바뀌면 다시 실행한다.
    /// 남쪽 나무 바로 앞 높은 곳에서 큰길을 북쪽으로 내려다보고, 플레이어를 행인 옆에 카메라 쪽으로 돌려 세운다.
    /// 찍은 뒤 마을 씬은 저장하지 않고, 찍기 전에 열려 있던 씬으로 되돌린다.
    /// </summary>
    public static class LoginTownCapture
    {
        private const string MenuPath = "Tools/A305/Onboarding/Capture Login Town";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string OutputPath = "Assets/Art/UI/Login/LoginTown.png";
        private const int Width = 1920;
        private const int Height = 1080;

        // 왼쪽 열은 크림색이 덮으므로 오른쪽에 호텔·집·하늘이 오게 큰길을 살짝 오른쪽으로 본다.
        private static readonly Vector3 CameraPosition = new Vector3(2f, 7.5f, -27.5f);
        private static readonly Vector3 CameraAngles = new Vector3(15f, 5f, 0f);
        private const float FieldOfView = 52f;

        // 화면 비율이 바뀌어 위아래가 잘려도 보이도록 플레이어를 화면 가운데 아래에 둔다.
        private static readonly Vector3 PlayerPosition = new Vector3(3.8f, 0.12f, -16f);
        private const float PlayerYaw = 200f;

        // 낮 11시
        private const double DaytimeMinutes = 11 * 60;

        [MenuItem(MenuPath)]
        public static void Capture()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(ScenePath);
            try
            {
                Pose();
                File.WriteAllBytes(OutputPath, Render());
            }
            finally
            {
                if (previousScenes.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                else
                    EditorSceneManager.OpenScene(ScenePath);
            }
            AssetDatabase.ImportAsset(OutputPath);
            Debug.Log($"로그인 배경을 다시 찍었습니다: {OutputPath}");
        }

        private static void Pose()
        {
            GameObject player = GameObject.Find("Characters/Player");
            if (player != null)
                player.transform.SetPositionAndRotation(
                    PlayerPosition,
                    Quaternion.Euler(0, PlayerYaw, 0)
                );
            foreach (
                var daylight in Object.FindObjectsByType<DaylightView>(FindObjectsSortMode.None)
            )
                daylight.ShowTime(DaytimeMinutes);
        }

        private static byte[] Render()
        {
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraAngles));
            camera.fieldOfView = FieldOfView;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
            };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            return picture.EncodeToPNG();
        }
    }
}
