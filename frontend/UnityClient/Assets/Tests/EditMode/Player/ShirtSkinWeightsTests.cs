using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 점프로 다리를 접어도 셔츠 아랫단이 엉덩이 쪽으로 끌려가지 않도록,
    /// 소매를 제외한 팔꿈치보다 낮은 셔츠 정점은 골반·척추만 따라가야 한다.
    /// 반바지·소매·피부는 원래 가중치를 유지한다.
    /// </summary>
    public class ShirtSkinWeightsTests
    {
        private const int Hips = 0;
        private const int Spine = 1;
        private const int Neck = 2;
        private const int UpperLeg = 3;
        private const int LowerArm = 4;
        private const float ElbowHeight = 0.1f;
        private const float Below = 0f;
        private const float Above = 0.15f;

        private static readonly SkinBoneRole[] Roles =
        {
            SkinBoneRole.Torso,
            SkinBoneRole.Torso,
            SkinBoneRole.Other,
            SkinBoneRole.Leg,
            SkinBoneRole.Arm,
        };

        private static readonly Color32 Shirt = new Color32(96, 128, 192, 255);
        private static readonly Color32 Shorts = new Color32(64, 160, 160, 255);
        private static readonly Color32 Skin = new Color32(224, 160, 96, 255);

        [Test]
        public void ShirtColor_IsBlueButNotTealShortsOrSkin()
        {
            Assert.That(ShirtSkinWeights.IsShirtColor(Shirt), Is.True);
            Assert.That(ShirtSkinWeights.IsShirtColor(new Color32(64, 96, 160, 255)), Is.True);
            Assert.That(ShirtSkinWeights.IsShirtColor(Shorts), Is.False);
            Assert.That(ShirtSkinWeights.IsShirtColor(Skin), Is.False);
        }

        [Test]
        public void ShirtHemBelowElbow_MovesLegAndArmWeightToTorso()
        {
            BoneWeight hem = Weight((Hips, .2f), (Spine, .2f), (UpperLeg, .4f), (LowerArm, .2f));

            BoneWeight corrected = CorrectOne(hem, Shirt, Below);

            AssertWeights(corrected, (Hips, .5f), (Spine, .5f));
        }

        [Test]
        public void ShirtHemWithoutTorsoWeight_FollowsHips()
        {
            BoneWeight hem = Weight((UpperLeg, .7f), (LowerArm, .3f));

            BoneWeight corrected = CorrectOne(hem, Shirt, Below);

            AssertWeights(corrected, (Hips, 1f));
        }

        [Test]
        public void ShirtHem_KeepsBonesThatAreNeitherLegNorArm()
        {
            BoneWeight hem = Weight((Spine, .3f), (Neck, .3f), (UpperLeg, .4f));

            BoneWeight corrected = CorrectOne(hem, Shirt, Below);

            AssertWeights(corrected, (Spine, .5f), (Neck, .5f));
        }

        [Test]
        public void ShortsSkinAndSleeve_KeepTheirWeights()
        {
            BoneWeight original = Weight((Hips, .2f), (UpperLeg, .6f), (LowerArm, .2f));

            AssertWeights(
                CorrectOne(original, Shorts, Below),
                (Hips, .2f),
                (UpperLeg, .6f),
                (LowerArm, .2f)
            );
            AssertWeights(
                CorrectOne(original, Skin, Below),
                (Hips, .2f),
                (UpperLeg, .6f),
                (LowerArm, .2f)
            );
            AssertWeights(
                CorrectOne(original, Shirt, Above),
                (Hips, .2f),
                (UpperLeg, .6f),
                (LowerArm, .2f)
            );
        }

        [Test]
        public void SleeveBelowElbow_KeepsArmWeightsDespiteMatchingShirtColor()
        {
            BoneWeight sleeve = Weight((LowerArm, .9f), (Spine, .1f));
            BoneWeight corrected = ShirtSkinWeights.Correct(
                new[] { sleeve },
                new[] { Shirt },
                new[] { Below },
                Roles,
                Hips,
                ElbowHeight,
                new[] { true }
            )[0];
            AssertWeights(corrected, (LowerArm, .9f), (Spine, .1f));
        }

        [Test]
        public void CorrectedWeights_AreSortedHeaviestFirst()
        {
            BoneWeight hem = Weight((UpperLeg, .5f), (Spine, .1f), (Hips, .4f));

            BoneWeight corrected = CorrectOne(hem, Shirt, Below);

            Assert.That(corrected.boneIndex0, Is.EqualTo(Hips));
            Assert.That(corrected.weight0, Is.EqualTo(.8f).Within(1e-5f));
            Assert.That(corrected.boneIndex1, Is.EqualTo(Spine));
            Assert.That(corrected.weight1, Is.EqualTo(.2f).Within(1e-5f));
            Assert.That(corrected.weight2, Is.Zero);
            Assert.That(corrected.weight3, Is.Zero);
        }

        [Test]
        public void CorrectingTwice_GivesTheSameWeights()
        {
            var weights = new[]
            {
                Weight((Hips, .2f), (Spine, .2f), (UpperLeg, .4f), (LowerArm, .2f)),
                Weight((Hips, .2f), (UpperLeg, .8f)),
            };
            var colors = new[] { Shirt, Shorts };
            var heights = new[] { Below, Below };

            BoneWeight[] once = ShirtSkinWeights.Correct(
                weights,
                colors,
                heights,
                Roles,
                Hips,
                ElbowHeight,
                new[] { false, false }
            );
            BoneWeight[] twice = ShirtSkinWeights.Correct(
                once,
                colors,
                heights,
                Roles,
                Hips,
                ElbowHeight,
                new[] { false, false }
            );

            Assert.That(twice, Is.EqualTo(once));
        }

        private static BoneWeight CorrectOne(BoneWeight weight, Color32 color, float height)
        {
            return ShirtSkinWeights.Correct(
                new[] { weight },
                new[] { color },
                new[] { height },
                Roles,
                Hips,
                ElbowHeight,
                new[] { false }
            )[0];
        }

        private static BoneWeight Weight(params (int bone, float weight)[] parts)
        {
            var weight = new BoneWeight();
            for (int i = 0; i < parts.Length; i++)
            {
                switch (i)
                {
                    case 0:
                        weight.boneIndex0 = parts[i].bone;
                        weight.weight0 = parts[i].weight;
                        break;
                    case 1:
                        weight.boneIndex1 = parts[i].bone;
                        weight.weight1 = parts[i].weight;
                        break;
                    case 2:
                        weight.boneIndex2 = parts[i].bone;
                        weight.weight2 = parts[i].weight;
                        break;
                    default:
                        weight.boneIndex3 = parts[i].bone;
                        weight.weight3 = parts[i].weight;
                        break;
                }
            }

            return weight;
        }

        private static void AssertWeights(
            BoneWeight actual,
            params (int bone, float weight)[] expected
        )
        {
            var byBone = new Dictionary<int, float>();
            Add(byBone, actual.boneIndex0, actual.weight0);
            Add(byBone, actual.boneIndex1, actual.weight1);
            Add(byBone, actual.boneIndex2, actual.weight2);
            Add(byBone, actual.boneIndex3, actual.weight3);
            Assert.That(byBone.Count, Is.EqualTo(expected.Length), "가중치가 있는 뼈 수");
            foreach ((int bone, float weight) in expected)
            {
                Assert.That(byBone.ContainsKey(bone), Is.True, $"뼈 {bone}");
                Assert.That(byBone[bone], Is.EqualTo(weight).Within(1e-5f), $"뼈 {bone}");
            }
        }

        private static void Add(Dictionary<int, float> byBone, int bone, float weight)
        {
            if (weight > 0f)
            {
                byBone[bone] = byBone.TryGetValue(bone, out float sum) ? sum + weight : weight;
            }
        }
    }
}
