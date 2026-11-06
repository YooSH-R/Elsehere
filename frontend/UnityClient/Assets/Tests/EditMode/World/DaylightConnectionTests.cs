using System.Collections.Generic;
using A305.EditorTools.Common;
using A305.Weather;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    public sealed class DaylightConnectionTests
    {
        [TestCase("PoliceStationScene", typeof(InteriorDaylight))]
        public void ProductionSceneFollowsGameClockDayAndNight(string name, System.Type view)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var link = BuilderTools.FindSingleInScene<GameClockDaylight>(scene);
            Assert.That(
                link,
                Is.Not.Null,
                name + "에 게임 시계와 밤낮을 잇는 GameClockDaylight가 없습니다."
            );
            var daylight =
                new SerializedObject(link).FindProperty("daylight").objectReferenceValue
                as DaylightView;
            Assert.That(daylight, Is.InstanceOf(view), name);
            Assert.That(
                new SerializedObject(daylight).FindProperty("daylight").objectReferenceValue,
                Is.Not.Null,
                name + "의 밤낮 설정"
            );
        }

        [TestCase("PoliceStationScene")]
        public void InteriorLetsDaylightInThroughWindows(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");
            var interior = BuilderTools.FindSingleInScene<InteriorDaylight>(scene);
            Assert.That(interior, Is.Not.Null, name);
            Assert.That(
                new SerializedObject(interior).FindProperty("windowLights").arraySize,
                Is.GreaterThan(0),
                name + "의 창으로 드는 빛"
            );
        }

        // 가로등은 밤에만 켜는 목록에, 상점 벽등·실내등은 밤에 더 밝히는 목록에 둔다.
        [Test]
        public void TownLightsStreetLampsOnlyAtNight()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(scene);
            var fields = new SerializedObject(weather);

            string[] streetLamps = Names(fields.FindProperty("streetLamps"));
            Assert.That(
                streetLamps,
                Has.Length.GreaterThanOrEqualTo(16),
                "가로등 16개가 모두 연결되어야 합니다."
            );
            Assert.That(streetLamps, Is.All.EqualTo("LanternLight"));
            Assert.That(Names(fields.FindProperty("lamps")), Has.No.Member("LanternLight"));
        }

        private static string[] Names(SerializedProperty list)
        {
            var names = new string[list.arraySize];
            for (int index = 0; index < names.Length; index++)
                names[index] = list.GetArrayElementAtIndex(index).objectReferenceValue.name;
            return names;
        }

        [Test]
        public void TownLightsWindowsOfOpenShopsAndLodgingOnly()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var weather = BuilderTools.FindSingleInScene<WeatherPresenter>(scene);
            SerializedProperty buildings = new SerializedObject(weather).FindProperty(
                "litBuildings"
            );
            var names = new List<string>();
            for (int index = 0; index < buildings.arraySize; index++)
                names.Add(buildings.GetArrayElementAtIndex(index).objectReferenceValue.name);

            Assert.That(
                names,
                Is.SupersetOf(
                    new[]
                    {
                        "HomeBuilding",
                        "CafeBuilding",
                        "HotelBuilding",
                        "DrugstoreFront",
                        "PostOffice",
                        "MapleDiner",
                        "TownBank",
                        "SudsLaundromat",
                        "BloomFlowers",
                        "MainStreetBarber",
                        "PoliceStation",
                        "MapleBakery",
                        "LittleArcade",
                        "HomeLiving",
                    }
                )
            );
            Assert.That(names, Has.None.Contains("Residence"), "주택은 입점 건물이 아닙니다.");
            Assert.That(
                names,
                Has.None.EqualTo("VacantBuilding2F"),
                "빈 건물은 불을 켜지 않습니다."
            );
        }
    }
}
