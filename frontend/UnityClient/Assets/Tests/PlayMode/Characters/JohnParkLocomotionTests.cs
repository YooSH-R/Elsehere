#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Characters.PlayModeTests
{
    public sealed class JohnParkLocomotionTests
    {
        [UnityTest]
        public IEnumerator MovingBounceFollowsSpeechTimeWithoutChangingFootTargets()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var panel = new GameObject("보행 강세 검사");
            try
            {
                var performer = root.GetComponent<RecordedNpcPerformance>();
                var speech = root.GetComponent<CharacterSpeechPlayer>();
                var motion = root.GetComponent<JohnParkLocomotion>();
                var link = panel.AddComponent<DialogueServerLink>();
                link.SetSpeaker(speech);
                root.GetComponent<AudioSource>().mute = true;
                yield return null;
                motion.enabled = false;
                Assert.That(performer.Play(link), Is.True);
                speech.Pause();
                var bones = root.GetComponentsInChildren<Transform>();
                var hips = bones.Single(b => b.name == "Hips");
                var feet = new[] { "Left_Foot", "Right_Foot" }
                    .Select(name => bones.Single(b => b.name == name))
                    .ToArray();
                var chest = bones.Single(b => b.name == "Chest");
                var hand = bones.Single(b => b.name == "Right_Hand");
                foreach (float speed in new[] { 1f, 2f })
                {
                    performer.SampleMotion(1.95f);
                    motion.SampleAt(.1f, speed);
                    float relaxedHeight = hips.position.y;
                    var targets = feet.Select(f => f.position).ToArray();
                    var rotations = feet.Select(f => f.rotation).ToArray();
                    var grip = chest.InverseTransformPoint(hand.position);
                    performer.SampleMotion(1.32f);
                    motion.SampleAt(.1f, speed);
                    Assert.That(
                        relaxedHeight - hips.position.y,
                        Is.GreaterThan(.015f),
                        "같은 발걸음에서도 실제 음성 강세에서 더 깊게 눌러야 한다."
                    );
                    for (int i = 0; i < feet.Length; i++)
                    {
                        Assert.That(
                            Vector3.Distance(targets[i], feet[i].position),
                            Is.LessThan(.001f)
                        );
                        Assert.That(
                            Quaternion.Angle(rotations[i], feet[i].rotation),
                            Is.LessThan(.1f)
                        );
                    }
                    Assert.That(
                        Vector3.Distance(grip, chest.InverseTransformPoint(hand.position)),
                        Is.LessThan(.0005f)
                    );
                    var first = hips.position;
                    performer.SampleMotion(1.32f);
                    motion.SampleAt(.1f, speed);
                    Assert.That(
                        Vector3.Distance(first, hips.position),
                        Is.LessThan(.0001f),
                        "일시정지 중 같은 음성 시각을 다시 평가해도 누적 변형이 없어야 한다."
                    );
                }
                performer.Stop(link);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(panel);
            }
        }

        [UnityTest]
        public IEnumerator SilentMovementVariesAccentsAcrossStepCycles()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            try
            {
                yield return null;
                var motion = root.GetComponent<JohnParkLocomotion>();
                motion.enabled = false;
                var animator = root.GetComponentInChildren<Animator>();
                var rhythm = animator.runtimeAnimatorController.animationClips.Single();
                var hips = root.GetComponentsInChildren<Transform>().Single(b => b.name == "Hips");
                foreach (float speed in new[] { 1f, 2f })
                {
                    float cycle =
                        speed == 1 ? motion.Settings.Walk.length : motion.Settings.Run.length;
                    float minimum = float.PositiveInfinity,
                        maximum = float.NegativeInfinity;
                    for (int i = 0; i < 8; i++)
                    {
                        rhythm.SampleAnimation(animator.gameObject, 0);
                        motion.SampleAt(.1f + i * cycle, speed);
                        minimum = Mathf.Min(minimum, hips.position.y);
                        maximum = Mathf.Max(maximum, hips.position.y);
                    }
                    Assert.That(
                        maximum - minimum,
                        Is.GreaterThan(.015f),
                        "랩이 없어도 매 걸음 같은 시점에 같은 깊이로 반복하지 않아야 한다."
                    );
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator WalkingWhileRappingKeepsSpeechAndRhythmActive()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var panel = new GameObject("하반신·랩 동시 재생 검사");
            try
            {
                var speech = root.GetComponent<CharacterSpeechPlayer>();
                var performer = root.GetComponent<RecordedNpcPerformance>();
                var driver = root.GetComponent<CharacterAnimationDriver>();
                var link = panel.AddComponent<DialogueServerLink>();
                root.GetComponent<AudioSource>().mute = true;
                link.SetSpeaker(speech);
                yield return null;
                Assert.That(performer.Play(link), Is.True);
                driver.SetLocomotion(1, true, 0);
                var shin = root.GetComponentsInChildren<Transform>()
                    .Single(b => b.name == "Left_LowerLeg");
                var first = shin.localRotation;
                float legMotion = 0,
                    mouthPeak = 0;
                for (int sample = 0; sample < 12; sample++)
                {
                    yield return new WaitForSecondsRealtime(.1f);
                    Assert.That(performer.IsPerforming, Is.True);
                    Assert.That(speech.IsPlaying, Is.True);
                    legMotion = Mathf.Max(legMotion, Quaternion.Angle(first, shin.localRotation));
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                            if (skin.sharedMesh.GetBlendShapeName(i).StartsWith("viseme_"))
                                mouthPeak = Mathf.Max(mouthPeak, skin.GetBlendShapeWeight(i));
                }
                Assert.That(
                    legMotion,
                    Is.GreaterThan(10),
                    "공연이 Animator 시간을 고정해도 다리는 움직여야 한다."
                );
                Assert.That(mouthPeak, Is.GreaterThan(20));
                Assert.That(speech.PlaybackTime, Is.GreaterThan(.8f));
                link.StopSpeech();
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(panel);
            }
        }

        [UnityTest]
        public IEnumerator StoppingMovementReturnsToTheOriginalRhythmPose()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
            );
            var root = Object.Instantiate(prefab);
            var reference = Object.Instantiate(prefab);
            try
            {
                reference.GetComponent<JohnParkLocomotion>().enabled = false;
                var referenceAnimator = reference.GetComponentInChildren<Animator>();
                referenceAnimator.enabled = false;
                var animator = root.GetComponentInChildren<Animator>();
                var driver = root.GetComponent<CharacterAnimationDriver>();
                yield return null;
                driver.SetLocomotion(2, true, 0);
                yield return new WaitForSecondsRealtime(.3f);
                driver.SetLocomotion(0, true, 0);
                yield return new WaitForSecondsRealtime(.3f);
                var clip = animator.runtimeAnimatorController.animationClips.Single();
                clip.SampleAnimation(
                    referenceAnimator.gameObject,
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime
                        * clip.length
                        % clip.length
                );
                var expected = reference
                    .GetComponentsInChildren<Transform>()
                    .Single(b => b.name == "Left_LowerLeg");
                var actual = root.GetComponentsInChildren<Transform>()
                    .Single(b => b.name == "Left_LowerLeg");
                Assert.That(
                    Quaternion.Angle(expected.localRotation, actual.localRotation),
                    Is.LessThan(.5f)
                );
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(reference);
            }
        }

        [UnityTest]
        public IEnumerator MovingJohnAnimatesHisLegsWithoutChangingTheHeldArm()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            try
            {
                var driver = root.GetComponent<CharacterAnimationDriver>();
                Assert.That(
                    driver,
                    Is.Not.Null,
                    "기존 이동 입력으로 John의 걷기·달리기를 선택할 수 있어야 한다."
                );
                var bones = root.GetComponentsInChildren<Transform>();
                var chest = bones.Single(b => b.name == "Chest");
                var hand = bones.Single(b => b.name == "Right_Hand");
                var shin = bones.Single(b => b.name == "Left_LowerLeg");
                yield return null;
                var grip = chest.InverseTransformPoint(hand.position);
                var gripRotation = Quaternion.Inverse(chest.rotation) * hand.rotation;
                foreach (float speed in new[] { 1f, 2f })
                {
                    driver.SetLocomotion(speed, true, 0);
                    var first = shin.localRotation;
                    float motion = 0;
                    for (int sample = 0; sample < 12; sample++)
                    {
                        yield return new WaitForSecondsRealtime(.05f);
                        motion = Mathf.Max(motion, Quaternion.Angle(first, shin.localRotation));
                        Assert.That(
                            Vector3.Distance(grip, chest.InverseTransformPoint(hand.position)),
                            Is.LessThan(.0005f)
                        );
                        Assert.That(
                            Quaternion.Angle(
                                gripRotation,
                                Quaternion.Inverse(chest.rotation) * hand.rotation
                            ),
                            Is.LessThan(.1f)
                        );
                    }
                    Assert.That(
                        motion,
                        Is.GreaterThan(10),
                        "걷기와 달리기 모두 다리가 실제로 움직여야 한다."
                    );
                    Assert.That(
                        root.transform.position,
                        Is.EqualTo(Vector3.zero),
                        "클립 자체가 NPC를 이동시키면 안 된다."
                    );
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
#endif
