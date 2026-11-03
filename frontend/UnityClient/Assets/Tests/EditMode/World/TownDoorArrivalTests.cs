using A305.EditorTools.Common;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
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

        /// <summary>
        /// 몸통보다 큰 머리(<see cref="HeadClearance"/>)는 문짝에 먼저 닿아 몸을 멈춘다. 가장 머리가 큰 캐릭터가 문 쪽으로 걸어
        /// 머리가 문짝에 닿은 자리에서도 몸이 감지 영역에 넉넉히 들어가야 문으로 들어간다. 얕게 겹치면 감지를 놓친다.
        /// </summary>
        [Test]
        public void WalkingIntoADoor_TheBodyReachesTheTriggerBeforeTheHeadMeetsTheDoor()
        {
            const float TriggerMarginMeters = .05f;
            Scene scene = EditorSceneManager.OpenScene(TownScenePath);
            var body = BuilderTools
                .FindSingleInScene<PlayerMover>(scene)
                .GetComponent<CharacterController>();
            PlayerCharacterDefinition biggestHead = BiggestHead();
            Physics.SyncTransforms();

            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(scene))
            {
                Transform front = door.ArrivalPoint;
                Vector3 toward = -front.forward;
                Vector3 head = front.position + Vector3.up * biggestHead.HeadCenterHeightMeters;
                Assert.That(
                    Physics.SphereCast(
                        head,
                        biggestHead.HeadRadiusMeters,
                        toward,
                        out RaycastHit hit,
                        2f,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    ),
                    Is.True,
                    $"{door.DoorwayId} 쪽으로 걸어도 머리가 닿는 문짝이 없습니다."
                );
                Vector3 stop = front.position + toward * (hit.distance - body.skinWidth);

                Vector3 centre = stop + body.center;
                Vector3 halfSpine = Vector3.up * (body.height / 2f - body.radius);
                Collider[] touched = Physics.OverlapCapsule(
                    centre - halfSpine,
                    centre + halfSpine,
                    body.radius - TriggerMarginMeters,
                    ~0,
                    QueryTriggerInteraction.Collide
                );
                Assert.That(
                    System.Array.Exists(
                        touched,
                        collider => collider.gameObject == door.gameObject
                    ),
                    Is.True,
                    $"{door.DoorwayId}: 머리가 {hit.collider.name}에 닿아 멈춘 자리에서 몸이 감지 영역에 닿지 않습니다."
                );
            }
        }

        private static PlayerCharacterDefinition BiggestHead()
        {
            PlayerCharacterDefinition biggest = null;
            foreach (string guid in AssetDatabase.FindAssets("t:PlayerCharacterDefinition"))
            {
                var character = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                if (biggest == null || character.HeadRadiusMeters > biggest.HeadRadiusMeters)
                    biggest = character;
            }

            Assert.That(biggest, Is.Not.Null, "캐릭터 정의가 없습니다.");
            return biggest;
        }
    }
}
