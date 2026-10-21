using System.Linq;
using A305.EditorTools.World;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 디자인한 공간 씬에 기능 연결 도구를 적용하면 기능 객체가 늘 같은 방식으로 붙고, 디자인은 그대로 남는지 확인한다.
    /// </summary>
    public class SpaceSceneSetupTests
    {
        private const float Tolerance = 0.001f;

        private static readonly string[] GroupNames =
        {
            "Systems",
            "Environment",
            "Characters",
            "UI",
            "Lighting",
        };

        private Scene scene;

        [SetUp]
        public void CreateNewScene()
        {
            // 디자이너가 File → New Scene으로 만든 씬처럼 기본 카메라와 조명에서 시작한다.
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );
        }

        [Test]
        public void Apply_ToNewScene_KeepsOnlyConventionGroupsAtRoot()
        {
            SpaceSceneSetup.Apply(scene);

            string[] rootNames = scene.GetRootGameObjects().Select(root => root.name).ToArray();
            Assert.That(rootNames, Is.EquivalentTo(GroupNames));
            Assert.That(Root("Lighting").GetComponentInChildren<Light>(), Is.Not.Null);
        }

        [Test]
        public void Apply_AddsPlayerCameraAndArrivalConnectedToEachOther()
        {
            SpaceSceneSetup.Apply(scene);

            PlayerMover player = Single<PlayerMover>();
            PlayerCameraRig cameraRig = Single<PlayerCameraRig>();
            SpaceArrival arrival = Single<SpaceArrival>();
            Assert.That(player.transform.parent, Is.EqualTo(Root("Characters")));
            Assert.That(cameraRig.transform.parent, Is.EqualTo(Root("Systems")));
            Assert.That(arrival.transform.parent, Is.EqualTo(Root("Systems")));
            Assert.That(Reference(player, "cameraTransform"), Is.EqualTo(cameraRig.transform));
            Assert.That(Reference(cameraRig, "followTarget"), Is.EqualTo(player.transform));
            Assert.That(Reference(arrival, "player"), Is.EqualTo(player));
            Assert.That(Reference(arrival, "cameraRig"), Is.EqualTo(cameraRig));
        }

        [Test]
        public void Apply_ReplacesDefaultCameraWithPlayerCamera()
        {
            SpaceSceneSetup.Apply(scene);

            Camera camera = Single<Camera>();
            Assert.That(camera.GetComponent<PlayerCameraRig>(), Is.Not.Null);
        }

        [Test]
        public void Apply_Twice_DoesNotAddFeatureObjectsAgain()
        {
            SpaceSceneSetup.Apply(scene);

            SpaceSceneSetup.Apply(scene);

            Assert.That(scene.GetRootGameObjects(), Has.Length.EqualTo(GroupNames.Length));
            Assert.That(FindAll<PlayerMover>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<PlayerCameraRig>(), Has.Length.EqualTo(1));
            Assert.That(FindAll<SpaceArrival>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void Apply_Again_KeepsDesignedEnvironmentAndPlayerPlacement()
        {
            SpaceSceneSetup.Apply(scene);
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "DesignedBuilding";
            building.transform.SetParent(Root("Environment"), false);
            building.transform.position = new Vector3(3f, 1f, 4f);
            Single<PlayerMover>().transform.position = new Vector3(-2f, 0f, 5f);

            SpaceSceneSetup.Apply(scene);

            Assert.That(Root("Environment").Find("DesignedBuilding"), Is.EqualTo(building.transform));
            AssertPosition(new Vector3(3f, 1f, 4f), building.transform.position);
            AssertPosition(new Vector3(-2f, 0f, 5f), Single<PlayerMover>().transform.position);
        }

        [Test]
        public void Apply_ToSceneWithDoorway_StartsPlayerAtDoorwayArrival()
        {
            var doorwayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SpaceSceneBuilder.DoorwayPrefabPath
            );
            var doorway = (GameObject)PrefabUtility.InstantiatePrefab(doorwayPrefab, scene);
            doorway.transform.SetPositionAndRotation(
                new Vector3(10f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f)
            );

            SpaceSceneSetup.Apply(scene);

            Transform arrivalPoint = doorway.GetComponent<SpaceDoorway>().ArrivalPoint;
            Transform player = Single<PlayerMover>().transform;
            AssertPosition(arrivalPoint.position, player.position);
            Assert.That(Quaternion.Angle(arrivalPoint.rotation, player.rotation), Is.LessThan(0.01f));
        }

        private Transform Root(string rootName)
        {
            GameObject root = scene
                .GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == rootName);
            Assert.That(root, Is.Not.Null, $"{rootName} 그룹이 없습니다.");
            return root.transform;
        }

        private T Single<T>()
            where T : Component
        {
            T[] components = FindAll<T>();
            Assert.That(components, Has.Length.EqualTo(1), $"{typeof(T).Name} 개수");
            return components[0];
        }

        private T[] FindAll<T>()
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static Object Reference(Object owner, string propertyName)
        {
            return new SerializedObject(owner).FindProperty(propertyName).objectReferenceValue;
        }

        private static void AssertPosition(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance));
        }
    }
}
