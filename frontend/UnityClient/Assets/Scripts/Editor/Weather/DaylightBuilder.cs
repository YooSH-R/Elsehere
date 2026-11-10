using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Deployment;
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
    /// 있으면 조정한 값을 그대로 두고 가로등 전구만 밤에만 켜는 목록으로 옮긴다. 씬의 조명과 재질은 바꾸지 않고 참조만 맞춘다.
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
            "PoliceStationScene",
        };

        // 밤에 더 밝히는 발광 재질: 마을 창빛·상점 조명과 실내 조명 갓.
        private static readonly string[] GlowMaterialPaths =
        {
            "Assets/Art/World/MainstreetArt/WindowAmberGlow.mat",
            "Assets/Art/World/MainstreetArt/PendantLight.mat",
            "Assets/Art/World/CafeBlenderKit/Materials/Glow.mat",
            "Assets/Art/World/TownStorefronts/CeilingLightPanel.mat",
            "Assets/Art/Hotel/MapleHouse/OpalLight.mat",
            "Assets/Art/World/Drugstore/Materials/DrugstoreLightPanel.mat",
        };

        // 밤에만 켜는 가로등 전구. 낮에는 끈다.
        private const string StreetLampGlowPath =
            "Assets/Art/World/MainstreetArt/LanternAmberV2.mat";

        // 실내 창밖 하늘을 그린 유리.
        private static readonly string[] WindowMaterialPaths =
        {
            "Assets/Art/Home/CozyCottage/WindowDaylight.mat",
            "Assets/Art/Cafe/CornerCafe/DaylightGlass.mat",
            "Assets/Art/World/Drugstore/Materials/DrugstoreDaylightGlass.mat",
        };

        // 밤에 입점한 상점·숙소 건물에서 안에서 불을 켠 것처럼 빛나게 할 창 유리.
        private static readonly string[] LitWindowMaterialPaths =
        {
            "Assets/Art/World/MainstreetArt/WindowBlue.mat",
            "Assets/Art/World/MainstreetArt/GlassLight.mat",
            "Assets/Art/World/MainstreetArt/SkyGlassV2.mat",
            "Assets/Art/World/CafeBlenderKit/Materials/Glass.mat",
        };

        // 마을에서 들어가 볼 수 있는 이용 건물: 내 숙소(집)·카페·호텔·드럭스토어.
        private static readonly string[] OpenSpaceBuildings =
        {
            "HomeBuilding",
            "CafeBuilding",
            "HotelBuilding",
            "DrugstoreFront",
        };

        private const string BackgroundBuildingsPath = "TownMainstreet/BackgroundBuildings";

        // 배경 건물 가운데 매장 실내가 있는 건물이 입점한 상점이다. 주택·빈 건물에는 실내가 없다.
        private const string ShopInteriorName = "Interior";

        // 마을에서 밤에 더 밝히는 상점 벽등·실내등. 순찰차 경광등처럼 따로 켜고 끄는 빛은 넣지 않는다.
        private static readonly string[] TownLampNames = { "FixtureLight", "InteriorLight" };

        // 밤에만 켜는 가로등 빛(StreetLampV2 안)
        private const string StreetLampLightName = "LanternLight";

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

        /// <summary>
        /// 마을 날씨 표시에 밤낮 설정과 밤에 밝히는 등, 밤에 창이 빛나는 입점 건물을 잇는다. 날씨 Builder도 이 연결을 쓴다.
        /// </summary>
        public static void ConnectTown(WeatherPresenter weather, Scene scene)
        {
            WebShadowSettingsBuilder.ConnectShadowQuality(scene);
            BuilderTools.SetReference(weather, "daylight", Settings());
            Light[] lights = BuilderTools
                .FindAllInScene<Light>(scene)
                .Where(light => light.type != LightType.Directional)
                .ToArray();
            SetList(weather, "lamps", lights.Where(light => TownLampNames.Contains(light.name)));
            SetList(
                weather,
                "streetLamps",
                lights.Where(light => light.name == StreetLampLightName)
            );
            SetList(weather, "litBuildings", OpenBuildings(scene));
        }

        /// <summary>입점한 상점·숙소 건물. 이용 건물과, 배경 건물 가운데 켜져 있고 매장 실내가 있는 건물이다.</summary>
        public static IEnumerable<Transform> OpenBuildings(Scene scene)
        {
            Transform environment = scene
                .GetRootGameObjects()
                .Select(root => root.transform)
                .FirstOrDefault(root => root.name == "Environment");
            if (environment == null)
                yield break;
            foreach (string name in OpenSpaceBuildings)
            {
                Transform building = environment.Find(name);
                if (building != null)
                    yield return building;
            }
            Transform background = environment.Find(BackgroundBuildingsPath);
            if (background == null)
                yield break;
            foreach (Transform building in background)
            {
                if (building.gameObject.activeSelf && building.Find(ShopInteriorName) != null)
                    yield return building;
            }
        }

        private static InteriorDaylight ConnectInterior(SceneGroups groups)
        {
            WebShadowSettingsBuilder.ConnectShadowQuality(groups.Scene);
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
            SetList(
                interior,
                "windowLights",
                lights.Where(light => light.type == LightType.Directional)
            );
            SetList(interior, "fills", rooms.Where(light => light.name.Contains(FillLightName)));
            SetList(interior, "lamps", rooms.Where(light => !light.name.Contains(FillLightName)));
            return interior;
        }

        private static DaylightSettings Settings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DaylightSettings>(SettingsPath);
            if (existing != null)
            {
                MoveStreetLampGlow(existing);
                return existing;
            }
            BuilderTools.EnsureFolder("Assets/Data/Weather");
            DaylightSettings settings = DaylightSettings.Create(
                GlowMaterialPaths.Select(path =>
                    BuilderTools.LoadRequiredAsset<Material>(path, MenuPath)
                ),
                WindowMaterialPaths.Select(path =>
                    BuilderTools.LoadRequiredAsset<Material>(path, MenuPath)
                ),
                LitWindowMaterialPaths.Select(path =>
                    BuilderTools.LoadRequiredAsset<Material>(path, MenuPath)
                ),
                new[] { BuilderTools.LoadRequiredAsset<Material>(StreetLampGlowPath, MenuPath) }
            );
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        // 가로등 전구를 밤에 더 밝히는 목록에서 밤에만 켜는 목록으로 옮긴다. 다른 조정 값은 그대로 둔다.
        private static void MoveStreetLampGlow(DaylightSettings settings)
        {
            var lamp = BuilderTools.LoadRequiredAsset<Material>(StreetLampGlowPath, MenuPath);
            if (settings.NightOnlyGlowMaterials.Contains(lamp))
                return;
            SetList(settings, "glowMaterials", settings.GlowMaterials.Where(item => item != lamp));
            SetList(
                settings,
                "nightOnlyGlowMaterials",
                settings.NightOnlyGlowMaterials.Append(lamp)
            );
            EditorUtility.SetDirty(settings);
        }

        private static void SetList<T>(Object owner, string propertyName, IEnumerable<T> items)
            where T : Object
        {
            T[] list = items.ToArray();
            var fields = new SerializedObject(owner);
            SerializedProperty property = fields.FindProperty(propertyName);
            property.arraySize = list.Length;
            for (int index = 0; index < list.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = list[index];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
