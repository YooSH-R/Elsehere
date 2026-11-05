using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class NpcEmotionAssetTests
    {
        [TestCase("LittleMung_MiddleAge_Male")]
        [TestCase("LittleMung_MiddleAge_Female")]
        [TestCase("LittleMung_BlackYoung_Male")]
        [TestCase("LittleMung_Grandmother")]
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

        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Male.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Female.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_BlackYoung_Male.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Grandmother.prefab")]
        [TestCase("Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab")]
        [TestCase("Assets/Prefabs/Npc/CafeFemaleDialogue.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Cafe_Male.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Grandfather.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_HotelStaff_Female.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Police_Female.prefab")]
        [TestCase("Assets/Prefabs/Npc/Review/LittleMung_Police_Male.prefab")]
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
