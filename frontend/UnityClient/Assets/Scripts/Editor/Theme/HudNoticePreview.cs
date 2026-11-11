using System;
using System.IO;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.Player;
using A305.Theme;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Theme
{
    /// <summary>알림 카드가 상단 줄과 겹치지 않는지 실제 HUD 프리팹으로 렌더링해 확인한다. 씬은 저장하지 않는다.</summary>
    public static class HudNoticePreview
    {
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("HUD_NOTICE_PREVIEW_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Set HUD_NOTICE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion angle = Quaternion.Euler(16, player.eulerAngles.y, 0);
            camera.transform.SetPositionAndRotation(
                player.position + Vector3.up * .97f + angle * new Vector3(0, 0, -3.4f),
                angle
            );
            foreach (
                PlayerTouchControls controls in BuilderTools.FindAllInScene<PlayerTouchControls>(
                    scene
                )
            )
                controls.gameObject.SetActive(false);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                element.ApplyCurrent();
            LearningHudView hud = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            hud.transform.Find("Panel/MenuBar").gameObject.SetActive(true);
            foreach (
                FirstDayMissionView mission in BuilderTools.FindAllInScene<FirstDayMissionView>(
                    scene
                )
            )
            {
                mission.transform.Find("Content").gameObject.SetActive(true);
                Transform status = mission.transform.Find("Content/Status");
                status.gameObject.SetActive(true);
                status.Find("Clock/Label").GetComponent<TMP_Text>().text = "35일차 · 오전 4:40";
                status.Find("Coins/Row/Amount").GetComponent<TMP_Text>().text = "98";
            }
            hud.ShowMessage("피드백이 완성됐어요. 휴대폰에서 확인해 주세요.");
            Render(camera, hud, Path.Combine(output, "notice-1920x1080.png"), 1920, 1080);
            Render(camera, hud, Path.Combine(output, "notice-1280x720.png"), 1280, 720);
            hud.ShowMessage("단어장에 담았어요.");
            Render(camera, hud, Path.Combine(output, "notice-short-1920x1080.png"), 1920, 1080);
        }

        private static void Render(
            Camera camera,
            LearningHudView hud,
            string path,
            int width,
            int height
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    (RectTransform)hud.transform.Find("Notice/MessageCard")
                );
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
