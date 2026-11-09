#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    public class UmbrellaGripTests
    {
        private GameObject player;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(player);

        [UnityTest]
        public IEnumerator Female_GripIsInThePalmRatherThanTheWrist() => CheckGrip("F");

        [UnityTest]
        public IEnumerator Male_GripIsInThePalmRatherThanTheWrist() => CheckGrip("M");

        private IEnumerator CheckGrip(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/" + id + ".asset"
            );
            player = new GameObject("Umbrella grip test");
            player.SetActive(false);
            player.AddComponent<CharacterController>();
            var visual = Object.Instantiate(definition.VisualPrefab, player.transform);
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            var hold = player.AddComponent<UmbrellaHold>();
            var settings = new SerializedObject(hold);
            settings.FindProperty("umbrellaPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<HeldUmbrella>(
                    "Assets/Prefabs/Player/HeldUmbrella.prefab"
                );
            settings.ApplyModifiedPropertiesWithoutUndo();
            player.SetActive(true);
            yield return null;
            hold.SetOpen(true);
            yield return null;
            yield return null;

            Animator animator = visual.GetComponentInChildren<Animator>();
            Transform wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            float palmLength = Vector3.Distance(wrist.position, middle.position);
            Assert.That(
                Vector3.Distance(hold.Umbrella.transform.position, wrist.position),
                Is.GreaterThan(palmLength * .4f),
                "우산 손잡이는 손목이 아니라 손바닥 안에 있어야 합니다."
            );

            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform pinky = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Vector3 normal = Vector3
                .Cross(index.position - wrist.position, pinky.position - wrist.position)
                .normalized;
            Assert.That(
                Mathf.Abs(Vector3.Dot(normal, hold.Umbrella.transform.up)),
                Is.LessThan(.35f),
                "손바닥이 우산대를 가로로 뚫지 않고 세워 쥐어야 합니다."
            );
            var headNames = animator
                .GetBoneTransform(HumanBodyBones.Head)
                .GetComponentsInChildren<Transform>()
                .Select(bone => bone.name)
                .ToHashSet();
            var shaftPoints = hold.Umbrella.ShaftPoints;
            Assert.That(
                Vector3.Distance(shaftPoints[0], hold.Umbrella.transform.position),
                Is.LessThan(.001f)
            );
            for (int i = 1; i < shaftPoints.Count - 1; i++)
                Assert.That(
                    Vector3.Angle(
                        shaftPoints[i] - shaftPoints[i - 1],
                        shaftPoints[i + 1] - shaftPoints[i]
                    ),
                    Is.LessThan(20),
                    "곡선 기둥에 급하게 꺾이는 부분이 없어야 합니다."
                );
            Vector3 shaftTop = shaftPoints[shaftPoints.Count - 1];
            using var body = new BodyClearanceProbe(animator, player.transform);
            body.Record("umbrella beside ear");
            Vector3 shaftPosition = player.transform.InverseTransformPoint(shaftTop);
            Assert.That(
                shaftPosition.x,
                Is.GreaterThan(body.Head.max.x + .014f),
                "우산대는 얼굴 앞이 아니라 오른쪽 귀·머리 바깥에 있어야 합니다."
            );
            Assert.That(
                Mathf.Abs(shaftPosition.z - body.Head.center.z),
                Is.LessThan(.12f),
                "우산대가 귀 옆에서 얼굴 앞쪽으로 벗어나면 안 됩니다."
            );
            Assert.That(
                Vector3.Dot(hold.Umbrella.Canopy.up, player.transform.up),
                Is.GreaterThan(.999f),
                "캐노피는 머리 위에서 수평을 유지해야 합니다."
            );
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    Vector3[] vertices = mesh.vertices;
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    float clearance = float.PositiveInfinity;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (
                            weights[i].weight0 < .5f
                            || !headNames.Contains(skin.bones[weights[i].boneIndex0].name)
                        )
                            continue;
                        Vector3 point = skin.transform.TransformPoint(vertices[i]);
                        clearance = Mathf.Min(
                            clearance,
                            Vector3.Dot(
                                point - hold.Umbrella.transform.position,
                                -hold.Umbrella.transform.right
                            )
                        );
                        clearance = Mathf.Min(
                            clearance,
                            Vector3.Dot(point - shaftTop, -player.transform.right)
                        );
                    }
                    Assert.That(
                        clearance,
                        Is.GreaterThan(.014f),
                        "반지름 1.4cm인 우산대가 얼굴·머리 안에 들어가면 안 됩니다."
                    );
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
#endif
