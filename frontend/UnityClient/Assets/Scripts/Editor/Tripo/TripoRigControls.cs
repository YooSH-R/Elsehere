using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace A305.EditorTools.Tripo
{
    public static class TripoRigControls
    {
        public static Transform AddLimb(
            GameObject rig,
            string name,
            Transform root,
            Transform mid,
            Transform tip
        )
        {
            Transform existing = rig.transform.Find(name);
            if (existing != null)
                return existing;
            if (
                root == null
                || mid == null
                || tip == null
                || !mid.IsChildOf(root)
                || !tip.IsChildOf(mid)
            )
                throw new InvalidOperationException("연속된 팔/다리 본이 필요합니다: " + name);
            var control = new GameObject(name);
            control.transform.SetParent(rig.transform, false);
            var target = new GameObject("Target").transform;
            target.SetParent(control.transform, false);
            target.SetPositionAndRotation(tip.position, tip.rotation);
            var hint = new GameObject("Hint").transform;
            hint.SetParent(control.transform, false);
            Vector3 axis = tip.position - root.position;
            Vector3 bend = mid.position - root.position;
            Vector3 perpendicular = bend - Vector3.Project(bend, axis);
            if (perpendicular.sqrMagnitude < 0.000001f)
                perpendicular = rig.transform.forward;
            hint.position =
                mid.position + perpendicular.normalized * Mathf.Max(axis.magnitude * 0.5f, 0.1f);
            var constraint = control.AddComponent<TwoBoneIKConstraint>();
            var data = constraint.data;
            data.root = root;
            data.mid = mid;
            data.tip = tip;
            data.target = target;
            data.hint = hint;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 1f;
            data.hintWeight = 1f;
            constraint.data = data;
            constraint.weight = 1f;
            return control.transform;
        }

        public static RigBuilder BuildHumanoid(GameObject model)
        {
            TripoModelReview.RequireHumanoid(model);
            Animator animator = model.GetComponentInChildren<Animator>(true);
            var builder = animator.GetComponent<RigBuilder>();
            if (builder == null)
                builder = animator.gameObject.AddComponent<RigBuilder>();
            Transform existing = animator.transform.Find("TripoControls");
            if (existing != null)
                return builder;
            var rigObject = new GameObject("TripoControls");
            rigObject.transform.SetParent(animator.transform, false);
            var rig = rigObject.AddComponent<Rig>();
            AddLimb(
                rigObject,
                "LeftHand",
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                animator.GetBoneTransform(HumanBodyBones.LeftHand)
            );
            AddLimb(
                rigObject,
                "RightHand",
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                animator.GetBoneTransform(HumanBodyBones.RightHand)
            );
            AddLimb(
                rigObject,
                "LeftFoot",
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftFoot)
            );
            AddLimb(
                rigObject,
                "RightFoot",
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.RightFoot)
            );
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            var headObject = new GameObject("Head");
            headObject.transform.SetParent(rigObject.transform, false);
            var headTarget = new GameObject("Target").transform;
            headTarget.SetParent(headObject.transform, false);
            headTarget.SetPositionAndRotation(head.position, head.rotation);
            var rotation = headObject.AddComponent<MultiRotationConstraint>();
            var rotationData = rotation.data;
            rotationData.constrainedObject = head;
            rotationData.sourceObjects = new WeightedTransformArray
            {
                new WeightedTransform(headTarget, 1f),
            };
            rotation.data = rotationData;
            rotation.weight = 1f;
            builder.layers.Add(new RigLayer(rig));
            return builder;
        }
    }
}
