using A305.EditorTools.Common;
using A305.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 건물에서 마을로 나오면 문을 등지고 문 앞 인도 1m에 선다. 선 자리에서 캐릭터가 문 감지 영역에 닿으면
    /// 나오자마자 다시 들어가므로, 감지 영역은 선 자리의 캐릭터와 겹치지 않아야 한다.
    /// </summary>
    public sealed class TownDoorArrivalTests
    {
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";

        [Test]
        public void ArrivalPointStandsOneMetreInFrontOfEveryTownDoor()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath);

            SpaceDoorway[] doors = BuilderTools.FindAllInScene<SpaceDoorway>(scene);
            Assert.That(doors, Is.Not.Empty);
            foreach (SpaceDoorway door in doors)
            {
                Vector3 local = door.transform.InverseTransformPoint(door.ArrivalPoint.position);
                Assert.That(local.z, Is.EqualTo(1f).Within(.01f), $"{door.DoorwayId} 앞 거리");
                Assert.That(
                    new Vector2(local.x, local.y).magnitude,
                    Is.LessThan(.01f),
                    $"{door.DoorwayId} 정면에서 벗어남"
                );
                Assert.That(
                    Vector3.Angle(door.ArrivalPoint.forward, door.transform.forward),
                    Is.LessThan(1f),
                    $"{door.DoorwayId}에서 나오면 문을 등져야 합니다."
                );
            }
        }

        [Test]
        public void PlayerStandingAtArrivalDoesNotTouchAnyDoorTrigger()
        {
            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            var body = BuilderTools
                .FindSingleInScene<PlayerMover>(scene)
                .GetComponent<CharacterController>();
            Physics.SyncTransforms();

            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(scene))
            {
                Vector3 centre = door.ArrivalPoint.position + body.center;
                float radius = body.radius + body.skinWidth;
                Vector3 halfSpine = Vector3.up * (body.height / 2f - body.radius);
                Collider[] touched = Physics.OverlapCapsule(
                    centre - halfSpine,
                    centre + halfSpine,
                    radius,
                    ~0,
                    QueryTriggerInteraction.Collide
                );

                foreach (Collider collider in touched)
                    Assert.That(
                        collider.GetComponent<SpaceDoorway>(),
                        Is.Null,
                        $"{door.DoorwayId} 앞에 선 캐릭터가 {collider.name} 감지 영역에 닿습니다."
                    );
            }
        }
    }
}
