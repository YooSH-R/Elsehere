using A305.Dialogue;
using A305.EditorTools.Common;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Wayfinding.Tests
{
    /// <summary>마을에는 길 안내가 하나 있고, 플레이어·대화와 이어져 있으며, 미션 장소마다 출입문 앞을 찾는다.</summary>
    public sealed class TownGuideConnectionTests
    {
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string SettingsPath = "Assets/Data/Wayfinding/DestinationGuideSettings.asset";

        // 모든 검사가 마을 씬을 읽기만 하므로 한 번만 연다.
        private Scene scene;

        [OneTimeSetUp]
        public void OpenTown()
        {
            scene = EditorSceneManager.OpenScene(TownScenePath);
        }

        [Test]
        public void TownHasOneGuideConnectedToThePlayerAndDialogue()
        {
            var guide = BuilderTools.FindSingleInScene<DestinationGuide>(scene);
            Assert.That(guide, Is.Not.Null, "마을에 길 안내가 없습니다.");
            var fields = new SerializedObject(guide);
            Assert.That(
                fields.FindProperty("settings").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<DestinationGuideSettings>(SettingsPath))
            );
            Assert.That(
                fields.FindProperty("player").objectReferenceValue,
                Is.SameAs(BuilderTools.FindSingleInScene<PlayerMover>(scene))
            );
            Assert.That(
                fields.FindProperty("dialogue").objectReferenceValue,
                Is.SameAs(BuilderTools.FindSingleInScene<DialogueController>(scene))
            );
            BuilderTools.RequireReferences(guide);
        }

        // 횡단보도 구역 상자와 길·출입문 빛은 보이기만 하는 안내라 사물 단어(호버)를 붙이지 않는다.
        [Test]
        public void GuideAndCrossingBoxesCarryNoObjectWord()
        {
            var guide = BuilderTools.FindSingleInScene<DestinationGuide>(scene);
            Assert.That(guide, Is.Not.Null, "마을에 길 안내가 없습니다.");
            Assert.That(guide.GetComponentsInChildren<HoverableObject>(true), Is.Empty);
            foreach (
                NavMeshModifierVolume crossing in BuilderTools.FindAllInScene<NavMeshModifierVolume>(
                    scene
                )
            )
                Assert.That(
                    crossing.GetComponentsInChildren<HoverableObject>(true),
                    Is.Empty,
                    crossing.name
                );
        }

        [TestCase("HOME", "HomeDoor")]
        [TestCase("CAFE", "CafeDoor")]
        [TestCase("HOTEL", "HotelDoor")]
        [TestCase("SHOP", "ShopDoor")]
        [TestCase("POLICE_STATION", "PoliceStationDoor")]
        public void EveryTownPlace_HasItsDoorFrontAsTheEntrance(string placeCode, string doorwayId)
        {
            SpaceDoorway door = System.Array.Find(
                BuilderTools.FindAllInScene<SpaceDoorway>(scene),
                doorway => doorway.DoorwayId == doorwayId
            );

            Assert.That(
                DestinationGuide.FindEntrance(scene, placeCode),
                Is.SameAs(door.ArrivalPoint)
            );
        }

        [TestCase("TOWN")]
        [TestCase(null)]
        public void PlaceWithoutATownDoor_HasNoEntrance(string placeCode)
        {
            Assert.That(DestinationGuide.FindEntrance(scene, placeCode), Is.Null);
        }
    }
}
