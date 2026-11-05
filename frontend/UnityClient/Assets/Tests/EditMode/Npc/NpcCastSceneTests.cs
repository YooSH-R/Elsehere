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
        public void CommutersUseTheirCastVisualAndKeepOneDialogueFace()
        {
            var failures = new List<string>();
            foreach (var member in NpcCast.Members.Where(member => member.Code != "TAXI_DRIVER"))
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
