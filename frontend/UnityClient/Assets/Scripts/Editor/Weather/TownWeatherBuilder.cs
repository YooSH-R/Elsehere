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
    /// 마을에 예보 날씨 표시를 연결한다. 맑은 날의 하늘·조명은 씬 디자인을 그대로 쓰고,
    /// 흐림·비의 값은 데이터 에셋에서 조정한다. 디자인한 구름·비는 덮어쓰지 않는다.
    /// </summary>
    public static class TownWeatherBuilder
    {
        private const string MenuPath = "Tools/A305/Weather/Build Town Weather";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string SettingsPath = "Assets/Data/Weather/TownWeather.asset";
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
            BuilderTools.SetReference(
                weather,
                "settings",
                BuilderTools.LoadOrCreateAsset<WeatherSettings>(SettingsPath)
            );
            BuilderTools.SetReference(weather, "sun", Sun(groups));
            BuilderTools.SetReference(weather, "clouds", summerClouds);
            BuilderTools.SetReference(weather, "overcastClouds", OvercastClouds(summerClouds));
            BuilderTools.SetReference(weather, "rain", Rain(groups));
            BuilderTools.SetReference(weather, "rainFollowTarget", player);
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
                MainstreetAtmosphere.Cloud(overcast, position, material).localScale = new Vector3(
                    1.6f,
                    1.2f,
                    1.6f
                );
            overcast.gameObject.SetActive(false);
            return overcast.gameObject;
        }

        private static ParticleSystem Rain(SceneGroups groups)
        {
            Transform existing = groups.Environment.Find("JourneyRain");
            if (existing != null)
                return existing.GetComponent<ParticleSystem>();
            GameObject created = BuilderTools
                .CreateEmpty("JourneyRain", groups.Environment, Vector3.up * 7, Quaternion.identity)
                .gameObject;
            var particles = created.AddComponent<ParticleSystem>();
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
            created.transform.rotation = Quaternion.Euler(90, 0, 0);
            var renderer = created.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 8;
            var material = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            if (material == null)
            {
                BuilderTools.EnsureFolder("Assets/Art/Mission");
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, RainMaterialPath);
            }
            renderer.sharedMaterial = material;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }
    }
}
