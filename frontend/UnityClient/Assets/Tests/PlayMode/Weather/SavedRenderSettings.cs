#if UNITY_EDITOR
using A305.Theme;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.Weather.PlayModeTests
{
    /// <summary>테스트가 바꾼 활성 씬의 하늘·환경광·안개와 유리 UI 뒤 화면 밝히기를 테스트 전 값으로 되돌린다.</summary>
    internal sealed class SavedRenderSettings
    {
        private float uiBackdropLift;
        private Material skybox;
        private AmbientMode ambientMode;
        private Color ambientSky;
        private Color ambientEquator;
        private Color ambientGround;
        private float ambientIntensity;
        private bool fog;
        private FogMode fogMode;
        private Color fogColor;
        private float fogStart;
        private float fogEnd;

        public void Read()
        {
            uiBackdropLift = UiBackdropLift.Amount;
            skybox = RenderSettings.skybox;
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
        }

        public void Restore()
        {
            UiBackdropLift.Show(uiBackdropLift);
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
#endif
