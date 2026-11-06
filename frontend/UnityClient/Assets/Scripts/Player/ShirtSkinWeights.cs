using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>스킨 가중치를 보정할 때 뼈를 나누는 역할.</summary>
    public enum SkinBoneRole
    {
        Other,
        Torso,
        Leg,
        Arm,
    }

    /// <summary>
    /// 자동 리깅한 리틀 멍은 셔츠 아랫단이 허벅지·팔 뼈를 따라가서,
    /// 다리를 접으면 엉덩이 쪽으로 끌려간다. 팔꿈치보다 낮은 셔츠 정점(몸통 아랫단)이
    /// 골반·척추만 따라가도록 가중치를 옮긴다.
    /// </summary>
    public static class ShirtSkinWeights
    {
        // 리틀 멍 셔츠는 파란색이다. 청록 반바지는 파랑과 초록이 비슷하고, 피부는 빨강이 가장 크다.
        private const int MinBlueOverGreen = 30;
        private const int MinBlueOverRed = 40;

        public static bool IsShirtColor(Color32 color)
        {
            return color.b - color.g > MinBlueOverGreen && color.b - color.r > MinBlueOverRed;
        }

        /// <param name="heights">골반에서 머리 방향으로 잰 정점 높이.</param>
        /// <param name="roles">뼈 번호별 역할.</param>
        /// <param name="hipsBone">몸통 가중치가 하나도 남지 않을 때 따라갈 골반 뼈 번호.</param>
        /// <param name="elbowHeight">
        /// 팔꿈치 높이. 이보다 낮은 셔츠는 소매가 아닌 몸통 아랫단이다.
        /// </param>
        public static BoneWeight[] Correct(
            IReadOnlyList<BoneWeight> weights,
            IReadOnlyList<Color32> colors,
            IReadOnlyList<float> heights,
            IReadOnlyList<SkinBoneRole> roles,
            int hipsBone,
            float elbowHeight
        )
        {
            var corrected = new BoneWeight[weights.Count];
            for (int i = 0; i < weights.Count; i++)
            {
                bool shirtHem = IsShirtColor(colors[i]) && heights[i] < elbowHeight;
                corrected[i] = shirtHem ? CorrectHem(weights[i], roles, hipsBone) : weights[i];
            }

            return corrected;
        }

        private static BoneWeight CorrectHem(
            BoneWeight weight,
            IReadOnlyList<SkinBoneRole> roles,
            int hipsBone
        )
        {
            var kept = new List<(int bone, float weight)>(4);
            float removed = 0f;
            foreach ((int bone, float value) in Parts(weight))
            {
                if (value <= 0f)
                {
                    continue;
                }

                SkinBoneRole role = roles[bone];
                if (role == SkinBoneRole.Leg || role == SkinBoneRole.Arm)
                {
                    removed += value;
                }
                else
                {
                    Merge(kept, bone, value);
                }
            }

            // 옮길 가중치가 없으면 원래 값을 그대로 두어, 다시 실행해도 결과가 바뀌지 않게 한다.
            if (removed <= 0f)
            {
                return weight;
            }

            float total = 0f;
            foreach (var (_, value) in kept)
            {
                total += value;
            }

            if (total <= 0f)
            {
                return new BoneWeight { boneIndex0 = hipsBone, weight0 = 1f };
            }

            kept.Sort((a, b) => b.weight.CompareTo(a.weight));
            var result = new BoneWeight();
            for (int slot = 0; slot < kept.Count; slot++)
            {
                SetSlot(ref result, slot, kept[slot].bone, kept[slot].weight / total);
            }

            return result;
        }

        private static IEnumerable<(int bone, float weight)> Parts(BoneWeight weight)
        {
            yield return (weight.boneIndex0, weight.weight0);
            yield return (weight.boneIndex1, weight.weight1);
            yield return (weight.boneIndex2, weight.weight2);
            yield return (weight.boneIndex3, weight.weight3);
        }

        private static void Merge(List<(int bone, float weight)> kept, int bone, float value)
        {
            for (int i = 0; i < kept.Count; i++)
            {
                if (kept[i].bone == bone)
                {
                    kept[i] = (bone, kept[i].weight + value);
                    return;
                }
            }

            kept.Add((bone, value));
        }

        private static void SetSlot(ref BoneWeight weight, int slot, int bone, float value)
        {
            switch (slot)
            {
                case 0:
                    weight.boneIndex0 = bone;
                    weight.weight0 = value;
                    break;
                case 1:
                    weight.boneIndex1 = bone;
                    weight.weight1 = value;
                    break;
                case 2:
                    weight.boneIndex2 = bone;
                    weight.weight2 = value;
                    break;
                default:
                    weight.boneIndex3 = bone;
                    weight.weight3 = value;
                    break;
            }
        }
    }
}
