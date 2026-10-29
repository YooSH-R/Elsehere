using System.Linq;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 예보한 날씨에 맞춰 실외 장소의 하늘·해·환경광·안개·구름과 비를 바꾼다. 맑음은 씬에 디자인된 모습이다.
    /// 하늘·구름 머티리얼은 사본을 물들여 원본 에셋을 바꾸지 않는다.
    /// </summary>
    public sealed class WeatherPresenter : MonoBehaviour
    {
        private static readonly int ZenithId = Shader.PropertyToID("_Zenith");
        private static readonly int HorizonId = Shader.PropertyToID("_Horizon");

        [SerializeField]
        private WeatherSettings settings;

        [SerializeField]
        private Light sun;

        [SerializeField]
        [Tooltip("맑은 날에도 떠 있는 구름. 날씨에 따라 색이 바뀐다")]
        private Transform clouds;

        [SerializeField]
        [Tooltip("흐림·비일 때만 하늘을 덮는 구름")]
        private GameObject overcastClouds;

        [SerializeField]
        private ParticleSystem rain;

        [SerializeField]
        [Tooltip("빗방울이 머리 위에서 따라갈 대상. 보통 플레이어")]
        private Transform rainFollowTarget;

        private bool prepared;
        private WeatherLook designedLook;
        private WeatherLook shownLook;
        private WeatherLook blendFrom;
        private WeatherLook blendTo;
        private float blendElapsedSeconds;
        private bool blending;
        private Material designedSky;
        private Material sky;
        private Material designedCloud;
        private Material cloud;
        private Renderer[] cloudRenderers;

        public WeatherKind Current { get; private set; } = WeatherKind.Clear;

        private void Awake()
        {
            bool missingReference =
                settings == null
                || sun == null
                || clouds == null
                || overcastClouds == null
                || rain == null
                || rainFollowTarget == null;
            if (missingReference)
            {
                Debug.LogError("날씨 설정·해·구름·비·비가 따라갈 대상 참조가 필요합니다.", this);
                enabled = false;
                return;
            }
            Prepare();
        }

        /// <summary>지금 보이는 모습에서 새 날씨로 설정한 시간 동안 섞는다.</summary>
        public void Show(WeatherKind kind)
        {
            Prepare();
            ChangeWeather(kind);
            blendFrom = shownLook;
            blendTo = settings.LookFor(kind, designedLook);
            blendElapsedSeconds = 0f;
            blending = true;
        }

        /// <summary>섞지 않고 바로 바꾼다. Editor 미리보기처럼 프레임이 흐르지 않을 때 쓴다.</summary>
        public void ShowImmediately(WeatherKind kind)
        {
            Prepare();
            ChangeWeather(kind);
            blending = false;
            Apply(settings.LookFor(kind, designedLook));
            overcastClouds.SetActive(kind != WeatherKind.Clear);
        }

        private void Update()
        {
            if (!blending)
                return;
            blendElapsedSeconds += Time.deltaTime;
            float amount =
                settings.TransitionSeconds > 0f
                    ? blendElapsedSeconds / settings.TransitionSeconds
                    : 1f;
            Apply(WeatherLook.Lerp(blendFrom, blendTo, amount));
            if (amount < 1f)
                return;
            blending = false;
            // 맑아질 때는 하늘이 다 밝아진 뒤에 덮고 있던 구름을 걷는다.
            overcastClouds.SetActive(Current != WeatherKind.Clear);
        }

        private void LateUpdate()
        {
            if (prepared && rain.isPlaying)
                MoveRainOverTarget();
        }

        private void OnDestroy()
        {
            if (!prepared)
                return;
            if (RenderSettings.skybox == sky)
                RenderSettings.skybox = designedSky;
            foreach (Renderer cloudRenderer in cloudRenderers)
            {
                if (cloudRenderer != null)
                    cloudRenderer.sharedMaterial = designedCloud;
            }
            Destroy(sky);
            Destroy(cloud);
        }

        private void Prepare()
        {
            if (prepared)
                return;
            prepared = true;
            cloudRenderers = clouds
                .GetComponentsInChildren<Renderer>(true)
                .Concat(overcastClouds.GetComponentsInChildren<Renderer>(true))
                .ToArray();
            designedSky = RenderSettings.skybox;
            designedCloud = cloudRenderers.Length > 0 ? cloudRenderers[0].sharedMaterial : null;
            designedLook = ReadDesignedLook();
            shownLook = designedLook;

            if (designedSky != null)
            {
                sky = new Material(designedSky);
                RenderSettings.skybox = sky;
            }
            // 흐림·비 구름도 같은 색으로 물들도록 모든 구름이 사본 하나를 함께 쓴다.
            if (designedCloud != null)
            {
                cloud = new Material(designedCloud);
                foreach (Renderer cloudRenderer in cloudRenderers)
                    cloudRenderer.sharedMaterial = cloud;
            }
            overcastClouds.SetActive(false);
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ChangeWeather(WeatherKind kind)
        {
            Current = kind;
            if (kind != WeatherKind.Clear)
                overcastClouds.SetActive(true);
            if (kind == WeatherKind.Rain && !rain.isEmitting)
            {
                MoveRainOverTarget();
                rain.Play(true);
            }
            if (kind != WeatherKind.Rain)
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void MoveRainOverTarget()
        {
            rain.transform.position =
                rainFollowTarget.position + Vector3.up * settings.RainHeightMeters;
        }

        private WeatherLook ReadDesignedLook()
        {
            return new WeatherLook
            {
                skyZenith = ReadSkyColor(ZenithId),
                skyHorizon = ReadSkyColor(HorizonId),
                sunColor = sun.color,
                sunIntensity = sun.intensity,
                sunShadowStrength = sun.shadowStrength,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                fogColor = RenderSettings.fogColor,
                fogStartMeters = RenderSettings.fogStartDistance,
                fogEndMeters = RenderSettings.fogEndDistance,
                cloudColor = designedCloud != null ? designedCloud.color : Color.white,
            };
        }

        private Color ReadSkyColor(int propertyId)
        {
            return designedSky != null && designedSky.HasProperty(propertyId)
                ? designedSky.GetColor(propertyId)
                : Color.white;
        }

        private void Apply(WeatherLook look)
        {
            shownLook = look;
            sun.color = look.sunColor;
            sun.intensity = look.sunIntensity;
            sun.shadowStrength = look.sunShadowStrength;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            RenderSettings.fogColor = look.fogColor;
            RenderSettings.fogStartDistance = look.fogStartMeters;
            RenderSettings.fogEndDistance = look.fogEndMeters;
            if (sky != null)
            {
                sky.SetColor(ZenithId, look.skyZenith);
                sky.SetColor(HorizonId, look.skyHorizon);
            }
            if (cloud != null)
                cloud.color = look.cloudColor;
        }
    }
}
