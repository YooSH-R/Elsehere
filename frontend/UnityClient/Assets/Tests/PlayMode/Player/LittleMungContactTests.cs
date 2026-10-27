#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    public sealed class LittleMungContactTests
    {
        private Animator animator;
        private PlayerMover player;
        private float oldDelta;
        private float[] finalHeights;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            player = Object.FindFirstObjectByType<PlayerMover>();
            animator = player.GetComponentInChildren<Animator>();

            player.gameObject.AddComponent<FinalPoseObserver>().Observe = () =>
                finalHeights = SoleHeights(animator);
            for (int i = 0; i < 45; i++)
                yield return null;
            player.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = oldDelta;
        }

        [UnityTest]
        public IEnumerator SpawnDoesNotStartWithShoesBelowTheFloor()
        {
            var holder = new GameObject("BeforeFirstControllerMove");
            holder.SetActive(false);
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/LittleMung/LittleMungPosePlayer.prefab"
            );
            var instance = Object.Instantiate(source, holder.transform);
            instance.transform.position = new Vector3(2, 0, 0);
            var mover = instance.GetComponent<PlayerMover>();
            var setup = new UnityEditor.SerializedObject(mover);
            setup.FindProperty("cameraTransform").objectReferenceValue = Object
                .FindFirstObjectByType<Camera>()
                .transform;
            setup.ApplyModifiedPropertiesWithoutUndo();
            mover.enabled = false;
            float minimum = float.PositiveInfinity;
            instance.AddComponent<FinalPoseObserver>().Observe = () =>
                minimum = Mathf.Min(
                    minimum,
                    SoleHeights(instance.GetComponentInChildren<Animator>()).Min()
                );
            holder.SetActive(true);
            yield return null;
            yield return null;
            Object.Destroy(holder);
            Assert.That(minimum, Is.GreaterThanOrEqualTo(-.004f));
        }

        [UnityTest]
        public IEnumerator IdleSolesMeetTheGround()
        {
            for (int i = 0; i < 30; i++)
                yield return null;
            var heights = finalHeights;
            Debug.Log("Idle soles: " + string.Join(",", heights));
            Assert.That(heights[0], Is.InRange(-0.004f, 0.005f), "Left idle sole");
            Assert.That(heights[1], Is.InRange(-0.004f, 0.005f), "Right idle sole");
        }

        [UnityTest]
        public IEnumerator WalkAndRunSolesDoNotEnterTheGround()
        {
            foreach (float speed in new[] { 0.8f, 1.7f })
            {
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsGrounded", true);
                float lowest = float.PositiveInfinity;
                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                    lowest = Mathf.Min(lowest, finalHeights.Min());
                }
                Debug.Log($"Contact speed={speed}: minimum sole={lowest:F5} m");
                Assert.That(lowest, Is.GreaterThanOrEqualTo(-0.004f), "Animated sole penetration");
            }
        }

        internal static float[] SoleHeights(Animator animator)
        {
            var result = new[] { float.PositiveInfinity, float.PositiveInfinity };
            var baked = new Mesh();
            try
            {
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    var weights = skin.sharedMesh.boneWeights;
                    for (int side = 0; side < 2; side++)
                    {
                        string prefix = side == 0 ? "Left_" : "Right_";
                        var feet = skin
                            .bones.Select(b =>
                                b.name.StartsWith(prefix + "Foot")
                                || b.name.StartsWith(prefix + "Toes")
                            )
                            .ToArray();
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var w = weights[i];
                            float sum =
                                (feet[w.boneIndex0] ? w.weight0 : 0)
                                + (feet[w.boneIndex1] ? w.weight1 : 0)
                                + (feet[w.boneIndex2] ? w.weight2 : 0)
                                + (feet[w.boneIndex3] ? w.weight3 : 0);
                            if (sum >= 0.5f)
                                result[side] = Mathf.Min(
                                    result[side],
                                    skin.transform.TransformPoint(vertices[i]).y
                                );
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
            return result;
        }
    }
}
#endif
