using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 마을 본길에서는 택시와 승용차가 달리고, 픽업트럭은 길가에 세워 둔다.
    /// </summary>
    public sealed class TownTrafficTests
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";

        [Test]
        public void TaxiAndSedan_DriveAroundTheMainStreet()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            string[] driving = BuilderTools
                .FindAllInScene<LoopingCar>(scene)
                .Select(car => car.name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(driving, Is.EqualTo(new[] { "Sedan", "Taxi" }));
        }

        [Test]
        public void Cars_FollowTheIntersectionSignals()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            TrafficLights[] lights = BuilderTools.FindAllInScene<TrafficLights>(scene).ToArray();

            Assert.That(lights, Has.Length.EqualTo(1));
            Assert.That(lights[0].Heads, Has.Count.EqualTo(2));
            Assert.That(
                BuilderTools.FindAllInScene<LoopingCar>(scene).Select(car => car.Lights),
                Is.All.SameAs(lights[0])
            );
        }

        [Test]
        public void Taxi_HasADriverToTalkTo()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            TaxiRide taxi = BuilderTools.FindAllInScene<TaxiRide>(scene).Single();
            NpcIdentity driver = taxi.GetComponentInChildren<NpcIdentity>();

            Assert.That(taxi.name, Is.EqualTo("Taxi"));
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.Code, Is.EqualTo("TAXI_DRIVER"));
            Assert.That(driver.LookTarget, Is.Not.Null);
        }

        [Test]
        public void PickupTruck_StaysParked()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            GameObject[] pickups = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "KenneyDeliveryTruck")
                .Select(t => t.gameObject)
                .ToArray();

            Assert.That(pickups, Is.Not.Empty);
            Assert.That(pickups.Select(p => p.GetComponentInParent<LoopingCar>()), Is.All.Null);
        }
    }
}
