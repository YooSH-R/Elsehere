#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    [TestFixture(false)]
    [TestFixture(true)]
    public sealed partial class LittleMungTerrainTests
    {
        private readonly bool corrected;

        public LittleMungTerrainTests(bool corrected) => this.corrected = corrected;

        private PlayerMover player;
        private Animator animator;
        private FinalPoseObserver observer;
        private Vector3[] localUp;
        private readonly HumanBodyBones[] ids =
        {
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };
        private float[] angles;
        private float oldDelta;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                corrected
                    ? "Assets/Scenes/Sandbox/LittleMungTerrainPlayerSandbox.unity"
                    : "Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            player = Object.FindFirstObjectByType<PlayerMover>();
            animator = player.GetComponentInChildren<Animator>();
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx"
                )
            );
            var referenceAnimator = reference.GetComponent<Animator>();
            localUp = new Vector3[2];
            for (int side = 0; side < 2; side++)
                localUp[side] = referenceAnimator
                    .GetBoneTransform(ids[side])
                    .InverseTransformDirection(Vector3.up);
            Object.DestroyImmediate(reference);
            observer = player.gameObject.AddComponent<FinalPoseObserver>();
            for (int i = 0; i < 30; i++)
                yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (observer != null)
                observer.Observe = null;
            Time.captureDeltaTime = oldDelta;
        }

        [UnityTest]
        public IEnumerator IdleSolesFollowTheRampNormal()
        {
            player.Teleport(new Vector3(4, .55f, 3), Quaternion.identity);
            angles = new float[2];
            var ramp = GameObject.Find("Ramp").transform;
            observer.Observe = () =>
            {
                for (int side = 0; side < 2; side++)
                    angles[side] = Vector3.Angle(
                        animator.GetBoneTransform(ids[side]).TransformDirection(localUp[side]),
                        ramp.up
                    );
            };
            for (int i = 0; i < 90; i++)
                yield return null;
            Debug.Log($"Ramp idle corrected={corrected}: sole angles={string.Join(",", angles)}");
            if (corrected)
                foreach (float angle in angles)
                    Assert.That(
                        angle,
                        Is.LessThan(3f),
                        "The planted sole must follow the ramp normal."
                    );
        }
    }
}
#endif
