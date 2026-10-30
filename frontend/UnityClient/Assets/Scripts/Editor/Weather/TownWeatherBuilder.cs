using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Mission;
using A305.Player;
using A305.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Weather
{
    /// <summary>
    /// 마을에 예보 날씨 표시를 연결한다. 날씨마다 정의 에셋 하나를 두고 TownWeather 목록에 넣는다.
    /// 맑은 날은 씬 디자인을 그대로 쓴다. 이미 있는 에셋·프리팹·흐림 구름은 덮어쓰지 않는다.
    /// </summary>
    public static class TownWeatherBuilder
    {
        private const string MenuPath = "Tools/A305/Weather/Build Town Weather";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string DataFolder = "Assets/Data/Weather";
        private const string SettingsPath = DataFolder + "/TownWeather.asset";
        private const string RainPrefabPath = "Assets/Prefabs/Weather/Rain.prefab";
        private const string RainMaterialPath = "Assets/Art/Mission/JourneyRain.mat";

        // 마을 길(z -32~69, x ±19)을 어느 쪽에서 봐도 하늘이 덮이게 둘레와 머리 위에 둔다.
        private static readonly Vector3[] OvercastPositions =
        {
            new Vector3(-26, 21, -38),
            new Vector3(6, 23, -46),
            new Vector3(28, 20, -20),
            new Vector3(-12, 24, -10),
            new Vector3(-30, 22, 2),
            new Vector3(24, 24, 18),
            new Vector3(0, 26, 20),
            new Vector3(-20, 21, 30),
            new Vector3(30, 22, 44),
            new Vector3(-4, 25, 62),
            new Vector3(-28, 23, 80),
            new Vector3(18, 21, 100),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            SceneGroups groups = BuilderTools.EnsureGroups(scene);
            WeatherPresenter weather = ConnectToScene(
                groups,
                BuilderTools.FindSingleInScene<PlayerMover>(scene).transform
            );
            var mission = BuilderTools.FindSingleInScene<FirstDayMissionController>(scene);
            if (mission != null)
                BuilderTools.SetReference(mission, "weather", weather);
            BuilderTools.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>해·구름·비를 날씨 표시에 연결해 돌려준다. 이미 있으면 연결만 다시 맞춘다.</summary>
        public static WeatherPresenter ConnectToScene(SceneGroups groups, Transform player)
        {
            var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(groups.Scene);
            if (weather == null)
                weather = BuilderTools
                    .CreateEmpty("TownWeather", groups.Lighting, Vector3.zero, Quaternion.identity)
                    .gameObject.AddComponent<WeatherPresenter>();
            Transform summerClouds = groups
                .Environment.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "SummerClouds");
            if (summerClouds == null)
                throw new InvalidOperationException(
                    "TownScene Environment에 SummerClouds가 없습니다. 먼저 마을 아트를 적용합니다."
                );
            BuilderTools.SetReference(weather, "settings", Settings());
            BuilderTools.SetReference(weather, "sun", Sun(groups));
            BuilderTools.SetReference(weather, "clouds", summerClouds);
            BuilderTools.SetReference(weather, "overcastClouds", OvercastClouds(summerClouds));
            BuilderTools.SetReference(weather, "effectFollowTarget", player);
            BuilderTools.RequireReferences(weather);
            return weather;
        }

        private static Light Sun(SceneGroups groups)
        {
            foreach (Light light in groups.Lighting.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            throw new InvalidOperationException(
                "TownScene Lighting에 해(Directional Light)가 없습니다."
            );
        }

        /// <summary>맑은 날 구름 옆에 흐림·비 구름을 둔다. 이미 있으면 디자인한 배치를 그대로 쓴다.</summary>
        private static GameObject OvercastClouds(Transform summerClouds)
        {
            Transform existing = summerClouds.parent.Find("OvercastClouds");
            if (existing != null)
                return existing.gameObject;
            Material material = summerClouds.GetComponentInChildren<Renderer>().sharedMaterial;
            Transform overcast = MainstreetArtKit.Group(
                "OvercastClouds",
                summerClouds.parent,
                Vector3.zero
            );
            foreach (Vector3 position in OvercastPositions)
            {
                Transform cloud = MainstreetAtmosphere.Cloud(overcast, position, material);
                // 하늘을 덮도록 맑은 날 구름보다 넓고 두껍게 편다.
                cloud.localScale = new Vector3(1.6f, 1.2f, 1.6f);
            }
            overcast.gameObject.SetActive(false);
            return overcast.gameObject;
        }

        /// <summary>마을 날씨 목록. 없을 때만 맑음·흐림·비로 만들고, 있으면 조정한 값과 더한 날씨를 그대로 둔다.</summary>
        private static WeatherSettings Settings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeatherSettings>(SettingsPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(DataFolder);
            WeatherSettings settings = WeatherSettings.Create(
                1.5f,
                7f,
                Definition(
                    "Clear",
                    () => WeatherDefinition.Create("CLEAR", default, keepDesignedLook: true)
                ),
                Definition(
                    "Cloudy",
                    () => WeatherDefinition.Create("CLOUDY", CloudyLook(), coversSky: true)
                ),
                Definition(
                    "Rain",
                    () =>
                        WeatherDefinition.Create(
                            "RAIN",
                            RainLook(),
                            coversSky: true,
                            effect: RainEffect()
                        )
                )
            );
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static WeatherDefinition Definition(string fileName, Func<WeatherDefinition> create)
        {
            string path = DataFolder + "/" + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeatherDefinition>(path);
            if (existing != null)
                return existing;
            WeatherDefinition created = create();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // 맑은 여름 하늘(MainstreetAtmosphere)보다 해가 약하고 하늘이 회색인 흐린 날.
        private static WeatherLook CloudyLook() =>
            new WeatherLook
            {
                skyZenith = new Color(.47f, .53f, .6f),
                skyHorizon = new Color(.76f, .79f, .81f),
                sunColor = new Color(.92f, .93f, .95f),
                sunIntensity = .95f,
                sunShadowStrength = .35f,
                ambientSky = new Color(.56f, .61f, .67f),
                ambientEquator = new Color(.58f, .57f, .54f),
                ambientGround = new Color(.3f, .29f, .28f),
                fogColor = new Color(.7f, .73f, .76f),
                fogStartMeters = 32f,
                fogEndMeters = 110f,
                cloudColor = new Color(.84f, .85f, .87f),
            };

        // 흐린 날보다 어둡고 안개가 가까운 비 오는 날.
        private static WeatherLook RainLook() =>
            new WeatherLook
            {
                skyZenith = new Color(.36f, .41f, .47f),
                skyHorizon = new Color(.58f, .62f, .66f),
                sunColor = new Color(.82f, .86f, .92f),
                sunIntensity = .6f,
                sunShadowStrength = .15f,
                ambientSky = new Color(.45f, .5f, .56f),
                ambientEquator = new Color(.45f, .46f, .47f),
                ambientGround = new Color(.25f, .25f, .26f),
                fogColor = new Color(.53f, .57f, .62f),
                fogStartMeters = 18f,
                fogEndMeters = 80f,
                cloudColor = new Color(.6f, .63f, .67f),
            };

        /// <summary>플레이어 머리 위에서 아래로 떨어지는 빗방울. 없을 때만 만든다.</summary>
        private static GameObject RainEffect()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RainPrefabPath);
            if (existing != null)
                return existing;
            var root = new GameObject("Rain");
            // 상자 모양 방출면을 눕혀 빗방울이 아래로 떨어지게 한다.
            var drops = BuilderTools.CreateEmpty(
                "Drops",
                root.transform,
                Vector3.zero,
                Quaternion.Euler(90, 0, 0)
            );
            var particles = drops.gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.startLifetime = 1.2f;
            main.startSpeed = 10;
            main.startSize = .025f;
            main.startColor = new Color(.65f, .8f, .95f, .45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;
            var emission = particles.emission;
            emission.rateOverTime = 250;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18, 18, .1f);
            var renderer = drops.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 8;
            renderer.sharedMaterial = RainMaterial();
            return BuilderTools.SavePrefab(root, RainPrefabPath);
        }

        private static Material RainMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder("Assets/Art/Mission");
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, RainMaterialPath);
            return material;
        }
    }
}
