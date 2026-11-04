using System.Linq;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public sealed class PasserbyOutfitTests
    {
        private const string Folder = "Assets/Prefabs/Characters/Mannequin/Npc/";
        private const string BasePath =
            "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Male_170.prefab";

        [System.Serializable]
        private sealed class WardrobeGeometry
        {
            public PartGeometry[] parts;
        }

        [System.Serializable]
        private sealed class PartGeometry
        {
            public string name;
            public Vector3[] vertices;
            public int[] triangles;
        }

        [Test]
        public void SavedWardrobeGeometryMatchesTheFittedSource()
        {
            var data = JsonUtility.FromJson<WardrobeGeometry>(
                System.IO.File.ReadAllText(
                    "../ArtSources/Characters/LittleMungPasserby/HairPantsParts.json"
                )
            );
            foreach (var part in data.parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/Art/Characters/Common/LittleMungPasserby/" + part.name + ".asset"
                );
                Assert.That(mesh.vertexCount, Is.EqualTo(part.vertices.Length), part.name);
                CollectionAssert.AreEqual(part.triangles, mesh.triangles, part.name);
                Assert.That(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), Is.True);
                var instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        Folder + "LittleMung_Passerby_Male.prefab"
                    )
                );
                try
                {
                    var skin = instance
                        .GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Single(r => r.name == part.name);
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                        Assert.That(
                            Vector3.Distance(
                                skin.transform.TransformPoint(vertices[i]),
                                part.vertices[i]
                            ),
                            Is.LessThan(.00001f),
                            part.name + " fitted position " + i
                        );
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void HairAndPantsUseBaseBonesWithoutChangingFaceOrSpeech()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var hair = skins.SingleOrDefault(s => s.name == "Hair_SidePart_Brown");
                var pants = skins.SingleOrDefault(s => s.name == "Suit_Navy_Pants");
                Assert.That(hair, Is.Not.Null, "Generated hair is not fitted to HairSlot");
                Assert.That(pants, Is.Not.Null, "Generated pants are not fitted to PantsSlot");
                Assert.That(
                    hair.transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.Head)),
                    Is.True
                );
                Assert.That(pants.transform.parent.name, Is.EqualTo("PantsSlot"));
                foreach (var skin in new[] { hair, pants })
                {
                    Assert.That(skin.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                    Assert.That(
                        skin.sharedMesh.boneWeights.Length,
                        Is.EqualTo(skin.sharedMesh.vertexCount)
                    );
                    foreach (var row in skin.sharedMesh.boneWeights)
                    {
                        var indices = new[]
                        {
                            row.boneIndex0,
                            row.boneIndex1,
                            row.boneIndex2,
                            row.boneIndex3,
                        };
                        var weights = new[] { row.weight0, row.weight1, row.weight2, row.weight3 };
                        Assert.That(weights.Sum(), Is.EqualTo(1).Within(.0001f));
                        for (int k = 0; k < 4; k++)
                        {
                            if (weights[k] <= .00001f)
                                continue;
                            var bone = skin.bones[indices[k]];
                            Assert.That(bone.IsChildOf(instance.transform), Is.True);
                            if (skin == hair)
                                Assert.That(
                                    bone,
                                    Is.SameAs(animator.GetBoneTransform(HumanBodyBones.Head))
                                );
                            else
                                Assert.That(
                                    new[]
                                    {
                                        "Hips",
                                        "Spine",
                                        "Left_UpperLeg",
                                        "Right_UpperLeg",
                                        "Left_LowerLeg",
                                        "Right_LowerLeg",
                                    },
                                    Does.Contain(bone.name)
                                );
                        }
                    }
                }
                var face = instance.GetComponentInChildren<CharacterFaceController>();
                foreach (CharacterFacePose pose in System.Enum.GetValues(typeof(CharacterFacePose)))
                    Assert.DoesNotThrow(() => face.SetPose(pose));
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
                face.ApplySpeech(timeline.SampleArticulation(.2f), .3f, .7f);
                var lips = skins.Single(s => s.sharedMesh.name == "SpeakingLips");
                Assert.That(
                    lips.GetBlendShapeWeight(lips.sharedMesh.GetBlendShapeIndex("viseme_AA")),
                    Is.GreaterThan(10)
                );
                var lid = skins.Single(s => s.sharedMesh.name == "UpperLeftLid");
                Assert.That(
                    lid.GetBlendShapeWeight(lid.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.GreaterThan(0)
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void OutfitInheritsBaseFaceAndUsesOnlyItsSkeleton()
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
            var child = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            Assert.That(child, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(child), Is.SameAs(parent));
            var sourceSkins = parent.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skins = child.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var source in sourceSkins)
                Assert.That(
                    skins.Any(s => s.sharedMesh == source.sharedMesh),
                    Is.True,
                    source.name
                );
            var animator = child.GetComponentInChildren<Animator>();
            Assert.That(
                animator.avatar,
                Is.SameAs(parent.GetComponentInChildren<Animator>().avatar)
            );
            Assert.That(child.GetComponentInChildren<CharacterFaceController>(), Is.Not.Null);
            foreach (string name in new[] { "Suit_Navy_Upper" })
            {
                var skin = skins.Single(s => s.name == name);
                Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(100));
                Assert.That(
                    skin.bones.All(b => b != null && b.IsChildOf(child.transform)),
                    Is.True
                );
                Assert.That(
                    skin.sharedMesh.boneWeights.Length,
                    Is.EqualTo(skin.sharedMesh.vertexCount)
                );
                foreach (var weight in skin.sharedMesh.boneWeights)
                    Assert.That(
                        weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                        Is.EqualTo(1).Within(.0001f)
                    );
            }
        }

        [Test]
        public void JacketDoesNotFollowLegsFingersOrHead()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            var skin = prefab
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(r => r.name == "Suit_Navy_Upper");
            var allowed = new[]
            {
                "Hips",
                "Spine",
                "Chest",
                "UpperChest",
                "Neck",
                "Neck_Twist_A",
                "Left_Shoulder",
                "Right_Shoulder",
                "Left_UpperArm",
                "Right_UpperArm",
                "Left_LowerArm",
                "Right_LowerArm",
            };
            foreach (var row in skin.sharedMesh.boneWeights)
            {
                var indices = new[]
                {
                    row.boneIndex0,
                    row.boneIndex1,
                    row.boneIndex2,
                    row.boneIndex3,
                };
                var weights = new[] { row.weight0, row.weight1, row.weight2, row.weight3 };
                for (int k = 0; k < 4; k++)
                    if (weights[k] > .00001f)
                        Assert.That(
                            allowed,
                            Does.Contain(skin.bones[indices[k]].name),
                            "Unrelated bone pulls jacket vertices"
                        );
            }
        }

        [Test]
        public void CoveragePreservesSkinAtBothWristOpenings()
        {
            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        Folder + "LittleMung_Passerby_Male.prefab"
                    )
                );
            var baked = new Mesh();
            var texture = new Texture2D(2, 2);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var body = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.vertexCount == 8782);
                body.BakeMesh(baked, true);
                texture.LoadImage(
                    System.IO.File.ReadAllBytes(
                        AssetDatabase.GetAssetPath(body.sharedMaterial.GetTexture("_BaseMap"))
                    )
                );
                var points = baked.vertices.Select(body.transform.TransformPoint).ToArray();
                var uv = body.sharedMesh.uv;
                foreach (var hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    var wrist = animator.GetBoneTransform(hand).position;
                    var indices = Enumerable
                        .Range(0, points.Length)
                        .Where(i => Vector3.Distance(points[i], wrist) < .065f)
                        .ToArray();
                    Assert.That(indices.Length, Is.GreaterThan(10), "Wrist sample is missing");
                    foreach (int index in indices)
                    {
                        int x = Mathf.Clamp(
                            (int)(uv[index].x * texture.width),
                            0,
                            texture.width - 1
                        );
                        int y = Mathf.Clamp(
                            (int)(uv[index].y * texture.height),
                            0,
                            texture.height - 1
                        );
                        Assert.That(
                            texture.GetPixel(x, y).a,
                            Is.GreaterThan(.95f),
                            hand + " skin is erased at the open cuff"
                        );
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PasserbyVariantRetainsBehaviorAndConnectsItsAnimator()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Passerby_Male.prefab"
            );
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromSource(prefab),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(BasePath))
            );
            Assert.That(
                prefab.GetComponent<CharacterController>().height,
                Is.EqualTo(1.7f).Within(.001)
            );
            Assert.That(
                prefab
                    .GetComponents<MonoBehaviour>()
                    .Any(c => c.GetType().FullName == "A305.Npc.Passerby"),
                Is.True
            );
            var driver = new SerializedObject(prefab.GetComponent<CharacterAnimationDriver>());
            Assert.That(
                driver.FindProperty("animator").objectReferenceValue,
                Is.SameAs(prefab.GetComponentInChildren<Animator>())
            );
            Assert.That(prefab.GetComponentInChildren<CharacterFaceController>(), Is.Not.Null);
            foreach (string name in new[] { "HairSlot", "UpperSlot", "PantsSlot", "ShoesSlot" })
                Assert.That(
                    prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == name),
                    Is.EqualTo(1),
                    name
                );
        }
    }
}
