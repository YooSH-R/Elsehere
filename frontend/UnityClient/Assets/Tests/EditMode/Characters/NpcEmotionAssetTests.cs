using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class NpcEmotionAssetTests
    {
        [TestCase("Left")]
        [TestCase("Right")]
        public void BenCheekRaiseKeepsTheSharedSkinBoundaryConnected(string side)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab"
            );
            var meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var body = meshes
                .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                .First()
                .sharedMesh;
            var join = meshes.Single(skin => skin.name == side + "EyeJoin").sharedMesh;
            var bodyVertices = body.vertices;
            var joinVertices = join.vertices;
            var bodyDelta = new Vector3[body.vertexCount];
            var joinDelta = new Vector3[join.vertexCount];
            body.GetBlendShapeFrameVertices(
                body.GetBlendShapeIndex("cheekRaise" + side),
                0,
                bodyDelta,
                null,
                null
            );
            join.GetBlendShapeFrameVertices(
                join.GetBlendShapeIndex("cheekRaise" + side),
                0,
                joinDelta,
                null,
                null
            );
            int shared = 0;
            for (int i = 0; i < joinVertices.Length; i++)
            {
                int match = System.Array.FindIndex(
                    bodyVertices,
                    vertex => (vertex - joinVertices[i]).sqrMagnitude < 1e-12f
                );
                if (match < 0)
                    continue;
                shared++;
                Assert.That(
                    Vector3.Distance(bodyDelta[match], joinDelta[i]),
                    Is.LessThan(.00001f),
                    "애교살을 올려도 눈 주변 연결부와 얼굴의 공유 경계가 벌어지면 안 됩니다."
                );
            }
            Assert.That(shared, Is.GreaterThan(10));
        }

        // 공통 눈 경계 보존은 Ben으로 확인하고, 각 NPC의 표정 모양은 검수 씬에서 확인한다.
        [TestCase("LittleMung_Business_Male")]
        public void EmotionsKeepLatestReviewedEyeBoundary(string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/" + name + ".prefab"
            );
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string part = skin.name;
                if (!part.Contains("Lid") && !part.Contains("Eye"))
                    continue;
                var reviewed = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/Art/Characters/ReviewCorrections/ServiceNpcs/"
                        + name
                        + "/Face/Blink/"
                        + part
                        + ".asset"
                );
                if (reviewed == null)
                    continue;
                Assert.That(
                    skin.sharedMesh.vertices,
                    Is.EqualTo(reviewed.vertices),
                    name + "/" + part
                );
                Assert.That(
                    skin.sharedMesh.normals,
                    Is.EqualTo(reviewed.normals),
                    name + "/" + part
                );
                Assert.That(
                    skin.sharedMesh.triangles,
                    Is.EqualTo(reviewed.triangles),
                    name + "/" + part
                );
            }
        }

        // 검수 프리팹, 호텔 직원, 카페 직원의 서로 다른 제작 경로를 각각 대표로 남긴다.
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab")]
        [TestCase("Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab")]
        [TestCase("Assets/Prefabs/Npc/CafeFemaleDialogue.prefab")]
        public void ServiceFaceSupportsSpeechAndAllAuthoredEmotions(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            Assert.That(CharacterFaceController.MissingChannels(root.transform, true), Is.Empty);
            Assert.That(CharacterFaceController.MissingEmotionChannels(root.transform), Is.Empty);
            var meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(s => s.sharedMesh)
                .ToArray();
            foreach (
                string family in new[]
                {
                    "browInnerUpLeft",
                    "browDownRight",
                    "eyeSquintLeft",
                    "eyeWideRight",
                    "mouthFrownLeft",
                    "mouthLaughOpen",
                }
            )
            {
                Assert.That(meshes.Any(m => HasMovement(m, family)), Is.True, path + "/" + family);
            }
        }

        private static bool HasMovement(Mesh mesh, string channel)
        {
            int index = mesh.GetBlendShapeIndex(channel);
            if (index < 0)
                return false;
            var offsets = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(
                index,
                mesh.GetBlendShapeFrameCount(index) - 1,
                offsets,
                null,
                null
            );
            return offsets.Any(v => v.sqrMagnitude > .000001f);
        }
    }
}
