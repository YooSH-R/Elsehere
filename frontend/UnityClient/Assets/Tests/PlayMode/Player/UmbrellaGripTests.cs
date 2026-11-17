#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator Female_StraightUmbrellaTiltsBackWithTheHandInFrontAndTheFaceClear() =>
            CheckGrip("F");

        [UnityTest]
        public IEnumerator Male_StraightUmbrellaTiltsBackWithTheHandInFrontAndTheFaceClear() =>
            CheckGrip("M");

        private IEnumerator CheckGrip(string id)
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(
                "Assets/Data/Player/Characters/" + id + ".asset"
            );
            player = new GameObject("Umbrella grip test");
            player.SetActive(false);
            player.AddComponent<CharacterController>();
            var visual = Object.Instantiate(definition.VisualPrefab, player.transform);
            // 지면 보정은 격리하되, 양팔이 몸에 묻히지 않게 하는 실제 플레이 보정은 유지한다.
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = behaviour is LittleMungArmClearance;
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
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.That(
                player.transform.InverseTransformPoint(wrist.position).z,
                Is.GreaterThan(player.transform.InverseTransformPoint(shoulder.position).z),
                "우산을 쥔 손은 어깨 앞에 있어야 하며, 팔을 등 뒤로 비틀면 안 됩니다."
            );
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
            Vector3 shaftTop = shaftPoints[shaftPoints.Count - 1];
            Vector3 shaftDirection = (shaftTop - shaftPoints[0]).normalized;
            Assert.That(
                Vector3.Dot(shaftDirection, player.transform.forward),
                Is.LessThan(-.3f),
                "우산 전체가 캐릭터 뒤로 기울어져야 합니다."
            );
            Transform shaft = hold.Umbrella.transform.Find("Shaft");
            Assert.That(shaft.gameObject.activeInHierarchy, Is.True);
            foreach (Vector3 vertex in shaft.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 offset = shaft.TransformPoint(vertex) - shaftPoints[0];
                Assert.That(
                    Vector3.ProjectOnPlane(offset, shaftDirection).magnitude,
                    Is.LessThanOrEqualTo(.0141f),
                    "우산대 전체가 손잡이와 꼭지를 잇는 직선 위에 있어야 합니다."
                );
            }
            using var body = new BodyClearanceProbe(animator, player.transform);
            body.Record("umbrella tilted behind shoulder");
            Assert.That(
                Vector3.Dot(hold.Umbrella.Canopy.up, shaftDirection),
                Is.GreaterThan(.999f),
                "캐노피도 우산대와 함께 기울어져야 합니다."
            );
            float canopyClearance = float.PositiveInfinity;
            int faceTriangles = 0;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    var faceVertices = new bool[vertices.Length];
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (
                            weights[i].weight0 < .5f
                            || !headNames.Contains(skin.bones[weights[i].boneIndex0].name)
                        )
                            continue;
                        Vector3 point = skin.transform.TransformPoint(vertices[i]);
                        canopyClearance = Mathf.Min(
                            canopyClearance,
                            Vector3.Dot(hold.Umbrella.Canopy.position - point, shaftDirection)
                        );
                        // 뒷머리 안쪽 겹침은 허용한다. 머리 앞쪽에서 정면을 향하는 표면만 얼굴로 검사한다.
                        Vector3 localPoint = player.transform.InverseTransformPoint(point);
                        Vector3 surfaceNormal = skin.transform.TransformDirection(normals[i]);
                        if (
                            localPoint.z < body.Head.center.z
                            || Vector3.Dot(surfaceNormal, player.transform.forward) < .5f
                        )
                            continue;
                        faceVertices[i] = true;
                    }
                    faceTriangles += AssertShaftDoesNotTouchFace(
                        skin,
                        mesh,
                        faceVertices,
                        shaftPoints[0],
                        shaftTop
                    );
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            Assert.That(faceTriangles, Is.GreaterThan(0), "실제 얼굴 앞면을 검사해야 합니다.");
            Assert.That(
                canopyClearance,
                Is.GreaterThan(.02f),
                "우산 천은 머리와 겹치면 안 됩니다."
            );
        }

        private static int AssertShaftDoesNotTouchFace(
            SkinnedMeshRenderer skin,
            Mesh baked,
            bool[] faceVertices,
            Vector3 start,
            Vector3 end
        )
        {
            var faceTriangles = new List<int>();
            int[] triangles = baked.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i],
                    b = triangles[i + 1],
                    c = triangles[i + 2];
                if (faceVertices[a] && faceVertices[b] && faceVertices[c])
                {
                    faceTriangles.Add(a);
                    faceTriangles.Add(b);
                    faceTriangles.Add(c);
                }
            }
            if (faceTriangles.Count == 0)
                return 0;

            var face = new Mesh
            {
                indexFormat = baked.indexFormat,
                vertices = baked.vertices,
                triangles = faceTriangles.ToArray(),
            };
            var probe = new GameObject("Face collision probe");
            try
            {
                probe.transform.SetParent(skin.transform, false);
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = face;
                Physics.SyncTransforms();
                Vector3 direction = (end - start).normalized;
                // 머리 옆의 정점까지 무한 평면으로 막지 않고, 실제 얼굴 표면과 우산대 굵기로 검사한다.
                bool touchesFace = Physics
                    .SphereCastAll(start, .014f, direction, Vector3.Distance(start, end))
                    .Any(hit => hit.collider == collider);
                Assert.That(
                    touchesFace,
                    Is.False,
                    "우산대가 얼굴 앞면을 뚫으면 안 됩니다: " + skin.name
                );
            }
            finally
            {
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(face);
            }
            return faceTriangles.Count / 3;
        }
    }
}
#endif
