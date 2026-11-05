using System.Collections.Generic;
using A305.ObjectHover;
using A305.Player;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 클릭·탭한 자리에서 맨 앞에 보이는 것이 NPC 몸일 때만 그 NPC를 고르는지 본다.
    /// 사물 카드가 뜨는 사물이나 벽이 앞에 있으면 NPC를 고르지 않는다.
    /// </summary>
    public class NpcPointerRaycastTests
    {
        // 다른 테스트가 만든 물체와 겹치지 않도록 멀리 둔다.
        private static readonly Vector3 Origin = new Vector3(6000f, 1f, 0f);
        private const float RayLengthMeters = 30f;

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (GameObject target in created)
            {
                Object.DestroyImmediate(target);
            }

            created.Clear();
        }

        [Test]
        public void Find_NpcBodyInFront_ReturnsThatNpc()
        {
            Passerby npc = CreateNpc(5f);

            Assert.That(Find(), Is.SameAs(npc));
        }

        [Test]
        public void Find_NothingInFront_ReturnsNull()
        {
            Assert.That(Find(), Is.Null);
        }

        [Test]
        public void Find_WallBeforeNpc_ReturnsNull()
        {
            CreateNpc(5f);
            CreateCube("Wall", 2f);

            Assert.That(Find(), Is.Null);
        }

        [Test]
        public void Find_ObjectWithWordCardBeforeNpc_ReturnsNull()
        {
            // 사물 카드가 뜨는 영역을 누르면 사물 카드가 뜨므로 그 뒤의 NPC에게 말을 걸지 않는다.
            CreateNpc(5f);
            GameObject cup = CreateCube("Cup", 2f);
            cup.GetComponent<BoxCollider>().isTrigger = true;
            cup.AddComponent<ObjectHoverHitArea>();

            Assert.That(Find(), Is.Null);
        }

        [Test]
        public void Find_OtherTriggerBeforeNpc_LooksThroughIt()
        {
            // 보이지 않는 감지 영역(문턱 등)은 NPC를 가리지 않는다.
            Passerby npc = CreateNpc(5f);
            CreateCube("DoorTrigger", 2f).GetComponent<BoxCollider>().isTrigger = true;

            Assert.That(Find(), Is.SameAs(npc));
        }

        [Test]
        public void Find_PlayerBetweenCameraAndNpc_LooksThroughPlayer()
        {
            // 3인칭 카메라는 플레이어 뒤에 있어 NPC를 누르면 광선이 플레이어 몸을 먼저 지난다.
            Passerby npc = CreateNpc(5f);
            var player = new GameObject("Player");
            created.Add(player);
            player.transform.position = Origin + Vector3.forward * 2f;
            player.AddComponent<PlayerMover>();
            Physics.SyncTransforms();

            Assert.That(Find(), Is.SameAs(npc));
        }

        private Passerby Find()
        {
            Physics.SyncTransforms();
            return new NpcPointerRaycast().Find(new Ray(Origin, Vector3.forward), RayLengthMeters);
        }

        private Passerby CreateNpc(float forwardMeters)
        {
            var npc = new GameObject("Npc");
            created.Add(npc);
            npc.transform.position = Origin + Vector3.forward * forwardMeters;
            return npc.AddComponent<Passerby>();
        }

        private GameObject CreateCube(string name, float forwardMeters)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(cube);
            cube.name = name;
            cube.transform.position = Origin + Vector3.forward * forwardMeters;
            return cube;
        }
    }
}
