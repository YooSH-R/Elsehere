using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 인격 카드가 있는 NPC가 정해진 리틀 멍 외형을 쓰는지 확인한다. 서서 지내는 Ben·Alex는 공용 행인 프리팹 아래에 외형을 두고,
    /// 택시 기사 Joe는 운전석에 앉은 외형이다. 다른 행인이 쓰는 공용 프리팹은 그대로다.
    /// </summary>
    public sealed class NpcVisualTests
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const string ShopPath = "Assets/Scenes/Production/ShopScene.unity";
        private const string BusinessPath =
            "Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab";
        private const string BlackYoungPath =
            "Assets/Prefabs/Npc/Review/LittleMung_BlackYoung_Male.prefab";
        private const string MiddleAgePath =
            "Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Male.prefab";
        private const string PasserbyPath = "Assets/Prefabs/Npc/Passerby.prefab";
        private const string ProxyPath =
            "Assets/Prefabs/World/TownProduction/LittleMungV7NpcVisual.prefab";
        private const string GenericRigPath = "Assets/Prefabs/Npc/BenVisual.prefab";
        private const float ServiceHeightMeters = 1.7f;

        // 서서 지내는 NPC: (장면, NPC 키, 외형 프리팹)
        private static readonly object[] StandingNpcs =
        {
            new object[] { TownPath, "TOWN_PASSERBY", BusinessPath },
            new object[] { ShopPath, "SHOP_STAFF", BlackYoungPath },
        };

        [TearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase(BusinessPath)]
        [TestCase(BlackYoungPath)]
        [TestCase(MiddleAgePath)]
        public void Visual_IsServiceHeight(string prefabPath)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var visual = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                );

            Bounds body = EvaluatedBodyBounds(visual.transform);

            // 발바닥은 대기 자세에서 루트보다 약간 아래에 있어, 접지는 NPC에 배치할 때 맞춘다.
            TestContext.WriteLine($"height={body.size.y:F4}m sole={body.min.y:F4}m");
            Assert.That(body.size.y, Is.EqualTo(ServiceHeightMeters).Within(.01f));
        }

        [TestCaseSource(nameof(StandingNpcs))]
        public void Npc_WearsItsVisual_DrivenByItsOwnAnimator(
            string scenePath,
            string code,
            string prefabPath
        )
        {
            NpcIdentity npc = Open(scenePath, code);
            Transform visual = Visual(npc, prefabPath);

            Assert.That(visual, Is.Not.Null, "NPC 아래에 정해진 외형 프리팹 인스턴스가 있습니다.");
            Assert.That(
                npc.GetComponentsInChildren<Transform>(true).Where(IsProxyRoot),
                Is.Empty,
                "예전 도형 대체 외형은 남기지 않습니다."
            );
            Animator[] activeAnimators = npc.GetComponentsInChildren<Animator>();
            Assert.That(activeAnimators, Has.Length.EqualTo(1));
            Animator animator = activeAnimators[0];
            Assert.That(animator.transform.IsChildOf(visual), Is.True);
            Assert.That(animator.avatar != null && animator.avatar.isHuman, Is.True);
            Assert.That(
                animator.applyRootMotion,
                Is.False,
                "이동은 CharacterController가 맡습니다."
            );
            var driver = new SerializedObject(npc.GetComponent<CharacterAnimationDriver>());
            Assert.That(driver.FindProperty("animator").objectReferenceValue, Is.SameAs(animator));
        }

        [TestCaseSource(nameof(StandingNpcs))]
        public void Npc_StandsOnTheFloor_InAFittingCapsule_AndLooksFromItsEyes(
            string scenePath,
            string code,
            string prefabPath
        )
        {
            NpcIdentity npc = Open(scenePath, code);
            Transform visual = Visual(npc, prefabPath);
            Assert.That(visual, Is.Not.Null);

            Bounds body = EvaluatedBodyBounds(visual);
            float eyes = EyeHeight(visual);
            var capsule = npc.GetComponent<CharacterController>();
            float floor = npc.transform.position.y;

            TestContext.WriteLine(
                $"height={body.size.y:F4}m floorOffset={body.min.y - floor:F4}m eyes={eyes - floor:F4}m capsule={capsule.height:F3}m"
            );
            Assert.That(body.size.y, Is.EqualTo(ServiceHeightMeters).Within(.01f));
            Assert.That(body.min.y, Is.EqualTo(floor).Within(.01f));
            Assert.That(capsule.height, Is.EqualTo(body.size.y).Within(.01f));
            Assert.That(capsule.center.y, Is.EqualTo(capsule.height / 2).Within(.001f));
            Assert.That(npc.LookTarget.position.y, Is.EqualTo(eyes).Within(.01f));
        }

        [TestCaseSource(nameof(StandingNpcs))]
        public void Npc_CarriesNoPlayerInputOrCameraComponents(
            string scenePath,
            string code,
            string prefabPath
        )
        {
            NpcIdentity npc = Open(scenePath, code);

            Assert.That(PlayerOnlyComponents(npc.transform), Is.Empty);
        }

        [TestCaseSource(nameof(StandingNpcs))]
        public void Npc_HasEveryFaceChannel_SoDialogueDrivesItsFace(
            string scenePath,
            string code,
            string prefabPath
        )
        {
            NpcIdentity npc = Open(scenePath, code);
            Transform visual = Visual(npc, prefabPath);
            Assert.That(visual, Is.Not.Null);

            AssertDialogueDrivesFace(npc, visual);
        }

        [Test]
        public void TaxiDriver_HasEveryFaceChannel_SoDialogueDrivesHisFace()
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            TaxiRide taxi = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TaxiRide>(true))
                .Single();
            var joe = (NpcIdentity)
                new SerializedObject(taxi).FindProperty("driver").objectReferenceValue;

            AssertDialogueDrivesFace(joe, joe.transform);
        }

        // 입 모양 9개, 좌우 미소, 좌우 눈 감기가 모두 있으면 대화가 그 NPC의 얼굴로 립싱크·깜박임을 적용한다.
        // 실행 중에는 NpcIdentity.Awake가 붙이는 화자를 같은 방식으로 붙여 확인한다.
        private static void AssertDialogueDrivesFace(NpcIdentity npc, Transform visual)
        {
            Assert.That(CharacterFaceController.MissingChannels(visual), Is.Empty);
            var speaker = npc.gameObject.AddComponent<DialogueSpeaker>();
            Assert.That(speaker.Player, Is.Not.Null, speaker.BindingNotice);
            Assert.That(speaker.Player.transform.IsChildOf(visual), Is.True);
            Assert.That(speaker.Player.Face, Is.Not.Null);
        }

        [Test]
        public void TaxiDriver_IsTheMiddleAgedMan_SeatedAtTheWheel()
        {
            Scene scene = EditorSceneManager.OpenScene(TownPath);
            TaxiRide taxi = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TaxiRide>(true))
                .Single();
            var ride = new SerializedObject(taxi);
            var joe = (NpcIdentity)ride.FindProperty("driver").objectReferenceValue;

            Assert.That(joe, Is.Not.Null);
            Assert.That(joe.Code, Is.EqualTo("TAXI_DRIVER"));
            Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(joe.gameObject), Is.True);
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(joe.gameObject),
                Is.EqualTo(MiddleAgePath)
            );
            Assert.That(
                taxi.GetComponentsInChildren<NpcIdentity>(true),
                Has.Length.EqualTo(1),
                "택시에는 기사 한 명만 탑니다."
            );
            Assert.That(
                joe.GetComponent<HoverableObject>(),
                Is.Not.Null,
                "사물 단어(taxi driver)를 유지합니다."
            );
            Assert.That(
                joe.GetComponentsInChildren<Behaviour>(true)
                    .Where(behaviour => behaviour.GetType().Name.StartsWith("LittleMung")),
                Is.Empty,
                "운전석에서는 서 있는 자세 보정을 쓰지 않습니다."
            );
            Assert.That(PlayerOnlyComponents(joe.transform), Is.Empty);

            // 저장된 운전 자세: 골반은 좌석, 두 손은 운전대 손잡이, 시선은 머리.
            Animator animator = joe.GetComponentInChildren<Animator>(true);
            Transform car = taxi.transform;
            Vector3 hips = car.InverseTransformPoint(
                animator.GetBoneTransform(HumanBodyBones.Hips).position
            );
            Vector3 left = car.InverseTransformPoint(
                animator.GetBoneTransform(HumanBodyBones.LeftHand).position
            );
            Vector3 right = car.InverseTransformPoint(
                animator.GetBoneTransform(HumanBodyBones.RightHand).position
            );
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            TestContext.WriteLine(
                $"hips={hips:F3} left={left:F3} right={right:F3} head={car.InverseTransformPoint(head.position):F3}"
            );
            Assert.That(Vector3.Distance(hips, new Vector3(-.38f, .75f, .01f)), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(left, new Vector3(-.46f, .83f, .13f)), Is.LessThan(.03f));
            Assert.That(Vector3.Distance(right, new Vector3(-.30f, .83f, .13f)), Is.LessThan(.03f));
            Assert.That(
                Vector3.Distance(joe.LookTarget.position, head.position),
                Is.LessThan(.01f)
            );

            // 큰 머리가 지붕 안감을 뚫지 않는다.
            float roof = car.InverseTransformPoint(
                car.GetComponentsInChildren<Renderer>(true)
                    .Single(renderer => renderer.name == "RoofLiner")
                    .bounds.min
            ).y;
            float headTop = joe.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Max(skin => HighestPoint(skin, car));
            TestContext.WriteLine($"headTop={headTop:F3} roofLiner={roof:F3}");
            Assert.That(headTop, Is.LessThan(roof), "머리가 지붕 안감에 닿지 않습니다.");
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

        private static NpcIdentity Open(string scenePath, string code)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>(true))
                .Single(npc => npc.Code == code);
        }

        private static Transform Visual(NpcIdentity npc, string prefabPath)
        {
            return npc.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)
                        == prefabPath
                );
        }

        private static bool IsProxyRoot(Transform child)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                return false;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
            return path == ProxyPath || path == GenericRigPath;
        }

        private static string[] PlayerOnlyComponents(Transform root)
        {
            return root.GetComponentsInChildren<Component>(true)
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

        // 저장된 자세의 스킨 메시에서 기준 물체(차) 좌표로 가장 높은 점
        private static float HighestPoint(SkinnedMeshRenderer skin, Transform space)
        {
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                return baked.vertices.Max(vertex =>
                    space.InverseTransformPoint(skin.transform.TransformPoint(vertex)).y
                );
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
