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
        public IEnumerator OpenUmbrella_RaisesTheHandAndCoversTheHead()
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
            Assert.That(
                canopy.y,
                Is.GreaterThan(head.max.y),
                "캐노피 아랫면이 머리 위에 있어야 합니다."
            );
            Assert.That(
                canopy.y,
                Is.LessThan(head.max.y + .3f),
                "캐노피가 머리에서 너무 떠 있지 않아야 합니다."
            );
            Bounds cloth = hold.Umbrella.Canopy.GetComponentInChildren<Renderer>().bounds;
            Vector3 headCenter = hold.transform.TransformPoint(head.center);
            Assert.That(
                Vector2.Distance(
                    new Vector2(headCenter.x, headCenter.z),
                    new Vector2(cloth.center.x, cloth.center.z)
                ),
                Is.LessThan(cloth.extents.x - head.extents.x),
                "캐노피가 머리 전체를 덮어야 합니다."
            );
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

            hold.SetFolded(false);
            yield return null;
            Assert.That(hold.Umbrella.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosedUmbrella_IsPutAway()
        {
            UmbrellaHold hold = null;
            yield return LoadTownPlayer(found => hold = found);

            hold.SetOpen(true);
            yield return null;
            hold.SetOpen(false);
            yield return null;

            Assert.That(hold.IsOpen, Is.False);
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
