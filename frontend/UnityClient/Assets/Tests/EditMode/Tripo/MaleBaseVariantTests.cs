using System;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class MaleBaseVariantTests
    {
        private const string BasePath =
            "Assets/Art/Characters/Common/LittleMungMannequin/LittleMung_Base_Male_Visual.prefab";
        private const string VariantPath =
            "Assets/Prefabs/Characters/Mannequin/Npc/LittleMung_Npc_Male.prefab";

        private static GameObject Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        [Test]
        public void NpcVariantInheritsMasterMeshesAvatarAndController()
        {
            var parent = Load(BasePath);
            var child = Load(VariantPath);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(child),
                Is.EqualTo(PrefabAssetType.Variant)
            );
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(child), Is.SameAs(parent));
            CollectionAssert.AreEqual(
                parent.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s => s.sharedMesh),
                child.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s => s.sharedMesh)
            );
            var a = parent.GetComponentInChildren<Animator>();
            var b = child.GetComponentInChildren<Animator>();
            Assert.That(b.avatar, Is.SameAs(a.avatar));
            Assert.That(b.runtimeAnimatorController, Is.SameAs(a.runtimeAnimatorController));
            Assert.That(b.avatar.isValid && b.avatar.isHuman, Is.True);
            Assert.That(TripoMasterMotionAudit.Inspect(child).Errors, Is.Empty);
        }

        [Test]
        public void VariantKeepsAllExpressionsAndSpeechIndependentFromBase()
        {
            var parent = Object.Instantiate(Load(BasePath));
            var child = Object.Instantiate(Load(VariantPath));
            try
            {
                var baseFace = parent.GetComponent<CharacterFaceController>();
                var npcFace = child.GetComponent<CharacterFaceController>();
                baseFace.SetPose(CharacterFacePose.Neutral);
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    npcFace.SetPose(pose);
                    foreach (var skin in parent.GetComponentsInChildren<SkinnedMeshRenderer>())
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                            Assert.That(skin.GetBlendShapeWeight(i), Is.Zero);
                }
                var timeline = SpeechVisemeTimeline.Parse(
                    JsonUtility.ToJson(
                        new SpeechVisemeData
                        {
                            version = 2,
                            provider = "windows-sapi-en-US",
                            duration = 1,
                            phonemes = new[]
                            {
                                new SpeechPhonemeCue
                                {
                                    start = 0,
                                    end = 1,
                                    phoneme = "aa",
                                },
                            },
                        }
                    ),
                    1
                );
                npcFace.ApplySpeech(timeline.SampleArticulation(.2f), .3f, .7f);
                var lid = child
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .First(s => s.sharedMesh.name == "UpperLeftLid");
                Assert.That(
                    lid.GetBlendShapeWeight(lid.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.GreaterThan(0)
                );
                var lips = child
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .First(s => s.sharedMesh.name == "SpeakingLips");
                Assert.That(
                    lips.GetBlendShapeWeight(lips.sharedMesh.GetBlendShapeIndex("viseme_AA")),
                    Is.GreaterThan(10)
                );
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void ParentChangesPropagateWhileTheChildSkinOverrideSurvives()
        {
            string folder = "Assets/MaleVariantInheritanceTest";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            AssetDatabase.CreateFolder("Assets", "MaleVariantInheritanceTest");
            GameObject parent = null;
            GameObject child = null;
            try
            {
                parent = (GameObject)PrefabUtility.InstantiatePrefab(Load(BasePath));
                string parentPath = folder + "/Parent.prefab";
                PrefabUtility.SaveAsPrefabAsset(parent, parentPath);
                Object.DestroyImmediate(parent);
                parent = null;
                child = (GameObject)PrefabUtility.InstantiatePrefab(Load(parentPath));
                var color = new Color(.62f, .40f, .27f);
                var tone = child.GetComponent<CharacterSkinTone>();
                tone.SetSkinColor(color);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tone);
                string childPath = folder + "/Child.prefab";
                PrefabUtility.SaveAsPrefabAsset(child, childPath);
                Object.DestroyImmediate(child);
                child = null;
                parent = PrefabUtility.LoadPrefabContents(parentPath);
                parent.GetComponent<CharacterFaceController>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(parent, parentPath);
                PrefabUtility.UnloadPrefabContents(parent);
                parent = null;
                var updated = Load(childPath);
                Assert.That(updated.GetComponent<CharacterFaceController>().enabled, Is.False);
                Assert.That(updated.GetComponent<CharacterSkinTone>().SkinColor, Is.EqualTo(color));
            }
            finally
            {
                if (parent != null)
                    Object.DestroyImmediate(parent);
                if (child != null)
                    Object.DestroyImmediate(child);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
