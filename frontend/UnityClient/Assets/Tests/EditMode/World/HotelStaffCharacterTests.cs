using System.Linq;
using A305.Characters;
using A305.EditorTools.World;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    public sealed class HotelStaffCharacterTests
    {
        private const string HotelierPath =
            "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab";

        [TestCase("Assets/Scenes/Production/HotelScene.unity")]
        [TestCase("Assets/Scenes/Sandbox/HotelInteriorSandbox.unity")]
        public void HotelStaffShowsOnlyTheAnimatedHotelier(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            try
            {
                AssertShowsOnlyHotelier(Staff(scene));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void ReconnectingHotelierKeepsStaffPlacementAndDialogueTarget()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HotelScene.unity");
            try
            {
                NpcIdentity staff = Staff(scene);
                var capsule = staff.GetComponent<CharacterController>();
                Vector3 position = staff.transform.position;
                Quaternion rotation = staff.transform.rotation;
                Vector3 lookTarget = staff.LookTarget.localPosition;
                float height = capsule.height;
                float radius = capsule.radius;

                HotelInteriorBuilder.ConnectHotelier(staff);
                HotelInteriorBuilder.ConnectHotelier(staff);

                AssertShowsOnlyHotelier(staff);
                Assert.That(staff.Code, Is.EqualTo("HOTEL_STAFF"));
                Assert.That(staff.transform.position, Is.EqualTo(position));
                Assert.That(staff.transform.rotation, Is.EqualTo(rotation));
                Assert.That(staff.LookTarget.localPosition, Is.EqualTo(lookTarget));
                Assert.That(capsule.height, Is.EqualTo(height));
                Assert.That(capsule.radius, Is.EqualTo(radius));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static NpcIdentity Staff(Scene scene)
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Single(npc => npc.Code == "HOTEL_STAFF");
        }

        private static void AssertShowsOnlyHotelier(NpcIdentity staff)
        {
            Animator[] animators = staff.GetComponentsInChildren<Animator>();
            Assert.That(
                animators,
                Has.Length.EqualTo(1),
                "Staff must show one animated character."
            );
            Animator hotelier = animators[0];
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hotelier.gameObject),
                Is.EqualTo(HotelierPath)
            );
            Transform character = PrefabUtility
                .GetNearestPrefabInstanceRoot(hotelier.gameObject)
                .transform;
            Assert.That(
                staff
                    .GetComponentsInChildren<Renderer>()
                    .All(renderer => renderer.transform.IsChildOf(character)),
                Is.True,
                "Placeholder shapes must not stay visible."
            );
            Assert.That(
                staff
                    .GetComponentsInChildren<Transform>(true)
                    .Any(item => PrefabUtility.IsPrefabAssetMissing(item.gameObject)),
                Is.False
            );
            Animator female = AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Characters/Female.prefab")
                .GetComponentInChildren<Animator>();
            Assert.That(hotelier.avatar, Is.SameAs(female.avatar));
            var motions = hotelier.runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(
                motions != null
                    ? motions.runtimeAnimatorController
                    : hotelier.runtimeAnimatorController,
                Is.SameAs(female.runtimeAnimatorController),
                "Hotelier must keep the shared locomotion parameters."
            );
            Assert.That(
                new SerializedObject(staff.GetComponent<CharacterAnimationDriver>())
                    .FindProperty("animator")
                    .objectReferenceValue,
                Is.SameAs(hotelier)
            );
        }
    }
}
