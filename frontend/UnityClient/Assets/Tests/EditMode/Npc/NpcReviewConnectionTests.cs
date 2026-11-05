using System.Linq;
using A305.Characters;
using A305.EditorTools.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Npc.Tests
{
    public sealed class NpcReviewConnectionTests
    {
        private const string Review = "Assets/Prefabs/Npc/Review/";

        [Test]
        public void NewCafeVisualUsesPlacementScaleWithoutChangingExistingScale()
        {
            var root = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(NpcBuilder.PasserbyPrefabPath)
                );
            try
            {
                var npc = root.GetComponent<NpcIdentity>();
                string path = "Assets/Prefabs/Npc/CafeFemaleDialogue.prefab";
                NpcVisualBuilder.Connect(npc, path, true, 1.6f);
                var visual = root
                    .transform.Cast<Transform>()
                    .Single(child =>
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child) == path
                    );
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 1.6f));
                Assert.That(root.GetComponent<CharacterController>().height, Is.GreaterThan(1.5f));
                visual.localScale = Vector3.one * 1.7f;
                NpcVisualBuilder.Connect(npc, path, true, 1.6f);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 1.7f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReconnectingScaledVisualMeasuresItsExistingSceneScale()
        {
            var root = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(NpcBuilder.PasserbyPrefabPath)
                );
            try
            {
                var npc = root.GetComponent<NpcIdentity>();
                string path = Review + "LittleMung_Cafe_Male.prefab";
                NpcVisualBuilder.Connect(npc, path, true);
                var visual = root
                    .transform.Cast<Transform>()
                    .Single(child =>
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child) == path
                    );
                float height = root.GetComponent<CharacterController>().height;
                float eyeHeight = npc.LookTarget.localPosition.y - visual.localPosition.y;
                visual.localScale *= 1.6f;
                Vector3 scale = visual.localScale;
                NpcVisualBuilder.Connect(npc, path, true);
                Assert.That(visual.localScale, Is.EqualTo(scale), "기존 외형 크기를 유지합니다.");
                Assert.That(
                    root.GetComponent<CharacterController>().height,
                    Is.EqualTo(height * 1.6f).Within(.001f)
                );
                Assert.That(
                    npc.LookTarget.localPosition.y - visual.localPosition.y,
                    Is.EqualTo(eyeHeight * 1.6f).Within(.001f)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReplacingVisualKeepsOneFaceAndPreservesNpcPlacementAndSettings()
        {
            var npc = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(NpcBuilder.PasserbyPrefabPath)
                );
            try
            {
                var identity = npc.GetComponent<NpcIdentity>();
                npc.transform.SetPositionAndRotation(
                    new Vector3(12, 2, 8),
                    Quaternion.Euler(0, 35, 0)
                );
                var position = npc.transform.position;
                var rotation = npc.transform.rotation;
                string code = identity.Code;
                var settings = new SerializedObject(npc.GetComponent<Passerby>())
                    .FindProperty("settings")
                    .objectReferenceValue;
                var accessory = new GameObject("동작과 무관한 소지품");
                accessory.transform.SetParent(npc.transform, false);
                NpcVisualBuilder.Connect(
                    identity,
                    Review + "LittleMung_Business_Male.prefab",
                    false
                );
                NpcVisualBuilder.Connect(identity, Review + "LittleMung_Cafe_Male.prefab", true);
                NpcVisualBuilder.Connect(identity, Review + "LittleMung_Cafe_Male.prefab", true);
                var faces = npc.GetComponentsInChildren<CharacterFaceController>();
                Assert.That(
                    faces,
                    Has.Length.EqualTo(1),
                    "예전 얼굴이 남으면 대화 연결이 다중 얼굴로 거부됩니다."
                );
                Assert.That(npc.GetComponentsInChildren<Animator>(), Has.Length.EqualTo(1));
                Assert.That(
                    CharacterFaceController.MissingChannels(faces[0].transform, true),
                    Is.Empty
                );
                var animator = new SerializedObject(npc.GetComponent<CharacterAnimationDriver>())
                    .FindProperty("animator")
                    .objectReferenceValue;
                Assert.That(animator, Is.SameAs(npc.GetComponentInChildren<Animator>()));
                Assert.That(npc.transform.position, Is.EqualTo(position));
                Assert.That(npc.transform.rotation, Is.EqualTo(rotation));
                Assert.That(identity.Code, Is.EqualTo(code));
                Assert.That(
                    new SerializedObject(npc.GetComponent<Passerby>())
                        .FindProperty("settings")
                        .objectReferenceValue,
                    Is.SameAs(settings)
                );
                Assert.That(accessory != null, Is.True);
                Assert.That(npc.GetComponent<CharacterController>().height, Is.GreaterThan(1.5f));
                var pose = npc.GetComponentInChildren<Animator>();
                pose.Rebind();
                pose.Update(0);
                var eyeMesh = new Mesh();
                try
                {
                    var eyes = npc.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(s =>
                            s.sharedMesh.name == "LeftEye" || s.sharedMesh.name == "RightEye"
                        )
                        .ToArray();
                    Assert.That(eyes, Has.Length.EqualTo(2));
                    float eyeHeight = 0;
                    foreach (var eye in eyes)
                    {
                        eye.BakeMesh(eyeMesh, true);
                        var heights = eyeMesh
                            .vertices.Select(v =>
                                npc.transform.InverseTransformPoint(
                                    eye.transform.TransformPoint(v)
                                ).y
                            )
                            .ToArray();
                        eyeHeight += (heights.Min() + heights.Max()) * .25f;
                    }
                    Assert.That(
                        identity.LookTarget.localPosition.y,
                        Is.EqualTo(eyeHeight).Within(.001f),
                        "시선 높이는 고정된 키가 아니라 보정한 눈 메시와 일치해야 합니다."
                    );
                }
                finally
                {
                    Object.DestroyImmediate(eyeMesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(npc);
            }
        }

        [TestCase("CAFE_SERVER", "LittleMung_Cafe_Male")]
        [TestCase("HOTEL_NIGHT_MANAGER", "LittleMung_HotelStaff_Female")]
        [TestCase("POLICE_OFFICER", "LittleMung_Police_Male")]
        [TestCase("TOWN_GRANDFATHER", "LittleMung_Grandfather")]
        public void ExistingRolesUseTheirReviewedCharacter(string code, string name)
        {
            Assert.That(NpcCast.Find(code).Look.PrefabPath, Is.EqualTo(Review + name + ".prefab"));
        }
    }
}
