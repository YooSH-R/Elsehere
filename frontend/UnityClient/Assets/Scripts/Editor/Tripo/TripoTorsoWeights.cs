using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    /// <summary>몸통 안쪽으로 새어 들어온 팔 가중치만 기존 몸통 본으로 되돌린다.</summary>
    public static class TripoTorsoWeights
    {
        public static BoneWeight[] Prepare(SkinnedMeshRenderer skin, Animator animator)
        {
            var mesh = skin.sharedMesh;
            var result = mesh.boneWeights;
            var frame = TripoMasterRig.BodyFrame(animator).inverse;
            Vector3 Joint(HumanBodyBones id) =>
                frame.MultiplyPoint3x4(animator.GetBoneTransform(id).position);
            var torso = new HashSet<Transform>(
                new[]
                {
                    HumanBodyBones.Hips,
                    HumanBodyBones.Spine,
                    HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest,
                }
                    .Select(animator.GetBoneTransform)
                    .Where(b => b != null)
            );
            var arms = new HashSet<Transform>();
            foreach (var id in new[] { HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder })
            {
                var shoulder = animator.GetBoneTransform(id);
                if (shoulder == null)
                    return result;
                arms.UnionWith(shoulder.GetComponentsInChildren<Transform>());
            }
            Vector3 left = Joint(HumanBodyBones.LeftUpperArm),
                right = Joint(HumanBodyBones.RightUpperArm);
            Vector3 leftElbow = Joint(HumanBodyBones.LeftLowerArm),
                rightElbow = Joint(HumanBodyBones.RightLowerArm);
            Vector3 neck = Joint(HumanBodyBones.Neck);
            float halfWidth = Mathf.Abs(right.x - left.x) * .5f;
            float shoulderHeight = (left.y + right.y) * .5f;
            var matrices = skin
                .bones.Select((bone, i) => frame * bone.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
            var vertices = mesh.vertices;
            for (int vertex = 0; vertex < result.Length; vertex++)
            {
                var w = result[vertex];
                int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                float torsoWeight = 0,
                    armWeight = 0;
                Vector3 point = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    point += matrices[ids[i]].MultiplyPoint3x4(vertices[vertex]) * values[i];
                    if (torso.Contains(skin.bones[ids[i]]))
                        torsoWeight += values[i];
                    if (arms.Contains(skin.bones[ids[i]]))
                        armWeight += values[i];
                }
                // 소매·손을 임의로 몸통으로 분류하지 않고 기존 몸통 영향이 있는 안쪽만 수정한다.
                if (torsoWeight < .2f || armWeight <= 0f)
                    continue;
                float lateral = 1f - Fade(halfWidth * 1.15f, halfWidth * 1.65f, Mathf.Abs(point.x));
                float vertical =
                    Fade(-.05f, .1f, point.y) * (1f - Fade(shoulderHeight - .08f, neck.y, point.y));
                float torsoDistance = Distance(point, Vector3.zero, neck);
                float armDistance = Mathf.Min(
                    Distance(point, left, leftElbow),
                    Distance(point, right, rightElbow)
                );
                float proximity = Fade(
                    .30f,
                    .40f,
                    armDistance / Mathf.Max(.0001f, armDistance + torsoDistance)
                );
                float removed = armWeight * lateral * vertical * proximity;
                if (removed <= .000001f)
                    continue;
                for (int i = 0; i < 4; i++)
                {
                    if (arms.Contains(skin.bones[ids[i]]))
                        values[i] *= 1f - removed / armWeight;
                    if (torso.Contains(skin.bones[ids[i]]))
                        values[i] *= 1f + removed / torsoWeight;
                }
                // Unity의 BoneWeight는 큰 가중치부터 저장한다. 본 추가나 메시 구조 변경은 없다.
                System.Array.Sort(values, ids);
                System.Array.Reverse(values);
                System.Array.Reverse(ids);
                result[vertex] = new BoneWeight
                {
                    boneIndex0 = ids[0],
                    boneIndex1 = ids[1],
                    boneIndex2 = ids[2],
                    boneIndex3 = ids[3],
                    weight0 = values[0],
                    weight1 = values[1],
                    weight2 = values[2],
                    weight3 = values[3],
                };
            }
            return result;
        }

        private static float Fade(float start, float end, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

        private static float Distance(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            float t = Mathf.Clamp01(
                Vector3.Dot(point - start, direction) / Mathf.Max(.000001f, direction.sqrMagnitude)
            );
            return Vector3.Distance(point, start + direction * t);
        }
    }
}
