#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    public class SeatedArmsTests
    {
        private GameObject root;
        private GameObject seat;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(seat);
        }

        [UnityTest]
        public IEnumerator Female_HandsRestInFrontOfTheBodyAndRestore() => CheckPose("F");

        [UnityTest]
        public IEnumerator Male_HandsRestInFrontOfTheBodyAndRestore() => CheckPose("M");

        private IEnumerator CheckPose(string characterId)
        {
            root = new GameObject("Seated arms test");
            root.SetActive(false);
            root.AddComponent<CharacterController>();
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/" + characterId + ".asset"
            );
            var visual = Object.Instantiate(definition.VisualPrefab, root.transform);
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            root.SetActive(true);
            yield return null;
            var animator = visual.GetComponentInChildren<Animator>();
            animator.Update(0);
            animator.enabled = false;
            seat = new GameObject("Seat");
            seat.transform.rotation = Quaternion.Euler(0, 47, 0);
            root.transform.rotation = seat.transform.rotation;
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 leftStanding = root.transform.InverseTransformPoint(leftHand.position);
            Vector3 rightStanding = root.transform.InverseTransformPoint(rightHand.position);
            Vector3 originalPosition = visual.transform.localPosition;
            var arms = SeatedArms.Find(animator);
            SeatedLegs
                .Find(animator)
                .Apply(visual.transform, originalPosition, seat.transform, .45f, .2f);
            arms.Apply(seat.transform);

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach (Transform hand in new[] { leftHand, rightHand })
            {
                Vector3 offset = seat.transform.InverseTransformDirection(
                    hand.position - hips.position
                );
                Assert.That(
                    offset.z,
                    Is.GreaterThan(.1f),
                    "손은 몸 뒤에 묻히지 않고 허벅지 앞에 있어야 합니다."
                );
                Assert.That(
                    hand.position.y,
                    Is.GreaterThan(.48f),
                    "손은 좌판 안으로 내려가지 않습니다."
                );
            }
            using var body = new BodyClearanceProbe(animator, root.transform);
            body.Record("seated");
            Assert.That(body.MinimumHeadDistance, Is.GreaterThan(.95f), body.WorstPose);
            Assert.That(body.MinimumTorsoDistance, Is.GreaterThan(.95f), body.WorstTorsoPose);

            arms.Restore();
            visual.transform.localPosition = originalPosition;
            Assert.That(
                Vector3.Distance(
                    root.transform.InverseTransformPoint(leftHand.position),
                    leftStanding
                ),
                Is.LessThan(.001f)
            );
            Assert.That(
                Vector3.Distance(
                    root.transform.InverseTransformPoint(rightHand.position),
                    rightStanding
                ),
                Is.LessThan(.001f)
            );
        }
    }
}
#endif
