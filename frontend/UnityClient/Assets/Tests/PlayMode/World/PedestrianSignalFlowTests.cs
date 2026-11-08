using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    public sealed class PedestrianSignalFlowTests
    {
        [UnityTest]
        public IEnumerator NewCrossing_StopsBothCarsAndReleasesThemWithVehicleGreen()
        {
            var root = new GameObject("고정 신호 검사");
            float previousDelta = Time.captureDeltaTime;
            try
            {
                var lights = Child(root, "신호").AddComponent<TrafficLights>();
                lights.enabled = false;
                Set(lights, "elapsedSeconds", 15f);
                var head = Child(root, "보행 신호").AddComponent<PedestrianSignalHead>();
                Set(head, "crossesMainRoad", true);
                // 다른 장면의 사람·차와 겹치지 않는 좌표를 쓴다.
                Set(head, "crossingPoint", new Vector3(0, 0, 500));
                Set(lights, "pedestrianHeads", new List<PedestrianSignalHead> { head });
                Assert.That(
                    lights.CanCross(true, TrafficSignalCycle.RequiredCrossingSeconds),
                    Is.True
                );
                var route = new CarLoopRoute(480, 530, 1.5f);
                LoopingCar north = Car(root, lights, "북행", route.NorthboundDistance(495));
                LoopingCar south = Car(root, lights, "남행", route.SouthboundDistance(505));
                Time.captureDeltaTime = 1f / 60f;
                for (int frame = 0; frame < 240; frame++)
                    yield return null;
                Assert.That(north.transform.position.z, Is.EqualTo(496.35f).Within(.01f));
                Assert.That(south.transform.position.z, Is.EqualTo(503.65f).Within(.01f));
                Vector3 northWaiting = north.transform.position;
                Vector3 southWaiting = south.transform.position;
                for (int frame = 0; frame < 30; frame++)
                    yield return null;
                Assert.That(north.transform.position, Is.EqualTo(northWaiting));
                Assert.That(south.transform.position, Is.EqualTo(southWaiting));
                Set(lights, "elapsedSeconds", 0f);
                Assert.That(lights.CanCross(true, 0), Is.False);
                for (int frame = 0; frame < 60; frame++)
                    yield return null;
                Assert.That(north.transform.position.z, Is.GreaterThan(northWaiting.z + 1));
                Assert.That(south.transform.position.z, Is.LessThan(southWaiting.z - 1));
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
                Object.DestroyImmediate(root);
            }
        }

        private static LoopingCar Car(
            GameObject root,
            TrafficLights lights,
            string name,
            float start
        )
        {
            GameObject body = Child(root, name);
            body.SetActive(false);
            var car = body.AddComponent<LoopingCar>();
            Set(car, "southZ", 480f);
            Set(car, "northZ", 530f);
            Set(car, "startDistance", start);
            Set(car, "lights", lights);
            body.SetActive(true);
            return car;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        private static void Set(object target, string field, object value) =>
            target
                .GetType()
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
    }
}
