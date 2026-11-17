#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    [TestFixture("F")]
    [TestFixture("M")]
    public sealed class UmbrellaHoldTests
    {
        private readonly string selected;

        public UmbrellaHoldTests(string selected)
        {
            this.selected = selected;
        }

        [TearDown]
        public void ResetSelection() => PlayerCharacterSelection.Reset();

        [UnityTest]
        public IEnumerator Umbrella_CoversTheHeadWhenOpenedAndIsPutAwayWhenClosed()
        {
            UmbrellaHold hold = null;
            yield return LoadTownPlayer(found => hold = found);
            Animator animator = hold.GetComponentInChildren<Animator>();
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            hold.SetOpen(true);
            yield return Frames(10);

            using var body = new BodyClearanceProbe(animator, hold.transform);
            body.Record("umbrella");
            Bounds head = body.Head;
            Vector3 canopy = hold.transform.InverseTransformPoint(hold.Umbrella.Canopy.position);
            Assert.That(hold.Umbrella.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                hold.Hand.position.y,
                Is.GreaterThan(shoulder.position.y - .15f),
                "손잡이를 쥔 손이 어깨 높이로 올라와야 합니다."
            );
            Assert.That(canopy.z, Is.LessThan(head.center.z), "캐노피가 머리 뒤에 있어야 합니다.");
            Assert.That(
                Vector3.Dot(hold.Umbrella.Canopy.up, hold.transform.forward),
                Is.LessThan(-.3f),
                "마을에서도 우산 천과 우산대가 함께 뒤로 기울어져야 합니다."
            );
            Bounds cloth = hold.Umbrella.Canopy.GetComponentInChildren<Renderer>().bounds;
            float headTop = hold.transform.TransformPoint(new Vector3(0, head.max.y, 0)).y;
            Assert.That(
                cloth.max.y,
                Is.GreaterThan(headTop),
                "우산 천의 위쪽이 머리 위로 올라와야 합니다."
            );

            hold.SetOpen(false);
            yield return null;

            Assert.That(hold.IsOpen, Is.False);
            Assert.That(hold.Umbrella.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator FoldedUmbrella_StaysInTheLoweredHandAndCanBePutAway()
        {
            UmbrellaHold hold = null;
            yield return LoadTownPlayer(found => hold = found);
            Animator animator = hold.GetComponentInChildren<Animator>();
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            hold.SetFolded(true);
            yield return Frames(10);

            Assert.That(hold.Umbrella, Is.Not.Null);
            Assert.That(hold.IsOpen, Is.False);
            Assert.That(hold.Umbrella.gameObject.activeInHierarchy, Is.True);
            Assert.That(hold.Hand.position.y, Is.LessThan(shoulder.position.y - .2f));
            Renderer cloth = hold.Umbrella.Canopy.GetComponentInChildren<Renderer>();
            Assert.That(
                cloth.bounds.size.x,
                Is.LessThan(.3f),
                "접은 우산은 넓게 펼쳐져 있지 않습니다."
            );
            Assert.That(cloth.bounds.center.y, Is.LessThan(hold.Hand.position.y));
            float floor = hold.transform.position.y;
            Vector3 tip = hold.Umbrella.ShaftPoints[hold.Umbrella.ShaftPoints.Count - 1];
            Assert.That(
                tip.y,
                Is.GreaterThan(floor + .02f),
                "접은 우산 끝이 바닥을 뚫지 않습니다."
            );
            Assert.That(
                cloth.bounds.min.y,
                Is.GreaterThan(floor),
                "접은 천이 바닥을 뚫지 않습니다."
            );
            Vector3 knee = hold.transform.InverseTransformPoint(
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position
            );
            Assert.That(
                hold.transform.InverseTransformPoint(tip).x,
                Is.GreaterThan(knee.x + .12f),
                "접은 우산은 다리를 뚫지 않게 몸 바깥으로 듭니다."
            );

            hold.SetFolded(false);
            yield return null;
            Assert.That(hold.Umbrella.gameObject.activeInHierarchy, Is.False);
        }

        private IEnumerator LoadTownPlayer(System.Action<UmbrellaHold> found)
        {
            PlayerCharacterSelection.Select(selected);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Production/TownScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var hold = player.GetComponent<UmbrellaHold>();
            Assert.That(hold, Is.Not.Null, "마을 플레이어에 우산 들기가 연결돼야 합니다.");
            found(hold);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
#endif
