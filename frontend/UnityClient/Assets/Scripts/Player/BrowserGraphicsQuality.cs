using System;
using UnityEngine;

namespace A305.Player
{
    public static class BrowserGraphicsQuality
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                Apply(Application.isMobilePlatform, BrowserPointer.IsPrimaryCoarse);
        }

        public static void Apply(bool isMobileBrowser, bool isPrimaryPointerCoarse)
        {
            // iPadOS Safari는 Mac UA를 보내므로 주 입력 장치도 함께 확인한다.
            // 모바일은 Web 기본 품질을 유지하고 PC만 시작 전에 올린다.
            if (isMobileBrowser || isPrimaryPointerCoarse)
                return;

            int pcQualityLevel = Array.IndexOf(QualitySettings.names, "PC");
            if (pcQualityLevel < 0)
            {
                Debug.LogWarning("PC 그래픽 품질 설정이 없어 Web 기본 품질을 유지합니다.");
                return;
            }

            QualitySettings.SetQualityLevel(pcQualityLevel);
            Debug.Log("Browser graphics quality: PC");
        }
    }
}
