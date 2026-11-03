using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.Weather
{
    /// <summary>
    /// 실내 장소의 낮·밤. 밤에는 창으로 드는 빛을 달빛으로 바꾸고 낮빛 보조광·환경광을 줄이며, 실내등은 밝히고 창밖 유리는 밤하늘 색으로 칠한다.
    /// 낮에는 씬에 디자인한 값 그대로다. 값은 이 씬이 활성 씬이 된 뒤 Start에서 읽는다.
    /// </summary>
    public sealed class InteriorDaylight : DaylightView
    {
        [SerializeField]
        private DaylightSettings daylight;

        [SerializeField]
        [Tooltip("창으로 드는 햇빛(Directional Light). 밤에는 달빛 색으로 약해진다")]
        private Light[] windowLights = new Light[0];

        [SerializeField]
        [Tooltip("낮빛이 방에 퍼지는 것을 흉내 낸 보조광(Bounce). 밤에는 약해진다")]
        private Light[] fills = new Light[0];

        [SerializeField]
        [Tooltip("스탠드·펜던트·샹들리에 같은 실내등. 밤에는 밝아진다")]
        private Light[] lamps = new Light[0];

        private bool prepared;
        private float[] windowLightIntensities;
        private Color[] windowLightColors;
        private float[] fillIntensities;
        private AmbientMode ambientMode;
        private Color ambientSky;
        private Color ambientEquator;
        private Color ambientGround;
        private float ambientIntensity;
        private NightLights nightLights;

        private void Awake()
        {
            if (daylight == null)
            {
                Debug.LogError("밤낮 설정(DaylightSettings) 참조가 필요합니다.", this);
                enabled = false;
            }
        }

        // 환경광은 활성 씬의 값이다. 다른 공간에서 이 씬을 겹쳐 불러오면 Awake 때는 아직 이전 씬이 활성 씬이다.
        private void Start()
        {
            Prepare();
        }

        private void OnDestroy()
        {
            nightLights?.Restore();
        }

        public override void ShowTime(double gameMinutes)
        {
            if (daylight == null)
                return;
            Prepare();
            float night = daylight.NightAmount(gameMinutes);
            float windowScale = Mathf.Lerp(1f, daylight.NightWindowLightScale, night);
            for (int index = 0; index < windowLights.Length; index++)
            {
                windowLights[index].intensity = windowLightIntensities[index] * windowScale;
                windowLights[index].color = Color.Lerp(
                    windowLightColors[index],
                    daylight.MoonColor,
                    night
                );
            }
            float fillScale = Mathf.Lerp(1f, daylight.NightFillScale, night);
            for (int index = 0; index < fills.Length; index++)
                fills[index].intensity = fillIntensities[index] * fillScale;
            ApplyAmbient(Mathf.Lerp(1f, daylight.NightInteriorAmbientScale, night));
            nightLights.Apply(daylight, night);
        }

        private void Prepare()
        {
            if (prepared)
                return;
            prepared = true;
            windowLights = windowLights.Where(light => light != null).ToArray();
            fills = fills.Where(light => light != null).ToArray();
            windowLightIntensities = windowLights.Select(light => light.intensity).ToArray();
            windowLightColors = windowLights.Select(light => light.color).ToArray();
            fillIntensities = fills.Select(light => light.intensity).ToArray();
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity;
            nightLights = new NightLights(
                daylight,
                lamps,
                gameObject
                    .scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            );
        }

        private void ApplyAmbient(float scale)
        {
            // 하늘상자 환경광은 색 대신 세기로 줄인다.
            if (ambientMode == AmbientMode.Skybox)
            {
                RenderSettings.ambientIntensity = ambientIntensity * scale;
                return;
            }
            RenderSettings.ambientSkyColor = Scale(ambientSky, scale);
            RenderSettings.ambientEquatorColor = Scale(ambientEquator, scale);
            RenderSettings.ambientGroundColor = Scale(ambientGround, scale);
        }

        private static Color Scale(Color color, float scale) =>
            new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
    }
}
