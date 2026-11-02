using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>마을 행인 Ben(TOWN_PASSERBY)이 비즈니스맨 외형으로 서고, 다른 행인이 쓰는 공용 프리팹은 그대로인지 확인한다.</summary>
    public sealed class TownPasserbyVisualTests
    {
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string BusinessPath =
            "Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab";
        private const string PasserbyPath = "Assets/Prefabs/Npc/Passerby.prefab";
        private const string ProxyPath =
            "Assets/Prefabs/World/TownProduction/LittleMungV7NpcVisual.prefab";
        private const string GenericRigPath = "Assets/Prefabs/Npc/BenVisual.prefab";
        private const float ServiceHeightMeters = 1.7f;

        [TearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void BusinessVisual_IsServiceHeight()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var visual = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(BusinessPath)
                );

            Bounds body = EvaluatedBodyBounds(visual.transform);

            // 발바닥은 대기 자세에서 루트보다 약간 아래에 있어, 접지는 Ben에 배치할 때 맞춘다.
            TestContext.WriteLine($"height={body.size.y:F4}m sole={body.min.y:F4}m");
            Assert.That(body.size.y, Is.EqualTo(ServiceHeightMeters).Within(.01f));
        }

        [Test]
        public void Ben_WearsTheBusinessManVisual_DrivenByHisOwnAnimator()
        {
            NpcIdentity ben = OpenBen();
            Transform visual = BusinessVisual(ben);

            Assert.That(visual, Is.Not.Null, "Ben 아래에 비즈니스맨 프리팹 인스턴스가 있습니다.");
            Assert.That(
                ben.GetComponentsInChildren<Transform>(true).Where(IsProxyRoot),
                Is.Empty,
                "예전 도형 대체 외형은 남기지 않습니다."
            );
            Animator[] activeAnimators = ben.GetComponentsInChildren<Animator>();
            Assert.That(activeAnimators, Has.Length.EqualTo(1));
            Animator animator = activeAnimators[0];
            Assert.That(animator.transform.IsChildOf(visual), Is.True);
            Assert.That(animator.avatar != null && animator.avatar.isHuman, Is.True);
            Assert.That(
                animator.applyRootMotion,
                Is.False,
                "이동은 CharacterController가 맡습니다."
            );
            var driver = new SerializedObject(ben.GetComponent<CharacterAnimationDriver>());
            Assert.That(driver.FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
        }

        [Test]
        public void Ben_StandsOnTheFloorAtServiceHeight_AndLooksFromHisEyes()
        {
            NpcIdentity ben = OpenBen();
            Transform visual = BusinessVisual(ben);
            Assert.That(visual, Is.Not.Null);

            Bounds body = EvaluatedBodyBounds(visual);
            float eyes = EyeHeight(visual);
            var capsule = ben.GetComponent<CharacterController>();

            TestContext.WriteLine(
                $"height={body.size.y:F4}m floorOffset={body.min.y - ben.transform.position.y:F4}m eyes={eyes - ben.transform.position.y:F4}m capsule={capsule.height:F3}m"
            );
            Assert.That(body.size.y, Is.EqualTo(ServiceHeightMeters).Within(.01f));
            Assert.That(body.min.y, Is.EqualTo(ben.transform.position.y).Within(.01f));
            Assert.That(capsule.height, Is.EqualTo(ServiceHeightMeters).Within(.001f));
            Assert.That(capsule.center.y, Is.EqualTo(ServiceHeightMeters / 2).Within(.001f));
            Assert.That(ben.LookTarget.position.y, Is.EqualTo(eyes).Within(.01f));
        }

        [Test]
        public void Ben_CarriesNoPlayerInputOrCameraComponents()
        {
            NpcIdentity ben = OpenBen();

            string[] playerOnly = ben.GetComponentsInChildren<Component>(true)
                .Where(component =>
                    component is PlayerMover
                    || component is PlayerCameraRig
                    || component is PlayerSeatPose
                    || component is PlayerCharacterAppearance
                    || component is Camera
                    || component is AudioListener
                    || component.GetType().Name == "PlayerInput"
                )
                .Select(component => component.GetType().Name + " on " + component.name)
                .ToArray();

            Assert.That(playerOnly, Is.Empty);
        }

        [Test]
        public void Ben_HasEveryFaceChannel_SoDialogueDrivesHisFace()
        {
            NpcIdentity ben = OpenBen();
            Transform visual = BusinessVisual(ben);
            Assert.That(visual, Is.Not.Null);

            Assert.That(
                CharacterFaceController.MissingChannels(visual),
                Is.Empty,
                "입 모양 9개, 좌우 미소, 좌우 눈 감기가 모두 있습니다."
            );
            // 실행 중에는 NpcIdentity.Awake가 붙이는 화자를 같은 방식으로 붙여 확인한다.
            var speaker = ben.gameObject.AddComponent<DialogueSpeaker>();
            Assert.That(speaker.Player, Is.Not.Null, speaker.BindingNotice);
            Assert.That(speaker.Player.transform.IsChildOf(visual), Is.True);
        }

        [Test]
        public void SharedPasserbyPrefab_KeepsItsOwnPlaceholderForOtherPassersby()
        {
            GameObject passerby = AssetDatabase.LoadAssetAtPath<GameObject>(PasserbyPath);

            Assert.That(passerby.transform.Find("Visual"), Is.Not.Null);
            Assert.That(
                passerby
                    .GetComponentsInChildren<Transform>(true)
                    .Where(child => PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)),
                Is.Empty,
                "공용 행인 프리팹에는 특정 인물의 외형을 넣지 않습니다."
            );
        }

        private static NpcIdentity OpenBen()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Single(npc => npc.Code == "TOWN_PASSERBY");
        }

        private static Transform BusinessVisual(NpcIdentity ben)
        {
            return ben.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                        == BusinessPath
                );
        }

        private static bool IsProxyRoot(Transform child)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                return false;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
            return path == ProxyPath || path == GenericRigPath;
        }

        // 대기 동작의 첫 자세를 적용한 몸 메시의 월드 경계. Renderer 배율을 한 번만 반영한다.
        private static Bounds EvaluatedBodyBounds(Transform visual)
        {
            Animator animator = visual.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(0);
            SkinnedMeshRenderer body = visual
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                .First();
            var baked = new Mesh();
            try
            {
                body.BakeMesh(baked, true);
                Vector3[] points = baked
                    .vertices.Select(vertex => body.transform.TransformPoint(vertex))
                    .ToArray();
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (Vector3 point in points)
                    bounds.Encapsulate(point);
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        // 공용 Avatar는 눈을 Humanoid 뼈로 매핑하지 않으므로 골격의 눈 뼈 이름으로 찾는다.
        private static float EyeHeight(Transform visual)
        {
            Transform[] bones = visual.GetComponentsInChildren<Transform>(true);
            Transform left = bones.Single(bone => bone.name == "Left_Eye");
            Transform right = bones.Single(bone => bone.name == "Right_Eye");
            return (left.position.y + right.position.y) / 2;
        }
    }
}
