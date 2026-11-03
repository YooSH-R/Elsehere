using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.Weather;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Weather
{
    /// <summary>
    /// 다섯 장소 씬이 게임 시각에 맞춰 낮·밤을 보이게 연결한다. 마을은 날씨 표시(WeatherPresenter)가, 실내는
    /// InteriorDaylight가 맡고 GameClockDaylight가 게임 시각을 넘긴다. 밤낮 설정 에셋은 없을 때만 만들고,
    /// 있으면 조정한 값을 그대로 둔다. 씬의 조명과 재질은 바꾸지 않고 참조만 맞춘다.
    /// </summary>
    public static class DaylightBuilder
    {
        private const string MenuPath = "Tools/A305/Weather/Build Day Night";
        private const string SettingsPath = "Assets/Data/Weather/DayNight.asset";

        private static readonly string[] Spaces =
        {
            "TownScene",
            "HomeScene",
            "CafeScene",
            "HotelScene",
            "ShopScene",
        };

        // 밤에 더 밝히는 발광 재질: 마을 창빛·가로등 전구·상점 조명과 실내 조명 갓.
        private static readonly string[] GlowMaterialPaths =
        {
            "Assets/Art/World/MainstreetArt/WindowAmberGlow.mat",
            "Assets/Art/World/MainstreetArt/LanternAmberV2.mat",
            "Assets/Art/World/MainstreetArt/PendantLight.mat",
            "Assets/Art/World/CafeBlenderKit/Materials/Glow.mat",
            "Assets/Art/World/TownStorefronts/CeilingLightPanel.mat",
            "Assets/Art/Hotel/MapleHouse/OpalLight.mat",
            "Assets/Art/World/Drugstore/Materials/DrugstoreLightPanel.mat",
        };

        // 실내 창밖 하늘을 그린 유리.
        private static readonly string[] WindowMaterialPaths =
        {
            "Assets/Art/Home/CozyCottage/WindowDaylight.mat",
            "Assets/Art/Cafe/CornerCafe/DaylightGlass.mat",
            "Assets/Art/World/Drugstore/Materials/DrugstoreDaylightGlass.mat",
        };

        // 마을에서 밤에 밝히는 등. 순찰차 경광등처럼 따로 켜고 끄는 빛은 넣지 않는다.
        private static readonly string[] TownLampNames =
        {
            "LanternLight",
            "FixtureLight",
            "InteriorLight",
        };

        // 실내에서 낮빛이 방에 퍼지는 것을 흉내 낸 보조광 이름에 들어가는 말.
        private const string FillLightName = "Bounce";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (string name in Spaces)
            {
                string path = "Assets/Scenes/Production/" + name + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                ConnectToScene(BuilderTools.EnsureGroups(scene));
                BuilderTools.SaveScene(scene, path);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 장소에 밤낮 표시와 게임 시각 연결을 둔다. 날씨 표시가 있는 마을은 그 표시가 밤낮도 맡는다.
        /// </summary>
        public static void ConnectToScene(SceneGroups groups)
        {
            var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(groups.Scene);
            DaylightView view;
            if (weather != null)
            {
                ConnectTown(weather, groups.Scene);
                view = weather;
            }
            else
                view = ConnectInterior(groups);
            var link = BuilderTools.FindSingleInScene<GameClockDaylight>(groups.Scene);
            if (link == null)
                link = BuilderTools
                    .CreateEmpty(
                        "GameClockDaylight",
                        groups.Systems,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .gameObject.AddComponent<GameClockDaylight>();
            BuilderTools.SetReference(link, "daylight", view);
        }

        /// <summary>마을 날씨 표시에 밤낮 설정과 밤에 밝히는 등을 잇는다. 날씨 Builder도 이 연결을 쓴다.</summary>
        public static void ConnectTown(WeatherPresenter weather, Scene scene)
        {
            BuilderTools.SetReference(weather, "daylight", Settings());
            SetLights(
                weather,
                "lamps",
                BuilderTools
                    .FindAllInScene<Light>(scene)
                    .Where(light =>
                        light.type != LightType.Directional && TownLampNames.Contains(light.name)
                    )
            );
        }

        private static InteriorDaylight ConnectInterior(SceneGroups groups)
        {
            var interior = BuilderTools.FindSingleInScene<InteriorDaylight>(groups.Scene);
            if (interior == null)
                interior = BuilderTools
                    .CreateEmpty(
                        "InteriorDaylight",
                        groups.Lighting,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .gameObject.AddComponent<InteriorDaylight>();
            BuilderTools.SetReference(interior, "daylight", Settings());
            Light[] lights = BuilderTools.FindAllInScene<Light>(groups.Scene);
            Light[] rooms = lights.Where(light => light.type != LightType.Directional).ToArray();
            SetLights(
                interior,
                "windowLights",
                lights.Where(light => light.type == LightType.Directional)
            );
            SetLights(interior, "fills", rooms.Where(light => light.name.Contains(FillLightName)));
            SetLights(interior, "lamps", rooms.Where(light => !light.name.Contains(FillLightName)));
            return interior;
        }

        private static DaylightSettings Settings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DaylightSettings>(SettingsPath);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder("Assets/Data/Weather");
            DaylightSettings settings = DaylightSettings.Create(
                GlowMaterialPaths.Select(path =>
                    BuilderTools.LoadRequiredAsset<Material>(path, MenuPath)
                ),
                WindowMaterialPaths.Select(path =>
                    BuilderTools.LoadRequiredAsset<Material>(path, MenuPath)
                )
            );
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void SetLights(Object owner, string propertyName, IEnumerable<Light> lights)
        {
            Light[] list = lights.ToArray();
            var fields = new SerializedObject(owner);
            SerializedProperty property = fields.FindProperty(propertyName);
            property.arraySize = list.Length;
            for (int index = 0; index < list.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = list[index];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
