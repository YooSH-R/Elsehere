using A305.EditorTools.Common;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>배포용 장소 씬과 마을 매장이 영업시간 처리에 연결되어 있는지 확인한다.</summary>
    public sealed class OpeningHoursConnectionTests
    {
        private const string PlaceHoursPath = "Assets/Data/World/PlaceHours.json";

        [TearDown]
        public void DiscardOpenedScene()
        {
            // 매장 실내를 나누는 처리는 열어 둔 씬을 바꾸므로 저장하지 않고 닫는다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase("PoliceStationScene")]
        public void ProductionScene_ReadsPlaceHoursAndShowsNoticesForItsPlayer(string name)
        {
            Scene scene = OpenProductionScene(name);
            var controller = BuilderTools.FindSingleInScene<OpeningHoursController>(scene);

            Assert.That(controller, Is.Not.Null, name);
            Assert.That(
                Reference(controller, "placeHours"),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<TextAsset>(PlaceHoursPath)),
                name
            );
            Assert.That(Reference(controller, "notice"), Is.InstanceOf<HudNoticeView>(), name);
            Assert.That(
                Reference(controller, "player"),
                Is.EqualTo(BuilderTools.FindSingleInScene<PlayerMover>(scene)),
                name
            );
        }

        private static Scene OpenProductionScene(string name) =>
            EditorSceneManager.OpenScene("Assets/Scenes/Production/" + name + ".unity");

        private static Object Reference(Object owner, string field) =>
            new SerializedObject(owner).FindProperty(field)?.objectReferenceValue;
    }
}
