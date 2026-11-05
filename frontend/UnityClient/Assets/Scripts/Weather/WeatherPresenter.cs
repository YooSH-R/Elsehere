using System.Collections.Generic;
using System.Linq;
using A305.Theme;
using UnityEngine;

namespace A305.Weather
{
    /// <summary>
    /// 예보한 날씨와 게임 시각에 맞춰 실외 장소의 하늘·해·환경광·안개·구름과 날씨 효과, 밤 불빛과 입점 건물의 불 켠 창을 바꾼다.
    /// 날씨 종류는 <see cref="WeatherSettings"/>의 정의 에셋 목록에서 서버 값으로 찾고, 그 낮 모습 위에
    /// <see cref="DaylightSettings"/>의 낮·노을·밤을 입힌다. 하늘·구름·발광 머티리얼은 사본을 물들여 원본 에셋을 바꾸지 않는다.
    /// UI는 시각을 따르지 않으므로, 밤에 어두워진 만큼 <see cref="UiBackdropLift"/>를 올려 유리 UI를 낮과 같은 밝기로 둔다.
    /// </summary>
    public sealed class WeatherPresenter : DaylightView
    {
        private static readonly int ZenithId = Shader.PropertyToID("_Zenith");
        private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
        private static readonly int StarsId = Shader.PropertyToID("_Stars");

        // 매장 실내(Interior)의 천장 등 이름. 입점 건물 외벽에 붙은 상점 등(FixtureLight)은 여기에 들지 않는다.
        private const string ShopRoomLampName = "InteriorLight";

        [SerializeField]
        private WeatherSettings settings;

        [SerializeField]
        private Light sun;

        [SerializeField]
        [Tooltip("맑은 날에도 떠 있는 구름. 날씨에 따라 색이 바뀐다")]
        private Transform clouds;

        [SerializeField]
        [Tooltip("하늘을 덮는 날씨(흐림·비 등)에만 띄우는 구름")]
        private GameObject overcastClouds;

        [SerializeField]
        [Tooltip("날씨 효과가 머리 위에서 따라갈 대상. 보통 플레이어")]
        private Transform effectFollowTarget;

        [SerializeField]
        [Tooltip("게임 시각에 따른 낮·노을·밤. 비우면 늘 씬 디자인의 낮이다")]
        private DaylightSettings daylight;

        [SerializeField]
        [Tooltip("밤에 더 밝히는 상점 벽등·실내등. 낮에는 씬 디자인 세기 그대로다")]
        private Light[] lamps = new Light[0];

        [SerializeField]
        [Tooltip("밤에만 켜는 가로등 빛. 낮에는 끈다")]
        private Light[] streetLamps = new Light[0];

        [SerializeField]
        [Tooltip("입점한 상점·숙소 건물. 밤에는 안에서 불을 켠 것처럼 창이 빛난다")]
        private Transform[] litBuildings = new Transform[0];

        private readonly Dictionary<WeatherDefinition, GameObject> effects =
            new Dictionary<WeatherDefinition, GameObject>();
        private bool prepared;
        private WeatherLook designedLook;
        private WeatherLook shownLook;
        private WeatherLook blendFrom;
        private WeatherLook blendTo;
        private float blendElapsedSeconds;
        private bool blending;
        private GameObject shownEffect;
        private GameObject leavingEffect;
        private Material designedSky;
        private Material sky;
        private Material designedCloud;
        private Material cloud;
        private Renderer[] cloudRenderers;
        private Quaternion designedSunRotation;
        private NightLights nightLights;
        private NightLights shopLights;
        private readonly Dictionary<Transform, NightLights> buildingLights =
            new Dictionary<Transform, NightLights>();
        private readonly Dictionary<Transform, bool> buildingOpen =
            new Dictionary<Transform, bool>();

        public IReadOnlyList<Transform> LitBuildings => litBuildings;

        public void SetBuildingOpen(Transform building, bool open)
        {
            if (buildingOpen.TryGetValue(building, out bool previous) && previous == open)
                return;
            buildingOpen[building] = open;
            if (prepared && shownMinutes.HasValue)
                WriteScene();
        }

        private bool IsInLitBuilding(Transform item) =>
            litBuildings.Any(building => building != null && item.IsChildOf(building));

        /// <summary>
        /// 방문할 때 로드한 실내도 마을과 같은 시각을 따르게 하고, 나올 때 재질 사본을 정리한다.
        /// 실내에 있는 동안은 날씨와 상관없이 실내 씬과 같은 실내 환경광(밤에도 불을 켠 방)이고 해 그림자도 옅으며,
        /// 나오면 마을 환경광·그림자로 돌아간다.
        /// </summary>
        public void SetShopInterior(Transform interior)
        {
            shopLights?.Restore();
            shopLights = null;
            if (daylight == null)
                return;

            if (interior != null)
            {
                var renderers = interior.GetComponentsInChildren<Renderer>(true);
                // 실내 씬과 달리 창으로 드는 빛·보조광이 없으므로 실내등을 매장 실내등 배율로 밝힌다.
                shopLights = new NightLights(
                    daylight,
                    Enumerable.Empty<Light>(),
                    renderers,
                    renderers,
                    shopLamps: interior.GetComponentsInChildren<Light>(true)
                );
            }
            if (shownMinutes.HasValue)
                WriteScene();
        }

        // 시각을 받기 전에는 씬 디자인의 낮이다.
        private double? shownMinutes;

        /// <summary>마지막으로 보여 준 날씨. 아직 바꾸지 않았으면 null이고 씬 디자인 그대로다.</summary>
        public WeatherDefinition Current { get; private set; }

        public WeatherSettings Settings => settings;

        public override void ShowTime(double gameMinutes)
        {
            if (daylight == null)
                return;
            Prepare();
            shownMinutes = gameMinutes;
            WriteScene();
        }

        private void Awake()
        {
            bool missingReference =
                settings == null
                || sun == null
                || clouds == null
                || overcastClouds == null
                || effectFollowTarget == null;
            if (missingReference)
            {
                Debug.LogError("날씨 설정·해·구름·효과가 따라갈 대상 참조가 필요합니다.", this);
                enabled = false;
            }
        }

        // 하늘·환경광·안개는 활성 씬의 값이다. 다른 공간에서 이 씬을 겹쳐 불러오면 Awake 때는
        // 아직 이전 씬이 활성 씬이므로, 이 씬이 활성 씬이 된 뒤인 Start에서 디자인 값을 읽는다.
        private void Start()
        {
            Prepare();
        }

        /// <summary>
        /// 서버 값(code)의 날씨로 설정한 시간 동안 섞어 바꾼다. 목록에 없는 날씨면 fallbackCode의 날씨를 보여 준다.
        /// </summary>
        public void Show(string code, string fallbackCode)
        {
            WeatherDefinition weather = Resolve(code, fallbackCode);
            if (weather == null)
                return;
            Prepare();
            ChangeWeather(weather);
            blendFrom = shownLook;
            blendTo = weather.LookOver(designedLook);
            blendElapsedSeconds = 0f;
            blending = true;
        }

        /// <summary>섞지 않고 바로 바꾼다. Editor 미리보기처럼 프레임이 흐르지 않을 때 쓴다.</summary>
        public void ShowImmediately(string code)
        {
            WeatherDefinition weather = Resolve(code, null);
            if (weather == null)
                return;
            Prepare();
            ChangeWeather(weather);
            blending = false;
            ShowWeatherLook(weather.LookOver(designedLook));
            FinishChange();
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
            ShowWeatherLook(WeatherLook.Lerp(blendFrom, blendTo, amount));
            if (amount < 1f)
                return;
            blending = false;
            FinishChange();
        }

        private void LateUpdate()
        {
            if (shownEffect != null)
                MoveOverTarget(shownEffect);
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
            nightLights?.Restore();
            foreach (NightLights lights in buildingLights.Values)
                lights.Restore();
            shopLights?.Restore();
            if (shownMinutes.HasValue)
                UiBackdropLift.Show(0f);
        }

        private WeatherDefinition Resolve(string code, string fallbackCode)
        {
            WeatherDefinition weather = settings.Resolve(code, fallbackCode);
            if (weather == null)
                Debug.LogWarning(
                    $"날씨 '{code}'와 대신할 '{fallbackCode}'가 {settings.name}에 없어 날씨를 바꾸지 않습니다.",
                    this
                );
            return weather;
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
            designedSunRotation = sun.transform.rotation;
            if (daylight != null)
            {
                // 입점 건물 안의 실내등은 마을의 어두운 밤 환경광을 그대로 받는 방을 밝히므로 따로 배율을 둔다.
                Renderer[] renderers = gameObject
                    .scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                    .ToArray();
                nightLights = new NightLights(
                    daylight,
                    lamps.Where(lamp => lamp != null && !IsInLitBuilding(lamp.transform)),
                    renderers.Where(renderer => !IsInLitBuilding(renderer.transform)),
                    streetLamps: streetLamps
                );
                foreach (
                    Transform building in litBuildings
                        .Where(building => building != null)
                        .Distinct()
                )
                {
                    Renderer[] surfaces = building.GetComponentsInChildren<Renderer>(true);
                    Light[] lights = lamps
                        .Where(lamp => lamp != null && lamp.transform.IsChildOf(building))
                        .ToArray();
                    buildingLights[building] = new NightLights(
                        daylight,
                        lights.Where(lamp => lamp.name != ShopRoomLampName),
                        surfaces,
                        surfaces,
                        shopLamps: lights.Where(lamp => lamp.name == ShopRoomLampName)
                    );
                }
            }

            if (designedSky != null)
            {
                sky = new Material(designedSky);
                RenderSettings.skybox = sky;
            }
            // 하늘을 덮는 구름도 같은 색으로 물들도록 모든 구름이 사본 하나를 함께 쓴다.
            if (designedCloud != null)
            {
                cloud = new Material(designedCloud);
                foreach (Renderer cloudRenderer in cloudRenderers)
                    cloudRenderer.sharedMaterial = cloud;
            }
            overcastClouds.SetActive(false);
        }

        private void ChangeWeather(WeatherDefinition weather)
        {
            Current = weather;
            if (weather.CoversSky)
                overcastClouds.SetActive(true);
            GameObject nextEffect = EffectOf(weather);
            if (nextEffect != shownEffect)
            {
                // 떠나는 효과는 새로 만들지만 않게 멈추고, 이미 떨어지던 것은 섞기가 끝날 때 거둔다.
                PutAwayLeavingEffect();
                if (shownEffect != null)
                {
                    foreach (
                        ParticleSystem particles in shownEffect.GetComponentsInChildren<ParticleSystem>()
                    )
                        particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    leavingEffect = shownEffect;
                }
                shownEffect = nextEffect;
            }
            if (shownEffect == null)
                return;
            MoveOverTarget(shownEffect);
            shownEffect.SetActive(true);
            foreach (
                ParticleSystem particles in shownEffect.GetComponentsInChildren<ParticleSystem>()
            )
                particles.Play(false);
        }

        private void FinishChange()
        {
            // 맑아질 때는 하늘이 다 밝아진 뒤에 덮고 있던 구름을 걷는다.
            overcastClouds.SetActive(Current.CoversSky);
            PutAwayLeavingEffect();
        }

        private void PutAwayLeavingEffect()
        {
            if (leavingEffect != null)
                leavingEffect.SetActive(false);
            leavingEffect = null;
        }

        /// <summary>날씨 효과를 처음 쓸 때 만들어 두고 다시 쓴다. 효과가 없는 날씨는 null이다.</summary>
        private GameObject EffectOf(WeatherDefinition weather)
        {
            if (weather.Effect == null)
                return null;
            if (!effects.TryGetValue(weather, out GameObject effect))
            {
                effect = Instantiate(weather.Effect, transform);
                effect.name = weather.Effect.name;
                effect.SetActive(false);
                effects.Add(weather, effect);
            }
            return effect;
        }

        private void MoveOverTarget(GameObject effect)
        {
            effect.transform.position =
                effectFollowTarget.position + Vector3.up * settings.EffectHeightMeters;
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

        private void ShowWeatherLook(WeatherLook look)
        {
            shownLook = look;
            WriteScene();
        }

        /// <summary>날씨 모습에 지금 시각을 입혀 해·환경광·안개·하늘·구름과 밤 불빛에 쓴다.</summary>
        private void WriteScene()
        {
            WeatherLook look = shownLook;
            float stars = 0f;
            if (daylight != null && shownMinutes.HasValue)
            {
                double minutes = shownMinutes.Value;
                bool skyCovered = Current != null && Current.CoversSky;
                look = daylight.Mix(shownLook, minutes, skyCovered);
                stars = daylight.StarAmount(minutes, skyCovered);
                sun.transform.rotation = daylight.SunRotation(designedSunRotation, minutes);
                float night = daylight.NightAmount(minutes);
                nightLights.Apply(daylight, night);
                foreach (var entry in buildingLights)
                    entry.Value.Apply(
                        daylight,
                        night,
                        !buildingOpen.TryGetValue(entry.Key, out bool open) || open
                    );
                UiBackdropLift.Show(night);
                if (shopLights != null)
                {
                    shopLights.Apply(daylight, night);
                    // 매장 안에서는 날씨와 상관없이 실내 씬과 같은 실내 환경광을 쓰고,
                    // InteriorDaylight처럼 밤에만 조금 줄인다.
                    float roomAmbient = Mathf.Lerp(1f, daylight.NightInteriorAmbientScale, night);
                    look.ambientSky = daylight.ShopInteriorAmbientSky * roomAmbient;
                    look.ambientEquator = daylight.ShopInteriorAmbientEquator * roomAmbient;
                    look.ambientGround = daylight.ShopInteriorAmbientGround * roomAmbient;
                    // 마을 해의 지붕·벽 그림자가 방을 뒤덮지 않도록 안에 있는 동안만 옅게 한다.
                    look.sunShadowStrength = Mathf.Min(
                        look.sunShadowStrength,
                        daylight.ShopInteriorSunShadowStrength
                    );
                }
            }
            if (sky != null && sky.HasProperty(StarsId))
                sky.SetFloat(StarsId, stars);
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
