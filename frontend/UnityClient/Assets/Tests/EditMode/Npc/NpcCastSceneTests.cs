using System.Collections.Generic;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.Npc.Tests
{
    public class NpcCastSceneTests
    {
        [Test]
        public void RecordedPerformerKeepsItsDedicatedRigGripAndMaterials()
        {
            var member = NpcCast.Find("TOWN_JOHN_PARK");
            Assert.That(member.Look.UsesRecordedPerformance, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(member.Look.PrefabPath);
            var copy = Object.Instantiate(prefab);
            try
            {
                var animator = copy.GetComponentInChildren<Animator>();
                var controller = animator.runtimeAnimatorController;
                var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
                var materials = skins.SelectMany(skin => skin.sharedMaterials).ToArray();
                var body = skins.OrderByDescending(skin => skin.sharedMesh.vertexCount).First();
                int hand = System.Array.FindIndex(body.bones, bone => bone.name == "Right_Hand");
                Assert.That(hand, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    body.sharedMesh.boneWeights.Count(weight =>
                        weight.boneIndex0 == hand && weight.weight0 > .99999f
                    ),
                    Is.GreaterThan(1000),
                    "오른손과 앰프를 고정하는 결합을 유지합니다."
                );
                NpcCastDresser.Dress(copy, member);
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
                Assert.That(animator.avatar.isValid && !animator.avatar.isHuman, Is.True);
                Assert.That(skins.SelectMany(skin => skin.sharedMaterials), Is.EqualTo(materials));
                Assert.That(copy.GetComponent<NpcTownWalker>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void CommutersUseTheirCastVisualAndKeepOneDialogueFace()
        {
            var failures = new List<string>();
            foreach (
                var member in NpcCast.Members.Where(member =>
                    member.Code != "TAXI_DRIVER" && !member.Look.UsesRecordedPerformance
                )
            )
            {
                string path = "Assets/Prefabs/Npc/Commuters/" + member.Code + ".prefab";
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, path);
                Assert.That(root.activeSelf, Is.False, member.Code);
                Assert.That(root.GetComponent<NpcTownWalker>(), Is.Not.Null, member.Code);
                var animators = root.GetComponentsInChildren<Animator>(true);
                var faces = root.GetComponentsInChildren<CharacterFaceController>(true);
                Assert.That(animators, Has.Length.EqualTo(1), member.Code);
                Assert.That(faces, Has.Length.EqualTo(1), member.Code);
                Transform visual = animators[0].transform;
                while (visual.parent != null && visual.parent != root.transform)
                    visual = visual.parent;
                string actual = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual);
                if (actual != member.Look.PrefabPath)
                    failures.Add($"{member.Code}: {actual} → {member.Look.PrefabPath}");
                Assert.That(
                    CharacterFaceController.MissingChannels(faces[0].transform, true),
                    Is.Empty,
                    member.Code
                );
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [TestCase("TownScene")]
        [TestCase("CafeScene")]
        [TestCase("HotelScene")]
        [TestCase("ShopScene")]
        [TestCase("PoliceStationScene")]
        public void SceneUsesCastVisualAndOnlyItsDialogueFace(string sceneName)
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/" + sceneName + ".unity"
            );
            var failures = new List<string>();
            var npcs = BuilderTools.FindAllInScene<NpcIdentity>(scene).ToArray();
            Assert.That(npcs, Is.Not.Empty);
            foreach (var npc in npcs)
            {
                var member = NpcCast.Find(npc.Code);
                Assert.That(member, Is.Not.Null, npc.Code);
                var animators = npc.GetComponentsInChildren<Animator>();
                var faces = npc.GetComponentsInChildren<CharacterFaceController>();
                if (animators.Length != 1 || faces.Length != 1)
                {
                    failures.Add($"{npc.Code}: Animator {animators.Length}, 얼굴 {faces.Length}");
                    continue;
                }
                Transform visual = animators[0].transform;
                while (visual.parent != null && visual.parent != npc.transform)
                    visual = visual.parent;
                string actual = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual);
                if (member.Look.UsesRecordedPerformance)
                    actual = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npc.gameObject);
                if (actual != member.Look.PrefabPath)
                    failures.Add($"{npc.Code}: {actual} → {member.Look.PrefabPath}");
                if (CharacterFaceController.MissingChannels(faces[0].transform, true).Length != 0)
                    failures.Add(npc.Code + ": 공통 표정 채널 누락");
                // NpcIdentity.Awake가 Play 때 추가하는 연결을 EditMode에서도 준비한다.
                var speaker = BuilderTools.GetOrAddComponent<DialogueSpeaker>(npc.gameObject);
                if (speaker.Player == null || speaker.Player.Face != faces[0])
                    failures.Add(npc.Code + ": 대화 얼굴 연결 불일치");
                var driver = npc.GetComponent<CharacterAnimationDriver>();
                // 운전석의 Joe는 Animator를 끈 채 고정 자세로 앉는다.
                if (npc.Code == "TAXI_DRIVER")
                    Assert.That(animators[0].enabled, Is.False);
                else if (member.Look.UsesRecordedPerformance)
                {
                    var performer = npc.GetComponent<RecordedNpcPerformance>();
                    Assert.That(performer, Is.Not.Null, npc.Code);
                    Assert.That(performer.IsReady, Is.True, npc.Code);
                    Assert.That(npc.GetComponent<NpcTownWalker>(), Is.Null, npc.Code);
                    Assert.That(animators[0].avatar.isHuman, Is.False, npc.Code);
                    Assert.That(
                        new SerializedObject(performer)
                            .FindProperty("animator")
                            .objectReferenceValue,
                        Is.EqualTo(animators[0]),
                        npc.Code
                    );
                }
                else if (
                    driver == null
                    || new SerializedObject(driver).FindProperty("animator").objectReferenceValue
                        != animators[0]
                )
                    failures.Add(npc.Code + ": 몸 애니메이터 연결 불일치");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
